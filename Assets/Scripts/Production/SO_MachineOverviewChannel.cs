using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    // Shared machine-state broadcast, consumed by any number of HMI displays.
    // Lives in Game.Production (not Game.Core) because it carries MachineState,
    // which is defined here - putting it in Core would create a Core -> Production
    // -> Core cycle.
    [CreateAssetMenu(menuName = "HMI/Machine Overview Channel", fileName = "MachineOverviewChannel")]
    public class SO_MachineOverviewChannel : ScriptableObject
    {
        private readonly Dictionary<string, MachineState> _states = new Dictionary<string, MachineState>();
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _warnings = new Dictionary<string, string>();
        private readonly HashSet<string> _hiddenUnlessFault = new HashSet<string>();

        /// <summary>
        /// Marks a machine (e.g. a conveyor belt) as "only show in the overview while it is in Fault"
        /// (HMI decision 01.10.). Call before the first ReportState.
        /// </summary>
        public void SetHiddenUnlessFault(string machineId, bool hidden)
        {
            if (hidden)
            {
                _hiddenUnlessFault.Add(machineId);
            }
            else
            {
                _hiddenUnlessFault.Remove(machineId);
            }
        }

        public bool IsHiddenUnlessFault(string machineId) => _hiddenUnlessFault.Contains(machineId);

        public event Action<string, string, MachineState> MachineStateChanged; // (machineId, displayName, state)

        public IReadOnlyDictionary<string, MachineState> CurrentStates => _states;
        public IReadOnlyDictionary<string, string> DisplayNames => _displayNames;

        // (machineId, hasWarning, reason) - non-blocking warnings (HMI amber), see MachineBase.SetWarning.
        public event Action<string, bool, string> MachineWarningChanged;

        /// <summary>Machines that currently have a warning, with their reason code.</summary>
        public IReadOnlyDictionary<string, string> CurrentWarnings => _warnings;

        public void ReportWarning(string machineId, bool hasWarning, string reason)
        {
            if (hasWarning)
            {
                _warnings[machineId] = reason;
            }
            else
            {
                _warnings.Remove(machineId);
            }

            MachineWarningChanged?.Invoke(machineId, hasWarning, reason);
        }

        public void ReportState(string machineId, string displayName, MachineState state)
        {
            _states[machineId] = state;
            _displayNames[machineId] = displayName;
            MachineStateChanged?.Invoke(machineId, displayName, state);
        }

        private void OnEnable()
        {
            // Guards against stale data surviving a play session when Domain
            // Reload is disabled in the editor.
            _states.Clear();
            _displayNames.Clear();
            _warnings.Clear();
            _hiddenUnlessFault.Clear();
        }
    }
}
