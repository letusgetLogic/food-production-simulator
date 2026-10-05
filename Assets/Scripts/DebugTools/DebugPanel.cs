using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Persistence;
using Game.Production;
using Game.Quality;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.DebugTools
{
    /// <summary>
    /// Debug and balancing panel (Woche 3), drawn with IMGUI so it needs no scene UI and works in WebGL.
    ///
    /// F1 toggles the panel (takes UI focus: cursor free, player look blocked), F5 quick-saves, F9 quick-loads.
    /// In WebGL builds the browser owns F1 (help) and F5 (reload), so the web keys are used there instead
    /// (default: key left of 1 = ^ on German keyboards, K save, L load).
    /// Sections: time scale, line start/stop/reset, fault injection, dosing refill, active faults,
    /// quality statistics, save/load. Only meant for development and playtests - <see cref="_enabledInBuilds"/>
    /// switches it off in player builds.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private FaultMonitor _faultMonitor;
        [SerializeField] private QualityInspector _qualityInspector;
        [SerializeField] private SaveLoadController _saveLoad;
        [SerializeField] private ConveyorLineController _lineController;

        [Header("Keys")]
        [SerializeField] private Key _toggleKey = Key.F1;
        [SerializeField] private Key _quickSaveKey = Key.F5;
        [SerializeField] private Key _quickLoadKey = Key.F9;

        [Header("Keys in WebGL builds (browser reserves F1/F5)")]
        [SerializeField] private Key _webToggleKey = Key.Backquote;
        [SerializeField] private Key _webQuickSaveKey = Key.K;
        [SerializeField] private Key _webQuickLoadKey = Key.L;

        [Header("Availability")]
        [Tooltip("Allow the panel in player builds (playtests). Off = editor and development builds only.")]
        [SerializeField] private bool _enabledInBuilds = true;

        [Header("Layout")]
        [SerializeField] private float _width = 460f;
        [Range(0.5f, 3f)] [SerializeField] private float _uiScale = 1f;

        private static readonly float[] TimeScales = { 0.5f, 1f, 2f, 4f };

        private readonly List<MachineBase> _machines = new List<MachineBase>();
        private bool _isOpen;
        private Vector2 _scroll;
        private string _statusLine = string.Empty;
        private float _statusUntil;
        private GUIStyle _headerStyle;
        private GUIStyle _smallStyle;

        private bool IsAvailable => Application.isEditor || Debug.isDebugBuild || _enabledInBuilds;

        private static bool IsWeb => Application.platform == RuntimePlatform.WebGLPlayer;
        private Key ToggleKey => IsWeb ? _webToggleKey : _toggleKey;
        private Key QuickSaveKey => IsWeb ? _webQuickSaveKey : _quickSaveKey;
        private Key QuickLoadKey => IsWeb ? _webQuickLoadKey : _quickLoadKey;

        private void Start()
        {
            if (!IsAvailable)
            {
                enabled = false;
                return;
            }

            // Explicit Unity null checks (unassigned serialized fields are "fake null" in the editor).
            ShortcutHints.DebugPanel = KeyLabel(ToggleKey);
            ShortcutHints.QuickSave = KeyLabel(QuickSaveKey);
            ShortcutHints.QuickLoad = KeyLabel(QuickLoadKey);

            if (_faultMonitor == null) _faultMonitor = FindFirstObjectByType<FaultMonitor>();
            if (_qualityInspector == null) _qualityInspector = FindFirstObjectByType<QualityInspector>();
            if (_saveLoad == null) _saveLoad = FindFirstObjectByType<SaveLoadController>();
            if (_lineController == null) _lineController = FindFirstObjectByType<ConveyorLineController>();

            _machines.AddRange(FindObjectsByType<MachineBase>(FindObjectsSortMode.None)
                .Where(m => m.gameObject.activeInHierarchy && !(m is ConveyorBelt))
                .OrderBy(m => m.name));

            if (_saveLoad != null)
            {
                _saveLoad.Saved += (slot, ok, message) => ShowStatus(ok ? "Saved: " + slot : "Save failed: " + message);
                _saveLoad.Loaded += (slot, ok, message) => ShowStatus(ok ? "Loaded: " + message : "Load failed: " + message);
            }

            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.CloseRequested += Close;
            }
        }

        private void OnDestroy()
        {
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.CloseRequested -= Close;
            }

            if (_isOpen && _uiFocusChannel != null)
            {
                _uiFocusChannel.PopFocus();
            }

            Time.timeScale = 1f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard[ToggleKey].wasPressedThisFrame)
            {
                if (_isOpen)
                {
                    Close();
                }
                else
                {
                    Open();
                }
            }

            if (keyboard[QuickSaveKey].wasPressedThisFrame && _saveLoad != null)
            {
                _saveLoad.Save();
            }

            if (keyboard[QuickLoadKey].wasPressedThisFrame && _saveLoad != null && !_saveLoad.Load())
            {
                ShowStatus("Nothing to load");
            }
        }

        /// <summary>Name of the key on the current keyboard layout (Backquote shows as ^ on a German keyboard).</summary>
        private static string KeyLabel(Key key)
        {
            string name = Keyboard.current != null ? Keyboard.current[key].displayName : null;
            return string.IsNullOrEmpty(name) ? key.ToString() : name;
        }

        private void Open()
        {
            _isOpen = true;
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.PushFocus();
            }
        }

        private void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.PopFocus();
            }
        }

        private void ShowStatus(string text)
        {
            _statusLine = text;
            _statusUntil = Time.unscaledTime + 4f;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            if (Time.unscaledTime < _statusUntil)
            {
                GUI.Label(new Rect(10f, Screen.height - 30f, 800f, 24f), _statusLine);
            }

            if (!_isOpen)
            {
                return;
            }

            EnsureStyles();
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_uiScale, _uiScale, 1f));

            float height = Screen.height / _uiScale - 20f;
            GUILayout.BeginArea(new Rect(10f, 10f, _width, height), GUI.skin.box);
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"DEBUG / BALANCING   ({ShortcutHints.DebugPanel} close · {ShortcutHints.QuickSave} save · {ShortcutHints.QuickLoad} load)", _headerStyle);
            DrawTime();
            DrawLine();
            DrawFaults();
            DrawInjection();
            DrawQuality();
            DrawSaveLoad();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void DrawTime()
        {
            Section("Time");
            GUILayout.BeginHorizontal();
            foreach (float scale in TimeScales)
            {
                bool active = Mathf.Approximately(Time.timeScale, scale);
                if (GUILayout.Toggle(active, $"{scale:0.#}x", GUI.skin.button) && !active)
                {
                    Time.timeScale = scale;
                }
            }
            GUILayout.EndHorizontal();
        }

        private void DrawLine()
        {
            Section("Line");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start line"))
            {
                if (_lineController != null)
                {
                    _lineController.StartLine();
                }
                foreach (MachineBase machine in _machines)
                {
                    machine.RequestRun();
                }
            }

            if (GUILayout.Button("Stop line"))
            {
                if (_lineController != null)
                {
                    _lineController.StopLine();
                }
                foreach (MachineBase machine in _machines)
                {
                    machine.StopRun();
                }
            }

            if (GUILayout.Button("Clear all faults"))
            {
                int cleared = 0;
                foreach (MachineBase machine in FindObjectsByType<MachineBase>(FindObjectsSortMode.None))
                {
                    if (machine.CurrentState == MachineState.Fault)
                    {
                        machine.AcknowledgeFault();
                    }

                    if (machine.CurrentState == MachineState.Maintenance)
                    {
                        machine.CompleteMaintenance();
                        cleared++;
                    }
                }
                ShowStatus($"{cleared} machines back to Ready - start the line again");
            }
            GUILayout.EndHorizontal();

            foreach (MachineBase machine in _machines)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{machine.name}", GUILayout.Width(_width * 0.45f));
                GUILayout.Label(machine.HasWarning ? $"{machine.CurrentState} ({machine.WarningReason})" : machine.CurrentState.ToString(), _smallStyle);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFaults()
        {
            Section("Active faults");
            if (_faultMonitor == null)
            {
                GUILayout.Label("No FaultMonitor in scene", _smallStyle);
                return;
            }

            if (_faultMonitor.ActiveFaultCount == 0)
            {
                GUILayout.Label("none", _smallStyle);
            }

            foreach (FaultMonitor.ActiveFault fault in _faultMonitor.ActiveFaults.ToList())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{fault.MachineName}: {fault.Message} ({fault.DurationSeconds:0}s)", _smallStyle);
                if (!fault.IsInMaintenance && GUILayout.Button("Ack", GUILayout.Width(50f)))
                {
                    fault.Machine.AcknowledgeFault();
                }
                else if (fault.IsInMaintenance && GUILayout.Button("Done", GUILayout.Width(50f)))
                {
                    fault.Machine.CompleteMaintenance();
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Label($"Total faults: {_faultMonitor.TotalFaultCount} · downtime {_faultMonitor.TotalDowntimeSeconds:0} s", _smallStyle);
            if (_faultMonitor.CountsByCode.Count > 0)
            {
                GUILayout.Label(string.Join(", ", _faultMonitor.CountsByCode.Select(e => $"{e.Key} {e.Value}")), _smallStyle);
            }
        }

        private void DrawInjection()
        {
            Section("Training faults / refill");
            if (_faultMonitor == null)
            {
                return;
            }

            foreach (MachineBase machine in _machines)
            {
                switch (machine)
                {
                    case ContinuousProcessStation station when station.UsesTemperatureControl:
                        if (GUILayout.Button($"{machine.name}: heater / cooling failure"))
                        {
                            Report(_faultMonitor.InjectFault(machine, ContinuousProcessStation.FaultReasonTemperatureOutOfRange), machine);
                        }
                        break;

                    case DosingMachine dosing:
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("Dosing: sauce empty"))
                        {
                            Report(_faultMonitor.InjectFault(machine, DosingMachine.FaultReasonSauceEmpty), machine);
                        }
                        if (GUILayout.Button("Refill sauce"))
                        {
                            dosing.TryRefill(DosingMedium.Sauce);
                        }
                        if (GUILayout.Button("Refill topping"))
                        {
                            dosing.TryRefill(DosingMedium.Topping);
                        }
                        GUILayout.EndHorizontal();
                        break;

                    case PressMachine:
                        if (GUILayout.Button($"{machine.name}: wrong product"))
                        {
                            Report(_faultMonitor.InjectFault(machine, PressMachine.FaultReasonWrongProduct), machine);
                        }
                        break;
                }
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Spawn pizza base at dosing"))
            {
                SpawnFormedPizzaAtDosing();
            }
            if (GUILayout.Button("Spawn portion at infeed"))
            {
                SpawnPortionAtInfeed();
            }
            GUILayout.EndHorizontal();

            ConveyorBelt firstBelt = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.CurrentState == MachineState.Running);
            if (firstBelt != null && GUILayout.Button($"Jam on a running belt ({firstBelt.transform.root.name})"))
            {
                Report(_faultMonitor.InjectFault(firstBelt, ConveyorBelt.FaultReasonJam), firstBelt);
            }
        }

        // ---- Test products (skip mixer/portioner for testing the downstream line) ----

        private int _testProductCounter;

        /// <summary>Spawns a formed pizza base (as if it came out of the press) at the start of the dosing station's belt.</summary>
        public void SpawnFormedPizzaAtDosing()
        {
            DosingMachine dosing = FindFirstObjectByType<DosingMachine>();
            SpawnOnBelt(dosing != null ? dosing.Belt : null, ProductState.FormedPizza);
        }

        /// <summary>Spawns a dough portion at the start of the belt behind the portioner (tests buffer + press).</summary>
        public void SpawnPortionAtInfeed()
        {
            ConveyorBelt belt = FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.transform.root.name == "conveyor-short");
            SpawnOnBelt(belt, ProductState.PortionedDough);
        }

        private void SpawnOnBelt(ConveyorBelt belt, ProductState state)
        {
            ProductToken prefab = _saveLoad != null ? _saveLoad.ProductPrefab : null;
            if (belt == null || prefab == null)
            {
                ShowStatus("Cannot spawn: belt or product prefab missing");
                return;
            }

            Transform drive = belt.transform;
            float length = drive.TryGetComponent(out BoxCollider box) ? box.size.z * Mathf.Abs(drive.lossyScale.z) : 2f;
            Vector3 position = drive.position - drive.forward * (length * 0.5f - 0.35f) + Vector3.up * 0.15f;

            ProductToken token = Instantiate(prefab, position, drive.rotation);
            _testProductCounter++;
            token.Product = new ProductInstance($"test-{_testProductCounter}", null, state)
            {
                MeasuredWeightGrams = 250f,
                FormedDiameterCm = state >= ProductState.FormedPizza ? 28f : (float?)null,
                FormedThicknessMm = state >= ProductState.FormedPizza ? 3f : (float?)null
            };
            token.name = $"{prefab.name} (test-{_testProductCounter})";
            ShowStatus($"Spawned {state} on {belt.transform.root.name}");
        }

        private void Report(bool injected, MachineBase machine) =>
            ShowStatus(injected ? "Injected on " + machine.name : machine.name + " is already in Fault/Maintenance");

        private void DrawQuality()
        {
            Section("Quality");
            if (_qualityInspector == null)
            {
                GUILayout.Label("No QualityInspector in scene", _smallStyle);
                return;
            }

            GUILayout.Label($"Good {_qualityInspector.GoodCount} · scrap {_qualityInspector.ScrapCount} " +
                            $"({_qualityInspector.ScrapRate01 * 100f:0}%) · {_qualityInspector.ThroughputPerMinute:0.0}/min");
            if (_qualityInspector.DefectCounts.Count > 0)
            {
                GUILayout.Label(string.Join(", ", _qualityInspector.DefectCounts.Select(e => $"{e.Key} {e.Value}")), _smallStyle);
            }

            IReadOnlyList<QualityResult> history = _qualityInspector.History;
            for (int i = history.Count - 1; i >= 0 && i >= history.Count - 5; i--)
            {
                QualityResult result = history[i];
                ProductInstance p = result.Product;
                GUILayout.Label($"{p.InstanceId}: {result.Summary}  " +
                                $"[{Fmt(p.MeasuredWeightGrams, "g")} {Fmt(p.DosedSauceGrams, "g S")} {Fmt(p.DosedToppingGrams, "g T")} " +
                                $"{Fmt(p.ActualBakeTimeSeconds, "s")} {Fmt(p.BakeTemperatureCelsius, "°C")}]", _smallStyle);
            }
        }

        private static string Fmt(float? value, string unit) => value.HasValue ? $"{value.Value:0.#}{unit}" : "-";

        private void DrawSaveLoad()
        {
            Section("Save / Load");
            if (_saveLoad == null)
            {
                GUILayout.Label("No SaveLoadController in scene", _smallStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Save ({ShortcutHints.QuickSave})"))
            {
                _saveLoad.Save();
            }

            GUI.enabled = _saveLoad.HasSave();
            if (GUILayout.Button($"Load ({ShortcutHints.QuickLoad})"))
            {
                _saveLoad.Load();
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label(_saveLoad.Storage.Describe(SaveLoadController.DefaultSlot), _smallStyle);
        }

        private void Section(string title)
        {
            GUILayout.Space(8f);
            GUILayout.Label(title.ToUpperInvariant(), _headerStyle);
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, GUI.skin.label.fontSize - 1), wordWrap = true };
        }
    }
}
