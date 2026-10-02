using System.Collections.Generic;
using Game.HMI;
using Game.Production;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Wires the machine terminals of the Portioner and the Press in the open scene, following the
    /// Mixer pattern, and adds the setpoint / actual value section to the operator screen.
    ///
    /// Menu: Tools / Food Production / Setup Machine Terminals (Portioner + Press)
    ///
    /// What it does (undoable with Ctrl+Z, run again any time - it updates instead of duplicating):
    ///  - for every PortionerMachine and PressMachine in the scene:
    ///      * HmiTerminalInteractable of its nested MachineTerminal -> _machine = machine, _panelId = "machine"
    ///      * MachineDetailBinder on the machine GameObject (like the mixer) -> binds the terminal's
    ///        ambient display (state, content, tiles) to the machine
    ///  - for every "machine" MachineDetailPanel under an HmiScreenController (Canvas_HMI):
    ///      * adds a "MachineValues" child (MachineParameterListView) in the scroll content, right
    ///        before the content message text, and assigns it to the panel
    /// Setpoints/actual values themselves come from IMachineParameterSource on the machines at runtime.
    /// </summary>
    public static class MachineTerminalSetup
    {
        private const string MenuPath = "Tools/Food Production/Setup Machine Terminals (Portioner + Press)";
        private const string ValuesObjectName = "MachineValues";

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var log = new List<string>();
            var problems = new List<string>();

            Undo.SetCurrentGroupName("Setup Machine Terminals");
            int undoGroup = Undo.GetCurrentGroup();

            var machines = new List<MachineBase>();
            machines.AddRange(Object.FindObjectsByType<PortionerMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            machines.AddRange(Object.FindObjectsByType<PressMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None));

            if (machines.Count == 0)
            {
                problems.Add("Keine PortionerMachine / PressMachine in der offenen Szene gefunden.");
            }

            foreach (MachineBase machine in machines)
            {
                WireMachineTerminal(machine, log, problems);
            }

            SO_HmiTheme theme = FindTheme();
            int screenPanels = 0;
            foreach (HmiScreenController screen in Object.FindObjectsByType<HmiScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                foreach (MachineDetailPanel panel in screen.GetComponentsInChildren<MachineDetailPanel>(true))
                {
                    if (panel.PanelId != "machine")
                    {
                        continue;
                    }

                    AddValueSection(panel, theme, log, problems);
                    screenPanels++;
                }
            }

            if (screenPanels == 0)
            {
                problems.Add("Kein Maschinen-Panel (panelId \"machine\") unter einem HmiScreenController gefunden - " +
                             "Sollwerte werden nur im Bildschirm-HMI angezeigt.");
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            string message = string.Join("\n", log);
            if (problems.Count > 0)
            {
                message += "\n\nProbleme:\n- " + string.Join("\n- ", problems);
            }

            Debug.Log("[MachineTerminalSetup]\n" + message);
            EditorUtility.DisplayDialog("Setup Machine Terminals", message + "\n\nSzene speichern nicht vergessen (Strg+S).", "OK");
        }

        private static void WireMachineTerminal(MachineBase machine, List<string> log, List<string> problems)
        {
            HmiTerminalInteractable terminal = machine.GetComponentInChildren<HmiTerminalInteractable>(true);
            if (terminal == null)
            {
                problems.Add($"{machine.name}: kein MachineTerminal (HmiTerminalInteractable) als Kind gefunden.");
                return;
            }

            var terminalSo = new SerializedObject(terminal);
            terminalSo.FindProperty("_machine").objectReferenceValue = machine;
            terminalSo.FindProperty("_panelId").stringValue = "machine";
            terminalSo.ApplyModifiedProperties();

            // Ambient display on the terminal itself (same component the mixer binds).
            MachineDetailPanel ambientPanel = terminal.GetComponent<MachineDetailPanel>();
            if (ambientPanel == null)
            {
                ambientPanel = terminal.GetComponentInChildren<MachineDetailPanel>(true);
            }

            if (ambientPanel == null)
            {
                problems.Add($"{machine.name}: MachineTerminal hat kein MachineDetailPanel - Binder nicht angelegt.");
                log.Add($"{machine.name}: Terminal -> Maschine verknüpft.");
                return;
            }

            MachineDetailBinder binder = machine.GetComponent<MachineDetailBinder>();
            bool created = false;
            if (binder == null)
            {
                binder = Undo.AddComponent<MachineDetailBinder>(machine.gameObject);
                created = true;
            }

            var binderSo = new SerializedObject(binder);
            binderSo.FindProperty("_detailPanel").objectReferenceValue = ambientPanel;
            binderSo.FindProperty("_machineInstance").objectReferenceValue = machine;
            binderSo.ApplyModifiedProperties();

            log.Add($"{machine.name}: Terminal -> Maschine verknüpft, MachineDetailBinder {(created ? "angelegt" : "aktualisiert")}.");
        }

        private static void AddValueSection(MachineDetailPanel panel, SO_HmiTheme theme, List<string> log, List<string> problems)
        {
            var panelSo = new SerializedObject(panel);
            var contentText = panelSo.FindProperty("_content").objectReferenceValue as TextMeshProUGUI;

            Transform parent = contentText != null ? contentText.transform.parent : FindDeep(panel.transform, "Content");
            if (parent == null)
            {
                problems.Add($"{panel.name}: weder _content noch ein 'Content'-Objekt gefunden.");
                return;
            }

            Transform existing = parent.Find(ValuesObjectName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(ValuesObjectName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + ValuesObjectName);
                Undo.SetTransformParent(go.transform, parent, "Parent " + ValuesObjectName);
                go.layer = parent.gameObject.layer;

                var rect = (RectTransform)go.transform;
                rect.localScale = Vector3.one;
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, 0f);
            }

            if (contentText != null)
            {
                // Directly after the tiles, before the free-text machine message.
                go.transform.SetSiblingIndex(contentText.transform.GetSiblingIndex());
            }

            MachineParameterListView view = go.GetComponent<MachineParameterListView>();
            if (view == null)
            {
                view = Undo.AddComponent<MachineParameterListView>(go);
            }

            var viewSo = new SerializedObject(view);
            if (theme != null)
            {
                viewSo.FindProperty("_theme").objectReferenceValue = theme;
            }
            if (contentText != null)
            {
                viewSo.FindProperty("_styleSource").objectReferenceValue = contentText;
            }
            viewSo.ApplyModifiedProperties();

            panelSo.FindProperty("_parameterList").objectReferenceValue = view;
            panelSo.ApplyModifiedProperties();

            log.Add($"Bildschirm-HMI '{GetPath(panel.transform)}': Sollwert-/Istwert-Bereich {(existing != null ? "aktualisiert" : "angelegt")}.");
        }

        private static SO_HmiTheme FindTheme()
        {
            string[] guids = AssetDatabase.FindAssets("t:SO_HmiTheme");
            return guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<SO_HmiTheme>(AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.Trim() == childName)
                {
                    return t;
                }
            }
            return null;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }
            return path;
        }
    }
}
