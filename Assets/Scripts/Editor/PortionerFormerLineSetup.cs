using System.Collections.Generic;
using System.IO;
using Game.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// One-click setup of the flow system (continuous infeed belt, 4-slot buffer, press feed, outfeed)
    /// for the Portioner -> Former line in Factory_Prototyp.
    ///
    /// Menu: Tools / Food Production / Setup Portioner-Former Line
    ///
    /// What it does (everything is undoable with Ctrl+Z, run it again any time - it updates instead of duplicating):
    ///  - creates config assets in Assets/ScriptableObjects/Conveyors (transport belt + buffer)
    ///  - adds a "BeltDrive" child (trigger volume + ConveyorBelt) on top of each belt's "Cube" surface:
    ///      Portioner belt, conveyor-short (Zulaufband), conveyor-4-slots (Puffer-Band + AccumulationZone),
    ///      Former belt (transfer into / out of the press), conveyor-long (1) Variant (Auslaufband)
    ///  - adds sensors: discharge sensor at the end of conveyor-short, infeed sensor at the press point
    ///  - mirrors the Former's OutputSensor to the downstream side if it points upstream
    ///    (the formed pizza rides on with the belt; the sensor only keeps spacing to the pizza ahead)
    ///  - creates a ConveyorLineController with all elements in material-flow order
    /// Belt sizes and the travel direction are read from the scene (Cube colliders, Portioner -> Former).
    /// </summary>
    public static class PortionerFormerLineSetup
    {
        private const string MenuPath = "Tools/Food Production/Setup Portioner-Former Line";
        private const string ConfigFolder = "Assets/ScriptableObjects/Conveyors";
        private const string TransportConfigPath = ConfigFolder + "/ConveyorConfig_Transport.asset";
        private const string BufferConfigPath = ConfigFolder + "/AccumulationZoneConfig_PressBuffer.asset";

        // Localization: table "Localization Table", key "text.conveyor" (same table as the machines).
        private const string NameTableReference = "GUID:8dde0fd5ff2680540a08d198e1b46953";
        private const long ConveyorNameKeyId = 22459634364428;

        private const string BeltDriveName = "BeltDrive";
        private const float DriveHeight = 0.6f;
        private const float DriveCenterY = 0.1f;
        private const float PressSensorHalfLength = 0.3f;
        private const float BufferSlots = 4;

        private struct BeltGeometry
        {
            public Vector3 Center;      // centre of the belt surface (top of the Cube)
            public float Length;        // along the travel direction
            public float Width;
            public float StartS;        // upstream end, as distance along the travel direction
            public float EndS;          // downstream end
        }

        [MenuItem(MenuPath)]
        public static void Run()
        {
            var log = new List<string>();
            Undo.SetCurrentGroupName("Setup Portioner-Former Line");
            int undoGroup = Undo.GetCurrentGroup();

            // ---- Find scene objects ----
            PortionerMachine portioner = Object.FindFirstObjectByType<PortionerMachine>();
            PressMachine press = FindPress();
            Transform portionerBeltRoot = portioner != null ? FindChildByName(portioner.transform, "conveyor-long") : null;
            Transform pressBeltRoot = press != null ? FindChildByName(press.transform, "conveyor-long") : null;
            Transform infeedRoot = FindRoot("conveyor-short");
            Transform bufferRoot = FindRoot("conveyor-4-slots");
            Transform outfeedRoot = FindRoot("conveyor-long (1) Variant");

            var missing = new List<string>();
            if (portioner == null) missing.Add("PortionerMachine (Portioner)");
            if (press == null) missing.Add("PressMachine (Former)");
            if (portionerBeltRoot == null) missing.Add("Portioner/conveyor-long");
            if (pressBeltRoot == null) missing.Add("Former/conveyor-long");
            if (infeedRoot == null) missing.Add("conveyor-short");
            if (bufferRoot == null) missing.Add("conveyor-4-slots");
            if (outfeedRoot == null) missing.Add("conveyor-long (1) Variant");
            if (missing.Count > 0)
            {
                SetupUi.Dialog("Setup Portioner-Former Line",
                    "Nicht gefunden:\n- " + string.Join("\n- ", missing) + "\n\nNichts wurde geändert.", "OK");
                return;
            }

            Vector3 flow = DominantAxis(press.transform.position - portioner.transform.position);
            log.Add($"Laufrichtung: {flow}");

            // ---- Configs ----
            SO_ConveyorConfig transportConfig = LoadOrCreate<SO_ConveyorConfig>(TransportConfigPath, log);
            SO_AccumulationZoneConfig bufferConfig = LoadOrCreate<SO_AccumulationZoneConfig>(BufferConfigPath, log);

            BeltGeometry bufferGeo = Measure(bufferRoot, flow);
            float slotLength = bufferGeo.Length / BufferSlots;
            var bufferSo = new SerializedObject(bufferConfig);
            bufferSo.FindProperty("_capacity").intValue = (int)BufferSlots;
            bufferSo.FindProperty("_slotLengthMeters").floatValue = slotLength;
            bufferSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bufferConfig);
            log.Add($"Puffer: {BufferSlots} Plätze à {slotLength:0.000} m (Bandlänge {bufferGeo.Length:0.00} m)");

            // ---- Belts ----
            ConveyorBelt portionerBelt = SetupBelt(portionerBeltRoot, "Portionierer-Band", flow, transportConfig, log, out _);
            ConveyorBelt infeedBelt = SetupBelt(infeedRoot, "Zulaufband", flow, transportConfig, log, out BeltGeometry infeedGeo);
            ConveyorBelt bufferBelt = SetupBelt(bufferRoot, "Puffer-Band", flow, transportConfig, log, out _);
            ConveyorBelt pressBelt = SetupBelt(pressBeltRoot, "Pressen-Band", flow, transportConfig, log, out _);
            ConveyorBelt outfeedBelt = SetupBelt(outfeedRoot, "Auslaufband", flow, transportConfig, log, out _);

            // ---- Press: output sensor on the downstream side ----
            var pressSo = new SerializedObject(press);
            Transform pressPoint = pressSo.FindProperty("_pressPoint").objectReferenceValue as Transform;
            var outputSensor = pressSo.FindProperty("_outputSensor").objectReferenceValue as PresenceSensor;
            if (pressPoint == null)
            {
                SetupUi.Dialog("Setup Portioner-Former Line", "Former hat keinen PressPoint zugewiesen.", "OK");
                return;
            }
            if (outputSensor != null)
            {
                MirrorDownstream(outputSensor.transform, pressPoint, flow, log);
            }

            // ---- Sensors ----
            // Infeed sensor around the press point: occupied while the press holds a product or the formed
            // pizza is still leaving. The way from the buffer to the press is tracked by the AccumulationZone
            // itself, so the sensor must NOT reach back to the buffer (the waiting front dough would block it).
            float pressS = Vector3.Dot(pressPoint.position, flow);
            float sensorStart = pressS - PressSensorHalfLength;
            float sensorEnd = pressS + PressSensorHalfLength;
            PresenceSensor pressInfeedSensor = SetupSensor(press.transform, "PressInfeedSensor", "press_infeed",
                flow, sensorStart, sensorEnd, bufferGeo, log);

            // Discharge sensor at the end of the Zulaufband: the belt pauses only once a product waits here.
            PresenceSensor infeedDischarge = SetupSensor(infeedRoot, "DischargeSensor", "infeed_discharge",
                flow, infeedGeo.EndS - 0.3f, infeedGeo.EndS - 0.01f, infeedGeo, log);

            // ---- Accumulation zone on the buffer belt ----
            GameObject bufferDrive = bufferBelt.gameObject;
            AccumulationZone zone = bufferDrive.GetComponent<AccumulationZone>();
            if (zone == null)
            {
                zone = Undo.AddComponent<AccumulationZone>(bufferDrive);
                log.Add("AccumulationZone am Puffer-Band hinzugefügt");
            }
            Transform infeedEdge = GetOrCreateChild(bufferDrive.transform, "InfeedEdge");
            Undo.RecordObject(infeedEdge, "Place InfeedEdge");
            infeedEdge.SetPositionAndRotation(
                AlongFlow(bufferGeo.Center, flow, bufferGeo.StartS), Quaternion.LookRotation(flow, Vector3.up));

            var zoneSo = new SerializedObject(zone);
            zoneSo.FindProperty("_config").objectReferenceValue = bufferConfig;
            zoneSo.FindProperty("_infeedEdge").objectReferenceValue = infeedEdge;
            zoneSo.FindProperty("_downstream").objectReferenceValue = press;
            zoneSo.FindProperty("_downstreamInfeedSensor").objectReferenceValue = pressInfeedSensor;
            zoneSo.ApplyModifiedProperties();

            // ---- Belt wiring (also done at runtime by the line controller) ----
            SetBeltRefs(portionerBelt, infeedBelt, null, null);
            SetBeltRefs(infeedBelt, bufferBelt, null, infeedDischarge);
            SetBeltRefs(bufferBelt, press, zone, null);
            SetBeltRefs(pressBelt, outfeedBelt, null, null);
            SetBeltRefs(outfeedBelt, null, null, null);

            var portionerSo = new SerializedObject(portioner);
            portionerSo.FindProperty("_downstream").objectReferenceValue = portionerBelt;
            portionerSo.ApplyModifiedProperties();

            // ---- Line controller ----
            ConveyorLineController controller = Object.FindFirstObjectByType<ConveyorLineController>();
            if (controller == null)
            {
                var go = new GameObject("LineController Portioner-Former");
                Undo.RegisterCreatedObjectUndo(go, "Create line controller");
                controller = Undo.AddComponent<ConveyorLineController>(go);
                log.Add("ConveyorLineController angelegt");
            }
            var controllerSo = new SerializedObject(controller);
            SerializedProperty elements = controllerSo.FindProperty("_elements");
            var order = new MachineBase[] { portioner, portionerBelt, infeedBelt, bufferBelt, press, pressBelt, outfeedBelt };
            elements.arraySize = order.Length;
            for (int i = 0; i < order.Length; i++)
            {
                elements.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
            }
            controllerSo.FindProperty("_autoWireDownstream").boolValue = true;
            controllerSo.FindProperty("_startOnPlay").boolValue = true;
            controllerSo.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(portioner.gameObject.scene);
            AssetDatabase.SaveAssets();

            string summary = "Fertig. Szene speichern (Strg+S) nicht vergessen.\n\n- " + string.Join("\n- ", log);
            Debug.Log("[Setup Portioner-Former Line]\n" + summary, controller);
            SetupUi.Dialog("Setup Portioner-Former Line", summary, "OK");
            Selection.activeObject = controller.gameObject;
        }

        // ---- Belts ----

        private static ConveyorBelt SetupBelt(Transform beltRoot, string label, Vector3 flow,
            SO_ConveyorConfig config, List<string> log, out BeltGeometry geo)
        {
            geo = Measure(beltRoot, flow);

            Transform drive = GetOrCreateChild(beltRoot, BeltDriveName);
            Undo.RecordObject(drive, "Place BeltDrive");
            drive.SetPositionAndRotation(geo.Center, Quaternion.LookRotation(flow, Vector3.up));
            drive.localScale = Vector3.one;
            drive.gameObject.layer = beltRoot.gameObject.layer;

            BoxCollider box = drive.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(drive.gameObject);
            }
            Undo.RecordObject(box, "Size BeltDrive");
            box.isTrigger = true;
            box.size = new Vector3(geo.Width, DriveHeight, geo.Length);
            box.center = new Vector3(0f, DriveCenterY, 0f);

            ConveyorBelt belt = drive.GetComponent<ConveyorBelt>();
            if (belt == null)
            {
                belt = Undo.AddComponent<ConveyorBelt>(drive.gameObject);
            }

            var so = new SerializedObject(belt);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_beltDirectionReference").objectReferenceValue = drive;
            SerializedProperty nameKey = so.FindProperty("_nameKey");
            nameKey.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = NameTableReference;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = ConveyorNameKeyId;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
            so.ApplyModifiedProperties();

            log.Add($"{label}: {GetPath(drive)} ({geo.Length:0.00} m)");
            return belt;
        }

        private static void SetBeltRefs(ConveyorBelt belt, MachineBase downstream, AccumulationZone zone, PresenceSensor discharge)
        {
            var so = new SerializedObject(belt);
            so.FindProperty("_downstream").objectReferenceValue = downstream;
            so.FindProperty("_accumulationZone").objectReferenceValue = zone;
            so.FindProperty("_dischargeSensor").objectReferenceValue = discharge;
            so.ApplyModifiedProperties();
        }

        // ---- Sensors ----

        private static PresenceSensor SetupSensor(Transform parent, string name, string sensorId, Vector3 flow,
            float startS, float endS, BeltGeometry referenceBelt, List<string> log)
        {
            Transform t = GetOrCreateChild(parent, name);
            float length = Mathf.Max(0.05f, endS - startS);
            Vector3 centre = AlongFlow(referenceBelt.Center, flow, (startS + endS) * 0.5f);

            Undo.RecordObject(t, "Place sensor");
            t.SetPositionAndRotation(centre, Quaternion.LookRotation(flow, Vector3.up));
            t.localScale = Vector3.one;

            BoxCollider box = t.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = Undo.AddComponent<BoxCollider>(t.gameObject);
            }
            Undo.RecordObject(box, "Size sensor");
            box.isTrigger = true;
            box.size = new Vector3(referenceBelt.Width * 0.9f, DriveHeight, length);
            box.center = new Vector3(0f, DriveCenterY, 0f);

            PresenceSensor sensor = t.GetComponent<PresenceSensor>();
            if (sensor == null)
            {
                sensor = Undo.AddComponent<PresenceSensor>(t.gameObject);
            }
            var so = new SerializedObject(sensor);
            so.FindProperty("_sensorId").stringValue = sensorId;
            so.ApplyModifiedProperties();

            log.Add($"Sensor {GetPath(t)} ({length:0.00} m)");
            return sensor;
        }

        private static void MirrorDownstream(Transform target, Transform pressPoint, Vector3 flow, List<string> log)
        {
            if (target == null)
            {
                return;
            }

            Vector3 delta = target.position - pressPoint.position;
            float along = Vector3.Dot(delta, flow);
            if (along >= 0f)
            {
                return; // already downstream
            }

            Undo.RecordObject(target, "Mirror press output");
            target.position -= flow * (2f * along);
            log.Add($"{GetPath(target)} auf die Auslaufseite der Presse gespiegelt");
        }

        // ---- Geometry ----

        private static BeltGeometry Measure(Transform beltRoot, Vector3 flow)
        {
            Transform cube = FindChildByName(beltRoot, "Cube");
            BoxCollider surface = cube != null ? cube.GetComponent<BoxCollider>() : null;
            if (surface == null)
            {
                throw new System.InvalidOperationException($"{GetPath(beltRoot)}: no child 'Cube' with BoxCollider found.");
            }

            Vector3 centre = surface.transform.TransformPoint(surface.center);
            Vector3 size = Vector3.Scale(surface.size, Abs(surface.transform.lossyScale));
            Vector3 side = Vector3.Cross(Vector3.up, flow);

            float length = Mathf.Abs(Vector3.Dot(size, Abs(flow)));
            float width = Mathf.Abs(Vector3.Dot(size, Abs(side)));
            Vector3 top = centre + Vector3.up * (size.y * 0.5f);
            float centreS = Vector3.Dot(top, flow);

            return new BeltGeometry
            {
                Center = top,
                Length = length,
                Width = width,
                StartS = centreS - length * 0.5f,
                EndS = centreS + length * 0.5f
            };
        }

        /// <summary>Point on the belt's centre line at distance <paramref name="s"/> along the flow.</summary>
        private static Vector3 AlongFlow(Vector3 beltCentre, Vector3 flow, float s) =>
            beltCentre + flow * (s - Vector3.Dot(beltCentre, flow));

        private static Vector3 DominantAxis(Vector3 v)
        {
            v.y = 0f;
            return Mathf.Abs(v.x) >= Mathf.Abs(v.z)
                ? new Vector3(Mathf.Sign(v.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(v.z));
        }

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ---- Scene / asset helpers ----

        private static PressMachine FindPress()
        {
            PressMachine[] presses = Object.FindObjectsByType<PressMachine>(FindObjectsSortMode.None);
            foreach (PressMachine p in presses)
            {
                if (p.name == "Former")
                {
                    return p;
                }
            }
            return presses.Length > 0 ? presses[0] : null;
        }

        private static Transform FindRoot(string name)
        {
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root.transform;
                }
            }
            return null;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child != parent && child.name == name)
                {
                    return child;
                }
            }
            return null;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Undo.SetTransformParent(go.transform, parent, "Parent " + name);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static T LoadOrCreate<T>(string path, List<string> log) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                Directory.CreateDirectory(ConfigFolder);
                AssetDatabase.Refresh();
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            log.Add("Config angelegt: " + path);
            return asset;
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
