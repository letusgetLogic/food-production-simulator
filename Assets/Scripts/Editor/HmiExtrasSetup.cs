using System.Collections.Generic;
using Game.Core;
using Game.HMI;
using Game.Persistence;
using Game.Production;
using Game.Quality;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds the Woche-3 UX screens to the open scene.
    ///
    /// Menu: Tools / Food Production / Setup Statistics Page + Pause Menu
    ///
    ///  - "Panel_Statistics" (StatisticsPanel, panel id "statistics") next to the overview panel of the
    ///    HMI screen, registered in HmiScreenController._panels; content is built at runtime
    ///  - button "Button_Statistics" (HmiNavButton) in the top-right corner of the overview panel
    ///  - root "PauseMenu" (PauseMenu, Escape) linked to the UI focus channel and the SaveLoadController
    /// Run "Setup Fault + Quality System" first. Undoable, can be run again (updates instead of duplicating).
    /// </summary>
    public static class HmiExtrasSetup
    {
        private const string Title = "Setup Statistics Page + Pause Menu";
        private const string StatisticsPanelName = "Panel_Statistics";
        private const string StatisticsButtonName = "Button_Statistics";
        private const string PauseMenuName = "PauseMenu";

        [MenuItem("Tools/Food Production/Setup Statistics Page + Pause Menu")]
        public static void Setup()
        {
            var log = new List<string>();
            Undo.SetCurrentGroupName(Title);
            int undoGroup = Undo.GetCurrentGroup();

            SO_HmiTheme theme = FindAsset<SO_HmiTheme>();
            SO_HmiInteractChannel interactChannel = FindAsset<SO_HmiInteractChannel>();
            SO_UiFocusChannel focusChannel = FindAsset<SO_UiFocusChannel>();
            QualityInspector quality = Object.FindFirstObjectByType<QualityInspector>(FindObjectsInactive.Include);
            FaultMonitor faults = Object.FindFirstObjectByType<FaultMonitor>(FindObjectsInactive.Include);
            SaveLoadController saveLoad = Object.FindFirstObjectByType<SaveLoadController>(FindObjectsInactive.Include);

            if (quality == null || faults == null)
            {
                log.Add("WARNUNG: kein QualityInspector/FaultMonitor - erst 'Setup Fault + Quality System' ausführen");
            }

            // ---- Statistics page ----
            OverviewPanel overview = Object.FindFirstObjectByType<OverviewPanel>(FindObjectsInactive.Include);
            HmiScreenController screen = FindScreenWith(overview);
            TextMeshProUGUI styleSource = overview != null ? overview.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            if (overview == null || screen == null)
            {
                log.Add("WARNUNG: OverviewPanel/HmiScreenController nicht gefunden - Statistik-Seite nicht angelegt");
            }
            else
            {
                StatisticsPanel statistics = SetupStatisticsPanel(overview, screen, theme, interactChannel, quality, faults, styleSource, log);
                SetupNavButton(overview, theme, interactChannel, styleSource, log);
                EditorSceneManager.MarkSceneDirty(statistics.gameObject.scene);
            }

            // ---- Pause menu ----
            GameObject pauseGo = FindRoot(PauseMenuName);
            if (pauseGo == null)
            {
                pauseGo = new GameObject(PauseMenuName);
                Undo.RegisterCreatedObjectUndo(pauseGo, "Create " + PauseMenuName);
                log.Add("'PauseMenu' angelegt");
            }

            PauseMenu pause = pauseGo.GetComponent<PauseMenu>();
            if (pause == null)
            {
                pause = Undo.AddComponent<PauseMenu>(pauseGo);
            }

            var pauseSo = new SerializedObject(pause);
            pauseSo.FindProperty("_uiFocusChannel").objectReferenceValue = focusChannel;
            pauseSo.FindProperty("_theme").objectReferenceValue = theme;
            pauseSo.FindProperty("_saveLoad").objectReferenceValue = saveLoad;
            pauseSo.FindProperty("_styleSource").objectReferenceValue = styleSource;
            pauseSo.FindProperty("_languageChannel").objectReferenceValue = FindAsset<SO_LanguageSwitcherChannel>();
            pauseSo.ApplyModifiedProperties();
            if (focusChannel == null)
            {
                log.Add("WARNUNG: kein SO_UiFocusChannel-Asset gefunden - Pausemenü ohne Fokus-Kanal");
            }
            if (saveLoad == null)
            {
                log.Add("Hinweis: kein SaveLoadController - Speichern/Laden im Pausemenü deaktiviert");
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(pauseGo.scene);

            string summary = "Fertig. Szene speichern (Strg+S) nicht vergessen.\n\n- " + string.Join("\n- ", log) +
                             "\n\nPrüfen: Lage des Statistik-Knopfs im Overview-Panel.";
            Debug.Log($"[{Title}]\n" + summary, pauseGo);
            SetupUi.Dialog(Title, summary, "OK");
        }

        private static StatisticsPanel SetupStatisticsPanel(OverviewPanel overview, HmiScreenController screen, SO_HmiTheme theme,
            SO_HmiInteractChannel interactChannel, QualityInspector quality, FaultMonitor faults, TextMeshProUGUI styleSource,
            List<string> log)
        {
            Transform parent = overview.transform.parent;
            Transform existing = parent.Find(StatisticsPanelName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(StatisticsPanelName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create " + StatisticsPanelName);
                Undo.SetTransformParent(go.transform, parent, "Parent " + StatisticsPanelName);
                go.layer = overview.gameObject.layer;
                log.Add("'Panel_Statistics' neben dem Overview-Panel angelegt");
            }

            // Same rect as the overview panel.
            var source = (RectTransform)overview.transform;
            var rect = (RectTransform)go.transform;
            Undo.RecordObject(rect, "Place " + StatisticsPanelName);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.anchoredPosition = source.anchoredPosition;
            rect.sizeDelta = source.sizeDelta;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;

            if (!go.TryGetComponent(out Image background))
            {
                background = Undo.AddComponent<Image>(go);
            }
            background.color = theme != null ? theme.PanelBackground : new Color(0.09f, 0.11f, 0.13f, 0.96f);

            StatisticsPanel panel = go.GetComponent<StatisticsPanel>();
            if (panel == null)
            {
                panel = Undo.AddComponent<StatisticsPanel>(go); // CanvasGroup comes via RequireComponent
            }

            var so = new SerializedObject(panel);
            so.FindProperty("_panelId").stringValue = "statistics";
            so.FindProperty("_theme").objectReferenceValue = theme;
            so.FindProperty("_interactChannel").objectReferenceValue = interactChannel;
            so.FindProperty("_qualityInspector").objectReferenceValue = quality;
            so.FindProperty("_faultMonitor").objectReferenceValue = faults;
            so.FindProperty("_styleSource").objectReferenceValue = styleSource;
            so.FindProperty("_backPanelId").stringValue = overview.PanelId;
            so.ApplyModifiedProperties();

            var screenSo = new SerializedObject(screen);
            SerializedProperty panels = screenSo.FindProperty("_panels");
            bool listed = false;
            for (int i = 0; i < panels.arraySize; i++)
            {
                listed |= panels.GetArrayElementAtIndex(i).objectReferenceValue == panel;
            }

            if (!listed)
            {
                panels.arraySize++;
                panels.GetArrayElementAtIndex(panels.arraySize - 1).objectReferenceValue = panel;
                screenSo.ApplyModifiedProperties();
                log.Add("Statistik-Seite im HmiScreenController registriert (Panel-ID 'statistics')");
            }

            return panel;
        }

        private static void SetupNavButton(OverviewPanel overview, SO_HmiTheme theme, SO_HmiInteractChannel interactChannel,
            TextMeshProUGUI styleSource, List<string> log)
        {
            Transform existing = overview.transform.Find(StatisticsButtonName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject(StatisticsButtonName, typeof(RectTransform), typeof(Image), typeof(Button));
                Undo.RegisterCreatedObjectUndo(go, "Create " + StatisticsButtonName);
                Undo.SetTransformParent(go.transform, overview.transform, "Parent " + StatisticsButtonName);
                go.layer = overview.gameObject.layer;

                var rect = (RectTransform)go.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-16f, -16f);
                rect.sizeDelta = new Vector2(220f, 56f);
                rect.localScale = Vector3.one;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                labelGo.layer = go.layer;
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.text = "STATISTICS";
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 22f;
                label.raycastTarget = false;
                if (styleSource != null)
                {
                    label.font = styleSource.font;
                }
                label.color = theme != null ? theme.ValueText : Color.white;
                log.Add("Knopf 'STATISTICS' oben rechts im Overview-Panel angelegt");
            }

            go.GetComponent<Image>().color = theme != null ? theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f, 1f);

            HmiNavButton nav = go.GetComponent<HmiNavButton>();
            if (nav == null)
            {
                nav = Undo.AddComponent<HmiNavButton>(go);
            }

            var so = new SerializedObject(nav);
            so.FindProperty("_interactChannel").objectReferenceValue = interactChannel;
            so.FindProperty("_targetPanelId").stringValue = "statistics";
            so.ApplyModifiedProperties();
        }

        private static HmiScreenController FindScreenWith(OverviewPanel overview)
        {
            if (overview == null)
            {
                return null;
            }

            foreach (HmiScreenController screen in Object.FindObjectsByType<HmiScreenController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                SerializedProperty panels = new SerializedObject(screen).FindProperty("_panels");
                for (int i = 0; i < panels.arraySize; i++)
                {
                    if (panels.GetArrayElementAtIndex(i).objectReferenceValue == overview)
                    {
                        return screen;
                    }
                }
            }
            return null;
        }

        private static T FindAsset<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                T asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                {
                    return asset;
                }
            }
            return null;
        }

        private static GameObject FindRoot(string rootName)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root;
                }
            }
            return null;
        }
    }
}
