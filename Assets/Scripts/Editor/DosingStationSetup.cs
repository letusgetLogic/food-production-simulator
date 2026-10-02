using System.Collections.Generic;
using System.IO;
using Game.HMI;
using Game.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the Dosierstation (sauce + topping) on the outfeed belt behind the press.
    ///
    /// Menu: Tools / Food Production / Setup Dosing Station
    ///
    /// What it does (undoable with Ctrl+Z, run again any time - it updates instead of duplicating):
    ///  - creates the config asset Assets/ScriptableObjects/MachineConfigs/DosingConfig.asset
    ///  - creates "Dosing Station" (DosingMachine) on the outfeed belt "conveyor-long (1) Variant";
    ///    the belt becomes the station's own belt (started/stopped with the station)
    ///  - SauceZone / ToppingZone (ProcessZone triggers) along the belt, with placeholder visuals
    ///    (gantry, sauce nozzle, topping hopper) - replace them with real models later
    ///  - two refill buttons (layer Interactable) and a MachineTerminal (prefab) + MachineDetailBinder
    ///  - ConveyorLineController: the outfeed belt entry is replaced by the station, so the press belt
    ///    waits while the station is not running (IInfeedReadiness)
    /// </summary>
    public static class DosingStationSetup
    {
        private const string MenuPath = "Tools/Food Production/Setup Dosing Station";

        private const string OutfeedRootName = "conveyor-long (1) Variant";
        private const string StationName = "Dosing Station";
        private const string ConfigFolder = "Assets/ScriptableObjects/MachineConfigs";
        private const string ConfigPath = ConfigFolder + "/DosingConfig.asset";
        private const string MaterialFolder = "Assets/Materials/Dosing";
        private const string TerminalPrefabPath = "Assets/Prefabs/Machines/MachineTerminal.prefab";

        // Localization: table "Localization Table", key "text.topping" (same table as the machines).
        private const string NameTableReference = "GUID:8dde0fd5ff2680540a08d198e1b46953";
        private const long ToppingNameKeyId = 22459634364508;

        private const int InteractableLayer = 6;
        private const float ZoneLength = 0.25f;
        private const float ZoneHeight = 0.6f;
        private const float TerminalHeightAboveFloor = 1.14f;

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var log = new List<string>();
            Undo.SetCurrentGroupName("Setup Dosing Station");
            int undoGroup = Undo.GetCurrentGroup();

            // ---- Outfeed belt ----
            Transform outfeedRoot = FindRoot(OutfeedRootName);
            ConveyorBelt belt = outfeedRoot != null ? outfeedRoot.GetComponentInChildren<ConveyorBelt>(true) : null;
            if (belt == null)
            {
                EditorUtility.DisplayDialog("Setup Dosing Station",
                    $"Auslaufband '{OutfeedRootName}' mit ConveyorBelt (BeltDrive) nicht gefunden.\n" +
                    "Erst 'Setup Portioner-Former Line' ausführen. Nichts wurde geändert.", "OK");
                return;
            }

            var beltBox = belt.GetComponent<BoxCollider>();
            Transform drive = belt.transform;
            float beltLength = beltBox.size.z * Mathf.Abs(drive.lossyScale.z);
            float beltWidth = beltBox.size.x * Mathf.Abs(drive.lossyScale.x);
            log.Add($"Auslaufband: {beltLength:0.00} m x {beltWidth:0.00} m");

            // ---- Config + materials ----
            SO_DosingConfig config = LoadOrCreate<SO_DosingConfig>(ConfigFolder, ConfigPath, log);
            Material frameMat = LoadOrCreateMaterial("M_DosingFrame", new Color(0.55f, 0.58f, 0.62f), log);
            Material sauceMat = LoadOrCreateMaterial("M_DosingSauce", new Color(0.75f, 0.12f, 0.08f), log);
            Material toppingMat = LoadOrCreateMaterial("M_DosingTopping", new Color(0.95f, 0.78f, 0.25f), log);

            // ---- Station root (pivot on the belt surface centre, forward = travel direction) ----
            Transform station = FindRoot(StationName);
            if (station == null)
            {
                var go = new GameObject(StationName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + StationName);
                station = go.transform;
                log.Add("'Dosing Station' angelegt");
            }
            Undo.RecordObject(station, "Place " + StationName);
            station.SetPositionAndRotation(drive.position, drive.rotation);
            station.localScale = Vector3.one;

            DosingMachine machine = station.GetComponent<DosingMachine>();
            if (machine == null)
            {
                machine = Undo.AddComponent<DosingMachine>(station.gameObject);
            }

            // ---- Zones ----
            float sauceZ = -beltLength * 0.15f;
            float toppingZ = beltLength * 0.2f;
            ProcessZone sauceZone = SetupZone(station, "SauceZone", sauceZ, beltWidth);
            ProcessZone toppingZone = SetupZone(station, "ToppingZone", toppingZ, beltWidth);

            // ---- Placeholder visuals ----
            float gantryHeight = 0.75f;
            Transform visuals = GetOrCreateChild(station, "Visuals");
            BuildGantry(visuals, "Gantry_Sauce", sauceZ, beltWidth, gantryHeight, frameMat);
            BuildGantry(visuals, "Gantry_Topping", toppingZ, beltWidth, gantryHeight, frameMat);
            Primitive(visuals, "SauceNozzle", PrimitiveType.Cylinder, new Vector3(0f, gantryHeight - 0.15f, sauceZ),
                new Vector3(0.12f, 0.12f, 0.12f), sauceMat);
            Primitive(visuals, "SauceTank", PrimitiveType.Cylinder, new Vector3(0f, gantryHeight + 0.25f, sauceZ),
                new Vector3(0.35f, 0.2f, 0.35f), sauceMat);
            Primitive(visuals, "ToppingHopper", PrimitiveType.Cube, new Vector3(0f, gantryHeight + 0.15f, toppingZ),
                new Vector3(beltWidth * 0.8f, 0.3f, 0.3f), toppingMat);
            Primitive(visuals, "ToppingOutlet", PrimitiveType.Cube, new Vector3(0f, gantryHeight - 0.1f, toppingZ),
                new Vector3(beltWidth * 0.7f, 0.06f, 0.12f), toppingMat);

            // ---- Refill buttons (side +X of the belt) ----
            float side = beltWidth * 0.5f + 0.2f;
            SetupRefillButton(station, machine, "Button_RefillSauce", DosingMedium.Sauce,
                new Vector3(side, 0.35f, sauceZ), sauceMat);
            SetupRefillButton(station, machine, "Button_RefillTopping", DosingMedium.Topping,
                new Vector3(side, 0.35f, toppingZ), toppingMat);

            // ---- Machine wiring ----
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("_config").objectReferenceValue = config;
            machineSo.FindProperty("_belt").objectReferenceValue = belt;
            machineSo.FindProperty("_sauceZone").objectReferenceValue = sauceZone;
            machineSo.FindProperty("_toppingZone").objectReferenceValue = toppingZone;
            SerializedProperty nameKey = machineSo.FindProperty("_nameKey");
            nameKey.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = NameTableReference;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = ToppingNameKeyId;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
            machineSo.ApplyModifiedProperties();

            // The station's belt is a free belt: no downstream, no buffer.
            var beltSo = new SerializedObject(belt);
            beltSo.FindProperty("_downstream").objectReferenceValue = null;
            beltSo.FindProperty("_accumulationZone").objectReferenceValue = null;
            beltSo.ApplyModifiedProperties();

            // ---- Terminal (side -X of the belt, facing the operator aisle) ----
            SetupTerminal(station, machine, new Vector3(-(beltWidth * 0.5f + 0.5f), 0f, 0f), log);

            // ---- Line controller ----
            WireLineController(belt, machine, log);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            AssetDatabase.SaveAssets();

            string summary = "Fertig. Szene speichern (Strg+S) nicht vergessen.\n\n- " + string.Join("\n- ", log) +
                             "\n\nTerminal und Knöpfe sind grob platziert - bei Bedarf im Scene View nachschieben.";
            Debug.Log("[Setup Dosing Station]\n" + summary, machine);
            EditorUtility.DisplayDialog("Setup Dosing Station", summary, "OK");
            Selection.activeObject = station.gameObject;
        }

        // ---- Parts ----

        private static ProcessZone SetupZone(Transform station, string zoneName, float z, float beltWidth)
        {
            Transform t = GetOrCreateChild(station, zoneName);
            Undo.RecordObject(t, "Place " + zoneName);
            t.localPosition = new Vector3(0f, 0f, z);
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            BoxCollider box = t.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(t.gameObject);
            }
            Undo.RecordObject(box, "Size " + zoneName);
            box.isTrigger = true;
            box.size = new Vector3(beltWidth * 0.9f, ZoneHeight, ZoneLength);
            box.center = new Vector3(0f, ZoneHeight * 0.5f - 0.1f, 0f);

            ProcessZone zone = t.GetComponent<ProcessZone>();
            if (zone == null)
            {
                zone = Undo.AddComponent<ProcessZone>(t.gameObject);
            }
            return zone;
        }

        private static void BuildGantry(Transform parent, string gantryName, float z, float beltWidth, float height, Material mat)
        {
            Transform gantry = GetOrCreateChild(parent, gantryName);
            gantry.localPosition = new Vector3(0f, 0f, z);
            gantry.localRotation = Quaternion.identity;

            float legX = beltWidth * 0.5f + 0.08f;
            Primitive(gantry, "Beam", PrimitiveType.Cube, new Vector3(0f, height, 0f),
                new Vector3(beltWidth + 0.3f, 0.06f, 0.08f), mat);
            Primitive(gantry, "Leg_L", PrimitiveType.Cube, new Vector3(-legX, height * 0.5f - 0.3f, 0f),
                new Vector3(0.06f, height + 0.6f, 0.06f), mat);
            Primitive(gantry, "Leg_R", PrimitiveType.Cube, new Vector3(legX, height * 0.5f - 0.3f, 0f),
                new Vector3(0.06f, height + 0.6f, 0.06f), mat);
        }

        private static void SetupRefillButton(Transform station, DosingMachine machine, string buttonName,
            DosingMedium medium, Vector3 localPosition, Material mat)
        {
            Transform t = Primitive(station, buttonName, PrimitiveType.Cube, localPosition,
                new Vector3(0.15f, 0.15f, 0.15f), mat, keepCollider: true);
            t.gameObject.layer = InteractableLayer;

            DosingRefillButton button = t.GetComponent<DosingRefillButton>();
            if (button == null)
            {
                button = Undo.AddComponent<DosingRefillButton>(t.gameObject);
            }

            var so = new SerializedObject(button);
            so.FindProperty("_machine").objectReferenceValue = machine;
            so.FindProperty("_medium").enumValueIndex = (int)medium;
            so.ApplyModifiedProperties();
        }

        private static void SetupTerminal(Transform station, DosingMachine machine, Vector3 localPosition, List<string> log)
        {
            HmiTerminalInteractable terminal = station.GetComponentInChildren<HmiTerminalInteractable>(true);
            if (terminal == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TerminalPrefabPath);
                if (prefab == null)
                {
                    log.Add("MachineTerminal-Prefab nicht gefunden - Terminal fehlt");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, station);
                Undo.RegisterCreatedObjectUndo(instance, "Create MachineTerminal");
                instance.name = "MachineTerminal";
                // Height/tilt like the terminals on Portioner and Press, turned towards the aisle (-X).
                // The station pivot sits on the belt surface; the other terminals are 1.14 m above the floor (y = 0).
                instance.transform.localPosition = localPosition + new Vector3(0f, TerminalHeightAboveFloor - station.position.y, 0f);
                instance.transform.localRotation = Quaternion.Euler(15f, -90f, 0f);
                terminal = instance.GetComponent<HmiTerminalInteractable>();
                log.Add("MachineTerminal angelegt");
            }

            var terminalSo = new SerializedObject(terminal);
            terminalSo.FindProperty("_machine").objectReferenceValue = machine;
            terminalSo.FindProperty("_panelId").stringValue = "machine";
            terminalSo.ApplyModifiedProperties();

            MachineDetailPanel ambientPanel = terminal.GetComponent<MachineDetailPanel>();
            if (ambientPanel == null)
            {
                return;
            }

            MachineDetailBinder binder = machine.GetComponent<MachineDetailBinder>();
            if (binder == null)
            {
                binder = Undo.AddComponent<MachineDetailBinder>(machine.gameObject);
            }

            var binderSo = new SerializedObject(binder);
            binderSo.FindProperty("_detailPanel").objectReferenceValue = ambientPanel;
            binderSo.FindProperty("_machineInstance").objectReferenceValue = machine;
            binderSo.ApplyModifiedProperties();
        }

        private static void WireLineController(ConveyorBelt belt, DosingMachine machine, List<string> log)
        {
            ConveyorLineController controller = Object.FindFirstObjectByType<ConveyorLineController>();
            if (controller == null)
            {
                log.Add("Kein ConveyorLineController - Station muss am Terminal gestartet werden");
                return;
            }

            var so = new SerializedObject(controller);
            SerializedProperty elements = so.FindProperty("_elements");

            var list = new List<Object>();
            for (int i = 0; i < elements.arraySize; i++)
            {
                list.Add(elements.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            list.Remove(machine);
            int beltIndex = list.IndexOf(belt);
            if (beltIndex >= 0)
            {
                list[beltIndex] = machine; // belt is owned by the station now
                log.Add("Linien-Controller: Auslaufband durch Dosierstation ersetzt");
            }
            else
            {
                list.Add(machine);
                log.Add("Linien-Controller: Dosierstation angehängt");
            }

            elements.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                elements.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedProperties();

            // Same wiring the controller does at runtime: the element before the station waits for it.
            int stationIndex = list.IndexOf(machine);
            if (stationIndex > 0 && list[stationIndex - 1] is ConveyorBelt upstreamBelt)
            {
                var upstreamSo = new SerializedObject(upstreamBelt);
                upstreamSo.FindProperty("_downstream").objectReferenceValue = machine;
                upstreamSo.ApplyModifiedProperties();
            }
        }

        // ---- Helpers ----

        private static Transform Primitive(Transform parent, string objectName, PrimitiveType type, Vector3 localPosition,
            Vector3 localScale, Material mat, bool keepCollider = false)
        {
            Transform existing = parent.Find(objectName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = objectName;
                Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
                Undo.SetTransformParent(go.transform, parent, "Parent " + objectName);

                if (!keepCollider && go.TryGetComponent(out Collider collider))
                {
                    Object.DestroyImmediate(collider);
                }
            }

            Undo.RecordObject(go.transform, "Place " + objectName);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (mat != null && go.TryGetComponent(out MeshRenderer renderer))
            {
                Undo.RecordObject(renderer, "Material " + objectName);
                renderer.sharedMaterial = mat;
            }

            return go.transform;
        }

        private static Material LoadOrCreateMaterial(string materialName, Color color, List<string> log)
        {
            string path = MaterialFolder + "/" + materialName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            EnsureFolder(MaterialFolder);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            mat.color = color;
            AssetDatabase.CreateAsset(mat, path);
            log.Add("Material angelegt: " + path);
            return mat;
        }

        private static T LoadOrCreate<T>(string folder, string path, List<string> log) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            EnsureFolder(folder);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.Add("Config angelegt: " + path);
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }
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

        private static Transform GetOrCreateChild(Transform parent, string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
            Undo.SetTransformParent(go.transform, parent, "Parent " + childName);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }
    }
}
