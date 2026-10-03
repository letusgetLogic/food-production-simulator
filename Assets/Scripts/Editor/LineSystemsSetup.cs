using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.DebugTools;
using Game.HMI;
using Game.Persistence;
using Game.Production;
using Game.Quality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the line-wide systems of Woche 2 into the open scene.
    ///
    /// Menu: Tools / Food Production / Setup Fault + Quality System
    ///
    /// What it does (undoable with Ctrl+Z, run again any time - it updates instead of duplicating):
    ///  - root "LineSystems" with FaultMonitor (FaultSystem), QualityInspector (QualitySystem) and
    ///    LineStatusBinder (feeds the overview panels: throughput, produced, scrap, alarms)
    ///  - assets FaultCatalog (the MVP fault cases) and QualitySpec_Margherita on first run (later runs keep edits)
    ///  - SaveLoadController (product prefab = portioner's pizza) and DebugPanel (F1/F5/F9) on "LineSystems"
    ///  - root "LineEnd" (LineEndSink, trigger) behind the last station of the line: products that reach it are
    ///    judged and removed. The tunnel setups move it automatically when a new station is added.
    /// </summary>
    public static class LineSystemsSetup
    {
        private const string Title = "Setup Fault + Quality System";
        private const string SystemsRootName = "LineSystems";
        private const string LineEndName = "LineEnd";
        private const string FaultCatalogPath = "Assets/ScriptableObjects/Faults/FaultCatalog.asset";
        private const string QualitySpecPath = "Assets/ScriptableObjects/Quality/QualitySpec_Margherita.asset";

        /// <summary>Stations in reverse flow order - the first one found is the end of the line.</summary>
        private static readonly string[] StationsFromEnd =
        {
            "Packaging", "Shock Freezer", "Cooling Tunnel", "Oven", "Dosing Station"
        };

        [MenuItem("Tools/Food Production/Setup Fault + Quality System")]
        public static void Setup()
        {
            var log = new List<string>();
            Undo.SetCurrentGroupName(Title);
            int undoGroup = Undo.GetCurrentGroup();

            // ---- Assets ----
            SO_FaultCatalog catalog = AssetDatabase.LoadAssetAtPath<SO_FaultCatalog>(FaultCatalogPath);
            if (catalog == null)
            {
                EnsureFolder(Path.GetDirectoryName(FaultCatalogPath).Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<SO_FaultCatalog>();
                catalog.ResetToDefaults();
                AssetDatabase.CreateAsset(catalog, FaultCatalogPath);
                log.Add("Fehlerkatalog angelegt: " + FaultCatalogPath);
            }

            SO_QualitySpec spec = AssetDatabase.LoadAssetAtPath<SO_QualitySpec>(QualitySpecPath);
            if (spec == null)
            {
                EnsureFolder(Path.GetDirectoryName(QualitySpecPath).Replace('\\', '/'));
                spec = ScriptableObject.CreateInstance<SO_QualitySpec>();
                AssetDatabase.CreateAsset(spec, QualitySpecPath);
                log.Add("Qualitäts-Spezifikation angelegt: " + QualitySpecPath);
            }

            // ---- Line end ----
            Transform lineEnd = FindRoot(LineEndName);
            if (lineEnd == null)
            {
                var go = new GameObject(LineEndName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + LineEndName);
                lineEnd = go.transform;
                log.Add("'LineEnd' angelegt");
            }

            BoxCollider endBox = lineEnd.GetComponent<BoxCollider>();
            if (endBox == null)
            {
                endBox = Undo.AddComponent<BoxCollider>(lineEnd.gameObject);
            }
            endBox.isTrigger = true;

            LineEndSink sink = lineEnd.GetComponent<LineEndSink>();
            if (sink == null)
            {
                sink = Undo.AddComponent<LineEndSink>(lineEnd.gameObject);
            }

            if (!MoveLineEndBehindLastStation(log))
            {
                log.Add("WARNUNG: keine Station mit eigenem Band gefunden - 'LineEnd' bitte von Hand ans Linienende setzen");
            }

            // ---- Systems root ----
            Transform systems = FindRoot(SystemsRootName);
            if (systems == null)
            {
                var go = new GameObject(SystemsRootName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + SystemsRootName);
                systems = go.transform;
                log.Add("'LineSystems' angelegt");
            }

            FaultMonitor monitor = GetOrAdd<FaultMonitor>(systems.gameObject);
            var monitorSo = new SerializedObject(monitor);
            monitorSo.FindProperty("_catalog").objectReferenceValue = catalog;
            monitorSo.ApplyModifiedProperties();

            QualityInspector inspector = GetOrAdd<QualityInspector>(systems.gameObject);
            var inspectorSo = new SerializedObject(inspector);
            inspectorSo.FindProperty("_spec").objectReferenceValue = spec;
            inspectorSo.FindProperty("_lineEnd").objectReferenceValue = sink;
            inspectorSo.ApplyModifiedProperties();

            LineStatusBinder binder = GetOrAdd<LineStatusBinder>(systems.gameObject);
            var binderSo = new SerializedObject(binder);
            binderSo.FindProperty("_faultMonitor").objectReferenceValue = monitor;
            binderSo.FindProperty("_qualityInspector").objectReferenceValue = inspector;
            binderSo.ApplyModifiedProperties();
            log.Add("FaultMonitor, QualityInspector, LineStatusBinder verbunden");

            // ---- Save/Load + debug panel (Woche 3) ----
            SaveLoadController saveLoad = GetOrAdd<SaveLoadController>(systems.gameObject);
            var saveSo = new SerializedObject(saveLoad);
            saveSo.FindProperty("_faultMonitor").objectReferenceValue = monitor;
            saveSo.FindProperty("_qualityInspector").objectReferenceValue = inspector;
            ProductToken productPrefab = FindPortionPrefab();
            saveSo.FindProperty("_productPrefab").objectReferenceValue = productPrefab;
            saveSo.ApplyModifiedProperties();
            log.Add(productPrefab != null
                ? "SaveLoadController angelegt (Produkt-Prefab: " + productPrefab.name + ")"
                : "WARNUNG: SaveLoadController ohne Produkt-Prefab - Produkte werden nicht gespeichert");

            DebugPanel debugPanel = GetOrAdd<DebugPanel>(systems.gameObject);
            var debugSo = new SerializedObject(debugPanel);
            debugSo.FindProperty("_faultMonitor").objectReferenceValue = monitor;
            debugSo.FindProperty("_qualityInspector").objectReferenceValue = inspector;
            debugSo.FindProperty("_saveLoad").objectReferenceValue = saveLoad;
            debugSo.FindProperty("_lineController").objectReferenceValue = Object.FindFirstObjectByType<ConveyorLineController>();
            debugSo.FindProperty("_uiFocusChannel").objectReferenceValue = FindAsset<SO_UiFocusChannel>();
            debugSo.ApplyModifiedProperties();
            log.Add("DebugPanel angelegt (F1 Panel, F5 Speichern, F9 Laden)");

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(systems.gameObject.scene);
            AssetDatabase.SaveAssets();

            string summary = "Fertig. Szene speichern (Strg+S) nicht vergessen.\n\n- " + string.Join("\n- ", log);
            Debug.Log($"[{Title}]\n" + summary, systems);
            SetupUi.Dialog(Title, summary, "OK");
            Selection.activeObject = systems.gameObject;
        }

        /// <summary>
        /// Places the existing 'LineEnd' just behind the belt of the last station of the line, slightly below
        /// the belt surface, so products falling off the belt end are caught. Returns false if 'LineEnd' or a
        /// station is missing.
        /// </summary>
        public static bool MoveLineEndBehindLastStation(List<string> log)
        {
            Transform lineEnd = FindRoot(LineEndName);
            if (lineEnd == null)
            {
                return false;
            }

            ConveyorBelt lastBelt = null;
            string lastStation = null;
            foreach (string stationName in StationsFromEnd)
            {
                Transform root = FindRoot(stationName);
                MachineBase station = root != null ? root.GetComponent<MachineBase>() : null;
                if (station == null)
                {
                    continue;
                }

                SerializedProperty beltProp = new SerializedObject(station).FindProperty("_belt");
                lastBelt = beltProp != null ? beltProp.objectReferenceValue as ConveyorBelt : null;
                if (lastBelt != null)
                {
                    lastStation = stationName;
                    break;
                }
            }

            if (lastBelt == null || !lastBelt.TryGetComponent(out BoxCollider driveBox))
            {
                return false;
            }

            Transform drive = lastBelt.transform;
            float length = driveBox.size.z * Mathf.Abs(drive.lossyScale.z);
            float width = driveBox.size.x * Mathf.Abs(drive.lossyScale.x);

            Undo.RecordObject(lineEnd, "Place LineEnd");
            lineEnd.SetPositionAndRotation(
                drive.position + drive.forward * (length * 0.5f),
                Quaternion.LookRotation(drive.forward, Vector3.up));
            lineEnd.localScale = Vector3.one;

            BoxCollider box = lineEnd.GetComponent<BoxCollider>();
            if (box != null)
            {
                Undo.RecordObject(box, "Size LineEnd");
                box.isTrigger = true;
                // Only below the belt surface: a product is caught after it has fallen off the belt end,
                // never while it is still on the last station's belt (its zone may not be finished yet).
                box.size = new Vector3(width + 0.6f, 1.2f, 1.4f);
                box.center = new Vector3(0f, -0.7f, 0.35f);
            }

            log?.Add($"'LineEnd' hinter '{lastStation}' gesetzt");
            return true;
        }

        /// <summary>The portioner's pizza prefab - every saved product is re-spawned from it.</summary>
        private static ProductToken FindPortionPrefab()
        {
            PortionerMachine portioner = Object.FindFirstObjectByType<PortionerMachine>();
            if (portioner == null)
            {
                return null;
            }

            SerializedProperty prop = new SerializedObject(portioner).FindProperty("_portionPrefab");
            return prop != null ? prop.objectReferenceValue as ProductToken : null;
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

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : Undo.AddComponent<T>(go);
        }

        private static Transform FindRoot(string rootName)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return root.transform;
                }
            }
            return null;
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
        }
    }
}
