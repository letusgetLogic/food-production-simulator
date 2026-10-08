using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// FaultSystem (Woche 2): one instance per scene. Watches every <see cref="MachineBase"/>, keeps the
    /// list of active faults (Fault and Maintenance) with their <see cref="FaultDefinition"/>, counts faults
    /// and downtime for the HMI statistics, and can inject training faults.
    ///
    /// Machines detect and raise their faults themselves (sensors deliver raw values, machines judge) -
    /// this monitor never decides whether something is a fault. It only collects, describes and injects.
    ///
    /// Rules (PM decision 02.10.):
    ///  - The way back is always Fault -> AcknowledgeFault -> Maintenance -> CompleteMaintenance -> Ready -> Start
    ///    (operator at the HMI), never automatic.
    ///  - Product held in a fault: a press stroke in progress is released as scrap (RejectReason
    ///    PressInterrupted); products inside a tunnel stay inside (dwell time grows, QualitySystem judges);
    ///    products on stopped belts simply wait.
    ///  - Belts are listed here like any other machine; the HMI decides how to show them.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaultMonitor : MonoBehaviour
    {
        /// <summary>A fault that has not been cleared yet (machine in Fault or Maintenance).</summary>
        public sealed class ActiveFault
        {
            public MachineBase Machine;
            public string Reason;
            public FaultDefinition Definition;
            public float StartTime;
            public bool IsInMaintenance;

            public string MachineName => DisplayName(Machine);

            public string Message => Definition != null ? Definition.Message : Reason;
            public string Remedy => Definition != null ? Definition.Remedy : string.Empty;
            public float DurationSeconds => Time.time - StartTime;
        }

        [SerializeField] private SO_FaultCatalog _catalog;

        [Header("Training fault injection")]
        [Tooltip("Inject a random injectable training fault every X seconds while the line runs (0 = off).")]
        [Min(0f)] [SerializeField] private float _autoInjectIntervalSeconds;

        [Tooltip("Stations that may receive injected faults. Empty = every ContinuousProcessStation with temperature.")]
        [SerializeField] private List<MachineBase> _injectionTargets = new List<MachineBase>();

        private readonly List<MachineBase> _machines = new List<MachineBase>();
        private readonly Dictionary<MachineBase, ActiveFault> _active = new Dictionary<MachineBase, ActiveFault>();
        private readonly Dictionary<MachineBase, Action<MachineState, MachineState>> _handlers =
            new Dictionary<MachineBase, Action<MachineState, MachineState>>();
        private readonly Dictionary<string, int> _countsByCode = new Dictionary<string, int>();
        private readonly List<ActiveFault> _activeList = new List<ActiveFault>();

        private float _autoInjectTimer;
        private float _accumulatedDowntimeSeconds;
        private float _lineDownSeconds;
        private readonly Dictionary<string, float> _downtimeByMachine = new Dictionary<string, float>();
        private readonly Dictionary<string, MachineBase> _machinesById = new Dictionary<string, MachineBase>();

        /// <summary>
        /// Seconds during which at least one machine was in Fault/Maintenance (line availability = 1 - this / run time).
        /// Unlike <see cref="TotalDowntimeSeconds"/>, overlapping faults are counted once.
        /// </summary>
        public float LineDownSeconds => _lineDownSeconds;

        /// <summary>
        /// Cleared downtime per machine id (<see cref="MachineId"/>, running faults not included).
        /// Keyed by id, not by display name, so a language switch does not split one machine into two entries -
        /// resolve the name for display with <see cref="DisplayNameOf"/>.
        /// </summary>
        public IReadOnlyDictionary<string, float> DowntimeByMachine => _downtimeByMachine;

        /// <summary>Raised whenever a fault starts, moves to maintenance or is cleared.</summary>
        public event Action FaultsChanged;

        /// <summary>Raised when a machine enters Fault: (machine, definition or null).</summary>
        public event Action<MachineBase, FaultDefinition> FaultRaised;

        /// <summary>Raised when a machine leaves Maintenance (fault fully cleared): (machine, seconds the fault lasted).</summary>
        public event Action<MachineBase, float> FaultCleared;

        /// <summary>Active faults, oldest first.</summary>
        public IReadOnlyList<ActiveFault> ActiveFaults => _activeList;

        public int ActiveFaultCount => _activeList.Count;
        public int TotalFaultCount { get; private set; }
        public IReadOnlyDictionary<string, int> CountsByCode => _countsByCode;
        public SO_FaultCatalog Catalog => _catalog;

        /// <summary>Downtime of all cleared faults plus the running ones, in seconds.</summary>
        public float TotalDowntimeSeconds
        {
            get
            {
                float running = 0f;
                foreach (ActiveFault fault in _activeList)
                {
                    running += fault.DurationSeconds;
                }
                return _accumulatedDowntimeSeconds + running;
            }
        }

        // ---- Lifecycle ----

        private void Start()
        {
            // Start (not Awake): MachineOverviewReporter numbers the machines in its Awake.
            foreach (MachineBase machine in FindObjectsByType<MachineBase>(FindObjectsSortMode.None))
            {
                if (!machine.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Action<MachineState, MachineState> handler = (previous, next) => HandleStateChanged(machine, previous, next);
                machine.StateChanged += handler;
                _handlers.Add(machine, handler);
                _machines.Add(machine);
                _machinesById[MachineId(machine)] = machine;

                if (machine.CurrentState == MachineState.Fault || machine.CurrentState == MachineState.Maintenance)
                {
                    HandleStateChanged(machine, MachineState.Ready, machine.CurrentState);
                }
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<MachineBase, Action<MachineState, MachineState>> entry in _handlers)
            {
                if (entry.Key != null)
                {
                    entry.Key.StateChanged -= entry.Value;
                }
            }
            _handlers.Clear();
        }

        private void Update()
        {
            if (_activeList.Count > 0)
            {
                _lineDownSeconds += Time.deltaTime;
            }

            if (_autoInjectIntervalSeconds <= 0f)
            {
                return;
            }

            _autoInjectTimer += Time.deltaTime;
            if (_autoInjectTimer >= _autoInjectIntervalSeconds)
            {
                _autoInjectTimer = 0f;
                InjectRandomTrainingFault();
            }
        }

        // ---- Queries ----

        /// <summary>
        /// Language-independent machine id: the hierarchy path (same identity the save system uses -
        /// machine GameObjects must not be renamed).
        /// </summary>
        public static string MachineId(MachineBase machine)
        {
            if (machine == null)
            {
                return "?";
            }

            string path = machine.name;
            for (Transform parent = machine.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }

        /// <summary>Localized display name with number, e.g. "Conveyor 3".</summary>
        public static string DisplayName(MachineBase machine) => machine == null ? "?" : machine.DisplayName;

        /// <summary>Current display name of a machine id; unknown ids (machine removed) are shown as they are.</summary>
        public string DisplayNameOf(string machineId) =>
            _machinesById.TryGetValue(machineId, out MachineBase machine) && machine != null ? DisplayName(machine) : machineId;

        public FaultDefinition Describe(string reason) => _catalog != null ? _catalog.Find(reason) : null;

        public ActiveFault GetActiveFault(MachineBase machine) =>
            machine != null && _active.TryGetValue(machine, out ActiveFault fault) ? fault : null;

        // ---- Save/Load ----

        /// <summary>Statistics for the save file (active faults are restored through the machines themselves).</summary>
        public void CaptureStatistics(SaveValues values)
        {
            values.Set("total", TotalFaultCount);
            values.Set("downtime", _accumulatedDowntimeSeconds);
            values.Set("lineDown", _lineDownSeconds);
            foreach (KeyValuePair<string, float> entry in _downtimeByMachine)
            {
                values.Set("machine." + entry.Key, entry.Value);
            }
            foreach (KeyValuePair<string, int> entry in _countsByCode)
            {
                values.Set("code." + entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Restores the statistics. Call after the saved faults were re-raised: those raised counts are
        /// replaced by the saved totals, so nothing is counted twice.
        /// </summary>
        public void RestoreStatistics(SaveValues values)
        {
            TotalFaultCount = values.GetInt("total", TotalFaultCount);
            _accumulatedDowntimeSeconds = values.GetFloat("downtime", _accumulatedDowntimeSeconds);
            _lineDownSeconds = values.GetFloat("lineDown", _lineDownSeconds);
            _countsByCode.Clear();
            _downtimeByMachine.Clear();
            for (int i = 0; i < values.Keys.Count; i++)
            {
                string key = values.Keys[i];
                if (key.StartsWith("code.", StringComparison.Ordinal))
                {
                    _countsByCode[key.Substring(5)] = (int)Math.Round(values.Values[i]);
                }
                else if (key.StartsWith("machine.", StringComparison.Ordinal))
                {
                    string id = ResolveSavedMachineKey(key.Substring(8));
                    _downtimeByMachine.TryGetValue(id, out float seconds);
                    _downtimeByMachine[id] = seconds + values.Values[i];
                }
            }
            FaultsChanged?.Invoke();
        }

        /// <summary>
        /// Saves before 05.10. keyed the downtime by display name - map such keys to the machine id
        /// (works when the save is loaded in the same language it was written in).
        /// </summary>
        private string ResolveSavedMachineKey(string key)
        {
            if (_machinesById.ContainsKey(key))
            {
                return key;
            }

            foreach (KeyValuePair<string, MachineBase> entry in _machinesById)
            {
                if (entry.Value != null && DisplayName(entry.Value) == key)
                {
                    return entry.Key;
                }
            }
            return key;
        }

        // ---- Injection (training / debug) ----

        /// <summary>
        /// Injects a training fault. "TemperatureOutOfRange" on a tunnel station simulates a heater failure
        /// (the fault then develops physically); every other code is raised directly on the machine.
        /// </summary>
        public bool InjectFault(MachineBase machine, string code)
        {
            if (machine == null || machine.CurrentState == MachineState.Fault
                || machine.CurrentState == MachineState.Maintenance)
            {
                return false;
            }

            if (code == ContinuousProcessStation.FaultReasonTemperatureOutOfRange
                && machine is ContinuousProcessStation station)
            {
                station.SimulateHeaterFailure();
                Debug.Log($"[FaultMonitor] Injected heater failure on {machine.name}.", machine);
                return true;
            }

            machine.TriggerFault(code);
            Debug.Log($"[FaultMonitor] Injected fault '{code}' on {machine.name}.", machine);
            return true;
        }

        /// <summary>Injects a heater failure on a random running target station.</summary>
        [ContextMenu("Inject random training fault")]
        public void InjectRandomTrainingFault()
        {
            var candidates = new List<MachineBase>();
            IEnumerable<MachineBase> pool = _injectionTargets.Count > 0 ? _injectionTargets : _machines;
            foreach (MachineBase machine in pool)
            {
                if (machine is ContinuousProcessStation station && machine.CurrentState == MachineState.Running
                    && station.UsesTemperatureControl && !station.HasHeaterFailure)
                {
                    candidates.Add(machine);
                }
            }

            if (candidates.Count == 0)
            {
                return;
            }

            MachineBase target = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            InjectFault(target, ContinuousProcessStation.FaultReasonTemperatureOutOfRange);
        }

        // ---- State tracking ----

        private void HandleStateChanged(MachineBase machine, MachineState previous, MachineState next)
        {
            switch (next)
            {
                case MachineState.Fault:
                {
                    string reason = machine.FaultReason;
                    FaultDefinition definition = Describe(reason);
                    _active[machine] = new ActiveFault
                    {
                        Machine = machine,
                        Reason = reason,
                        Definition = definition,
                        StartTime = Time.time
                    };

                    TotalFaultCount++;
                    string code = definition != null ? definition.Code : reason ?? "Unknown";
                    _countsByCode.TryGetValue(code, out int count);
                    _countsByCode[code] = count + 1;

                    RebuildList();
                    FaultRaised?.Invoke(machine, definition);
                    FaultsChanged?.Invoke();
                    break;
                }

                case MachineState.Maintenance:
                    if (_active.TryGetValue(machine, out ActiveFault inMaintenance))
                    {
                        inMaintenance.IsInMaintenance = true;
                        FaultsChanged?.Invoke();
                    }
                    break;

                default:
                    if (_active.TryGetValue(machine, out ActiveFault cleared))
                    {
                        float duration = cleared.DurationSeconds;
                        _accumulatedDowntimeSeconds += duration;
                        string id = MachineId(machine);
                        _downtimeByMachine.TryGetValue(id, out float machineDowntime);
                        _downtimeByMachine[id] = machineDowntime + duration;
                        _active.Remove(machine);
                        RebuildList();
                        FaultCleared?.Invoke(machine, duration);
                        FaultsChanged?.Invoke();
                    }
                    break;
            }
        }

        private void RebuildList()
        {
            _activeList.Clear();
            _activeList.AddRange(_active.Values);
            _activeList.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        }
    }
}
