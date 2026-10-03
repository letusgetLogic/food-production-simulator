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
    /// Builds a Durchlaufstation (ContinuousProcessStation) on a new belt directly behind the previous
    /// station of the line: Oven behind the Dosing Station, Cooling behind the Oven, Freezer behind Cooling.
    ///
    /// Menu: Tools / Food Production / Setup Oven | Setup Cooling Tunnel | Setup Shock Freezer | Setup Packaging
    ///
    /// What it does (undoable with Ctrl+Z, run again any time - it updates instead of duplicating):
    ///  - places a new belt (prefab "conveyor-long (1) Variant") seamlessly after the upstream station's belt
    ///    and gives it a BeltDrive (ConveyorBelt, transport config) - the belt is owned by the station
    ///  - creates the station root with ContinuousProcessStation, a ProcessZone over ~80 % of the belt,
    ///    a TemperatureSensor, a placeholder tunnel housing, a MachineTerminal + MachineDetailBinder
    ///  - creates the config asset with the Margherita values on first run (later runs keep your edits)
    ///  - wiring: upstream belt waits for the station (Downstream), station is added to the line controller
    /// </summary>
    public static class TunnelStationSetup
    {
        private sealed class Spec
        {
            public string Title;
            public string StationName;
            public string BeltRootName;
            public string UpstreamStationName;
            public string ConfigPath;
            public long NameKeyId;
            public string SensorId;
            public ProductState InputState;
            public ProductState OutputState;
            public bool RecordBakeTime;
            public bool UsesTemperature = true;
            public float DwellSeconds;
            public float MinDwellSeconds;
            public float MaxDwellSeconds;
            public float TargetTemperature;
            public float MinTargetTemperature;
            public float MaxTargetTemperature;
            public float MinValidTemperature;
            public float MaxValidTemperature;
            public float WarningTolerance;
            public float ThermalRatePerSecond;
            public Color HousingColor;
            public Color GlowColor;
        }

        private const string ConfigFolder = "Assets/ScriptableObjects/MachineConfigs";
        private const string MaterialFolder = "Assets/Materials/Tunnel";
        private const string BeltPrefabPath = "Assets/Prefabs/Conveyors/conveyor-long (1) Variant.prefab";
        private const string TransportConfigPath = "Assets/ScriptableObjects/Conveyors/ConveyorConfig_Transport.asset";
        private const string TerminalPrefabPath = "Assets/Prefabs/Machines/MachineTerminal.prefab";

        private const string NameTableReference = "GUID:8dde0fd5ff2680540a08d198e1b46953";
        private const long ConveyorNameKeyId = 22459634364428; // text.conveyor
        private const long OvenNameKeyId = 22459634364465;     // text.oven
        private const long CoolingNameKeyId = 22459634364430;  // text.cooling (freezer has no own key yet)
        private const long PackagingNameKeyId = 22459634364469; // text.packaging

        private const string BeltDriveName = "BeltDrive";
        private const float DriveHeight = 0.6f;
        private const float DriveCenterY = 0.1f;
        private const float TunnelHeight = 0.7f;
        private const float TerminalHeightAboveFloor = 1.14f;
        private const float HandoverStepMeters = 0.003f;

        private static readonly Spec Oven = new Spec
        {
            Title = "Setup Oven",
            StationName = "Oven",
            BeltRootName = "conveyor-oven",
            UpstreamStationName = "Dosing Station",
            ConfigPath = ConfigFolder + "/OvenConfig.asset",
            NameKeyId = OvenNameKeyId,
            SensorId = "oven_temperature",
            InputState = ProductState.ToppedPizza,
            OutputState = ProductState.BakedPizza,
            RecordBakeTime = true,
            DwellSeconds = 15f, MinDwellSeconds = 5f, MaxDwellSeconds = 60f,
            TargetTemperature = 280f, MinTargetTemperature = 150f, MaxTargetTemperature = 320f,
            MinValidTemperature = 250f, MaxValidTemperature = 320f, WarningTolerance = 10f,
            ThermalRatePerSecond = 40f,
            HousingColor = new Color(0.25f, 0.27f, 0.3f),
            GlowColor = new Color(1f, 0.45f, 0.1f)
        };

        private static readonly Spec Cooling = new Spec
        {
            Title = "Setup Cooling Tunnel",
            StationName = "Cooling Tunnel",
            BeltRootName = "conveyor-cooling",
            UpstreamStationName = "Oven",
            ConfigPath = ConfigFolder + "/CoolingConfig.asset",
            NameKeyId = CoolingNameKeyId,
            SensorId = "cooling_temperature",
            InputState = ProductState.BakedPizza,
            OutputState = ProductState.CooledPizza,
            RecordBakeTime = false,
            DwellSeconds = 10f, MinDwellSeconds = 3f, MaxDwellSeconds = 40f,
            TargetTemperature = 20f, MinTargetTemperature = 5f, MaxTargetTemperature = 30f,
            MinValidTemperature = 0f, MaxValidTemperature = 35f, WarningTolerance = 5f,
            ThermalRatePerSecond = 5f,
            HousingColor = new Color(0.55f, 0.62f, 0.68f),
            GlowColor = new Color(0.45f, 0.75f, 1f)
        };

        private static readonly Spec Freezer = new Spec
        {
            Title = "Setup Shock Freezer",
            StationName = "Shock Freezer",
            BeltRootName = "conveyor-freezer",
            UpstreamStationName = "Cooling Tunnel",
            ConfigPath = ConfigFolder + "/FreezerConfig.asset",
            NameKeyId = CoolingNameKeyId,
            SensorId = "freezer_temperature",
            InputState = ProductState.CooledPizza,
            OutputState = ProductState.FrozenPizza,
            RecordBakeTime = false,
            DwellSeconds = 12f, MinDwellSeconds = 5f, MaxDwellSeconds = 60f,
            TargetTemperature = -18f, MinTargetTemperature = -40f, MaxTargetTemperature = -10f,
            MinValidTemperature = -45f, MaxValidTemperature = -12f, WarningTolerance = 4f,
            ThermalRatePerSecond = 10f,
            HousingColor = new Color(0.82f, 0.88f, 0.92f),
            GlowColor = new Color(0.7f, 0.9f, 1f)
        };

        private static readonly Spec Packaging = new Spec
        {
            Title = "Setup Packaging",
            StationName = "Packaging",
            BeltRootName = "conveyor-packaging",
            UpstreamStationName = "Shock Freezer",
            ConfigPath = ConfigFolder + "/PackagingConfig.asset",
            NameKeyId = PackagingNameKeyId,
            SensorId = string.Empty,
            InputState = ProductState.FrozenPizza,
            OutputState = ProductState.PackagedPizza,
            RecordBakeTime = false,
            UsesTemperature = false,
            DwellSeconds = 8f, MinDwellSeconds = 3f, MaxDwellSeconds = 30f,
            TargetTemperature = 20f, MinTargetTemperature = 0f, MaxTargetTemperature = 40f,
            MinValidTemperature = -50f, MaxValidTemperature = 50f, WarningTolerance = 50f,
            ThermalRatePerSecond = 0f,
            HousingColor = new Color(0.62f, 0.47f, 0.3f),
            GlowColor = new Color(0.95f, 0.9f, 0.75f)
        };

        [MenuItem("Tools/Food Production/Setup Packaging")]
        public static void SetupPackaging() => Run(Packaging);

        [MenuItem("Tools/Food Production/Setup Oven")]
        public static void SetupOven() => Run(Oven);

        [MenuItem("Tools/Food Production/Setup Cooling Tunnel")]
        public static void SetupCooling() => Run(Cooling);

        [MenuItem("Tools/Food Production/Setup Shock Freezer")]
        public static void SetupFreezer() => Run(Freezer);

        private static void Run(Spec spec)
        {
            var log = new List<string>();
            Undo.SetCurrentGroupName(spec.Title);
            int undoGroup = Undo.GetCurrentGroup();

            // ---- Upstream station and its belt ----
            Transform upstreamRoot = FindRoot(spec.UpstreamStationName);
            MachineBase upstream = upstreamRoot != null ? upstreamRoot.GetComponent<MachineBase>() : null;
            ConveyorBelt upstreamBelt = upstream != null ? GetOwnedBelt(upstream) : null;
            if (upstreamBelt == null)
            {
                SetupUi.Dialog(spec.Title,
                    $"Vorgelagerte Station '{spec.UpstreamStationName}' mit eigenem Band nicht gefunden.\n" +
                    "Erst die Station davor einbauen. Nichts wurde geändert.", "OK");
                return;
            }

            Transform upstreamBeltRoot = upstreamBelt.transform.root;
            Vector3 flow = upstreamBelt.transform.forward;
            flow.y = 0f;
            flow.Normalize();
            BeltGeometry upstreamGeo = Measure(upstreamBeltRoot, flow);

            // ---- New belt right behind it ----
            // Every belt sits a few millimetres lower than the one before, so a product at the handover
            // never runs against the edge of the next belt surface. Invisible, see also BeltPhysicsSetup.
            Vector3 beltPosition = upstreamBeltRoot.position + flow * upstreamGeo.Length + Vector3.down * HandoverStepMeters;
            Transform beltRoot = FindRoot(spec.BeltRootName);
            if (beltRoot != null)
            {
                Undo.RecordObject(beltRoot, "Place " + spec.BeltRootName);
                beltRoot.SetPositionAndRotation(beltPosition, upstreamBeltRoot.rotation);
            }
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeltPrefabPath);
                if (prefab == null)
                {
                    SetupUi.Dialog(spec.Title, "Band-Prefab nicht gefunden: " + BeltPrefabPath, "OK");
                    return;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create " + spec.BeltRootName);
                instance.name = spec.BeltRootName;
                instance.transform.SetPositionAndRotation(beltPosition, upstreamBeltRoot.rotation);
                beltRoot = instance.transform;
                log.Add($"Band '{spec.BeltRootName}' hinter '{upstreamBeltRoot.name}' gesetzt");
            }

            BeltPhysicsSetup.ApplyToBelt(beltRoot);
            var transportConfig = AssetDatabase.LoadAssetAtPath<SO_ConveyorConfig>(TransportConfigPath);
            ConveyorBelt belt = SetupBelt(beltRoot, flow, transportConfig, out BeltGeometry geo);
            log.Add($"Band: {geo.Length:0.00} m x {geo.Width:0.00} m");

            // ---- Station root (pivot on the belt surface centre, forward = flow) ----
            Transform station = FindRoot(spec.StationName);
            if (station == null)
            {
                var go = new GameObject(spec.StationName);
                Undo.RegisterCreatedObjectUndo(go, "Create " + spec.StationName);
                station = go.transform;
                log.Add($"'{spec.StationName}' angelegt");
            }
            Undo.RecordObject(station, "Place " + spec.StationName);
            station.SetPositionAndRotation(belt.transform.position, belt.transform.rotation);
            station.localScale = Vector3.one;

            ContinuousProcessStation machine = station.GetComponent<ContinuousProcessStation>();
            if (machine == null)
            {
                machine = Undo.AddComponent<ContinuousProcessStation>(station.gameObject);
            }

            SO_ContinuousProcessConfig config = LoadOrCreateConfig(spec, log);

            // ---- Process zone over ~80 % of the belt ----
            float zoneLength = geo.Length * 0.8f;
            Transform zoneT = GetOrCreateChild(station, "ProcessZone");
            zoneT.localPosition = Vector3.zero;
            zoneT.localRotation = Quaternion.identity;
            BoxCollider zoneBox = zoneT.GetComponent<BoxCollider>();
            if (zoneBox == null)
            {
                zoneBox = Undo.AddComponent<BoxCollider>(zoneT.gameObject);
            }
            Undo.RecordObject(zoneBox, "Size ProcessZone");
            zoneBox.isTrigger = true;
            zoneBox.size = new Vector3(geo.Width * 0.9f, 0.6f, zoneLength);
            zoneBox.center = new Vector3(0f, 0.2f, 0f);
            ProcessZone zone = zoneT.GetComponent<ProcessZone>();
            if (zone == null)
            {
                zone = Undo.AddComponent<ProcessZone>(zoneT.gameObject);
            }

            // ---- Temperature sensor (not for packaging) ----
            TemperatureSensor sensor = null;
            if (spec.UsesTemperature)
            {
                Transform sensorT = GetOrCreateChild(station, "TemperatureSensor");
                sensorT.localPosition = new Vector3(0f, TunnelHeight - 0.1f, 0f);
                sensor = sensorT.GetComponent<TemperatureSensor>();
                bool newSensor = sensor == null;
                if (newSensor)
                {
                    sensor = Undo.AddComponent<TemperatureSensor>(sensorT.gameObject);
                    var sensorSo = new SerializedObject(sensor);
                    sensorSo.FindProperty("_sensorId").stringValue = spec.SensorId;
                    sensorSo.FindProperty("_ambientTemperature").floatValue = 20f;
                    sensorSo.FindProperty("_heatingTargetTemperature").floatValue = spec.TargetTemperature;
                    sensorSo.FindProperty("_thermalRatePerSecond").floatValue = spec.ThermalRatePerSecond;
                    sensorSo.ApplyModifiedProperties();
                }
            }

            // ---- Placeholder tunnel housing ----
            Material housingMat = LoadOrCreateMaterial("M_" + spec.StationName.Replace(" ", "") + "_Housing", spec.HousingColor, log);
            Material glowMat = LoadOrCreateMaterial("M_" + spec.StationName.Replace(" ", "") + "_Glow", spec.GlowColor, log);
            Transform visuals = GetOrCreateChild(station, "Visuals");
            float housingLength = zoneLength + 0.1f;
            float wallX = geo.Width * 0.5f + 0.06f;
            Primitive(visuals, "Wall_L", new Vector3(-wallX, TunnelHeight * 0.5f - 0.05f, 0f),
                new Vector3(0.06f, TunnelHeight + 0.1f, housingLength), housingMat);
            Primitive(visuals, "Wall_R", new Vector3(wallX, TunnelHeight * 0.5f - 0.05f, 0f),
                new Vector3(0.06f, TunnelHeight + 0.1f, housingLength), housingMat);
            Primitive(visuals, "Roof", new Vector3(0f, TunnelHeight + 0.03f, 0f),
                new Vector3(geo.Width + 0.2f, 0.06f, housingLength), housingMat);
            Primitive(visuals, "Glow", new Vector3(0f, TunnelHeight - 0.01f, 0f),
                new Vector3(geo.Width * 0.8f, 0.02f, zoneLength * 0.95f), glowMat);

            // ---- Machine wiring ----
            var machineSo = new SerializedObject(machine);
            machineSo.FindProperty("_config").objectReferenceValue = config;
            machineSo.FindProperty("_belt").objectReferenceValue = belt;
            machineSo.FindProperty("_processZone").objectReferenceValue = zone;
            machineSo.FindProperty("_temperatureSensor").objectReferenceValue = sensor;
            SetNameKey(machineSo.FindProperty("_nameKey"), spec.NameKeyId);
            machineSo.ApplyModifiedProperties();

            // The station's own belt is a free belt.
            var beltSo = new SerializedObject(belt);
            beltSo.FindProperty("_downstream").objectReferenceValue = null;
            beltSo.FindProperty("_accumulationZone").objectReferenceValue = null;
            beltSo.ApplyModifiedProperties();

            // The upstream station's belt waits while this station cannot accept (IInfeedReadiness).
            var upstreamBeltSo = new SerializedObject(upstreamBelt);
            upstreamBeltSo.FindProperty("_downstream").objectReferenceValue = machine;
            upstreamBeltSo.ApplyModifiedProperties();
            log.Add($"Band von '{spec.UpstreamStationName}' wartet auf '{spec.StationName}'");

            SetupTerminal(station, machine, new Vector3(-(geo.Width * 0.5f + 0.5f), 0f, 0f), log);
            AddToLineController(upstream, machine, log);

            // Line end (QualitySystem) moves behind the newest station, if it exists already.
            if (LineSystemsSetup.MoveLineEndBehindLastStation(log))
            {
                log.Add("Linienende hinter die letzte Station verschoben");
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(station.gameObject.scene);
            AssetDatabase.SaveAssets();

            string summary = "Fertig. Szene speichern (Strg+S) nicht vergessen.\n\n- " + string.Join("\n- ", log) +
                             "\n\nPrüfen: Steht das neue Band frei (keine Wand im Weg)?";
            Debug.Log($"[{spec.Title}]\n" + summary, machine);
            SetupUi.Dialog(spec.Title, summary, "OK");
            Selection.activeObject = station.gameObject;
        }

        // ---- Config ----

        private static SO_ContinuousProcessConfig LoadOrCreateConfig(Spec spec, List<string> log)
        {
            var config = AssetDatabase.LoadAssetAtPath<SO_ContinuousProcessConfig>(spec.ConfigPath);
            if (config != null)
            {
                return config;
            }

            EnsureFolder(ConfigFolder);
            config = ScriptableObject.CreateInstance<SO_ContinuousProcessConfig>();
            config.InputState = spec.InputState;
            config.OutputState = spec.OutputState;
            config.RecordDwellAsBakeTime = spec.RecordBakeTime;
            config.DefaultDwellSeconds = spec.DwellSeconds;
            config.MinDwellSeconds = spec.MinDwellSeconds;
            config.MaxDwellSeconds = spec.MaxDwellSeconds;
            config.UsesTemperature = spec.UsesTemperature;
            config.RequireTemperatureBeforeRun = spec.UsesTemperature;
            config.DefaultTargetTemperatureCelsius = spec.TargetTemperature;
            config.MinTargetTemperatureCelsius = spec.MinTargetTemperature;
            config.MaxTargetTemperatureCelsius = spec.MaxTargetTemperature;
            config.MinValidTemperatureCelsius = spec.MinValidTemperature;
            config.MaxValidTemperatureCelsius = spec.MaxValidTemperature;
            config.WarningToleranceCelsius = spec.WarningTolerance;
            AssetDatabase.CreateAsset(config, spec.ConfigPath);
            log.Add("Config angelegt: " + spec.ConfigPath);
            return config;
        }

        // ---- Belt ----

        private struct BeltGeometry
        {
            public Vector3 Center;
            public float Length;
            public float Width;
        }

        private static ConveyorBelt SetupBelt(Transform beltRoot, Vector3 flow, SO_ConveyorConfig config, out BeltGeometry geo)
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
            SetNameKey(so.FindProperty("_nameKey"), ConveyorNameKeyId);
            so.ApplyModifiedProperties();
            return belt;
        }

        private static BeltGeometry Measure(Transform beltRoot, Vector3 flow)
        {
            BoxCollider surface = null;
            foreach (Transform child in beltRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child != beltRoot && child.name == "Cube" && child.TryGetComponent(out surface))
                {
                    break;
                }
            }

            if (surface == null)
            {
                throw new System.InvalidOperationException($"{beltRoot.name}: no child 'Cube' with BoxCollider found.");
            }

            Vector3 centre = surface.transform.TransformPoint(surface.center);
            Vector3 size = Vector3.Scale(surface.size, Abs(surface.transform.lossyScale));
            Vector3 side = Vector3.Cross(Vector3.up, flow);

            return new BeltGeometry
            {
                Center = centre + Vector3.up * (size.y * 0.5f),
                Length = Mathf.Abs(Vector3.Dot(size, Abs(flow))),
                Width = Mathf.Abs(Vector3.Dot(size, Abs(side)))
            };
        }

        private static ConveyorBelt GetOwnedBelt(MachineBase station)
        {
            var so = new SerializedObject(station);
            SerializedProperty prop = so.FindProperty("_belt");
            return prop != null ? prop.objectReferenceValue as ConveyorBelt : null;
        }

        // ---- Terminal / line ----

        private static void SetupTerminal(Transform station, MachineBase machine, Vector3 localPosition, List<string> log)
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
                instance.transform.localPosition = localPosition +
                    new Vector3(0f, TerminalHeightAboveFloor - station.position.y, 0f);
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

        private static void AddToLineController(MachineBase upstream, MachineBase machine, List<string> log)
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
            int upstreamIndex = list.IndexOf(upstream);
            list.Insert(upstreamIndex >= 0 ? upstreamIndex + 1 : list.Count, machine);

            elements.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                elements.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            }
            so.ApplyModifiedProperties();
            log.Add("Linien-Controller: Station nach '" + upstream.name + "' eingereiht");
        }

        // ---- Helpers ----

        private static void SetNameKey(SerializedProperty nameKey, long keyId)
        {
            nameKey.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = NameTableReference;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = keyId;
            nameKey.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
        }

        private static void Primitive(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, Material mat)
        {
            Transform existing = parent.Find(objectName);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = objectName;
                Undo.RegisterCreatedObjectUndo(go, "Create " + objectName);
                Undo.SetTransformParent(go.transform, parent, "Parent " + objectName);
                if (go.TryGetComponent(out Collider collider))
                {
                    Object.DestroyImmediate(collider);
                }
            }

            Undo.RecordObject(go.transform, "Place " + objectName);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            if (go.TryGetComponent(out MeshRenderer renderer))
            {
                Undo.RecordObject(renderer, "Material " + objectName);
                renderer.sharedMaterial = mat;
            }
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

        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
