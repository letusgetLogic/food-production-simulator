using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    // Shared machine-state broadcast, consumed by any number of HMI displays.
    // Lives in Game.Production (not Game.Core) because it carries MachineState,
    // which is defined here - putting it in Core would create a Core -> Production
    // -> Core cycle.
    [CreateAssetMenu(menuName = "HMI/Machine Overview Channel")]
    public class SO_MachineOverviewChannel : ScriptableObject
    {
        private readonly Dictionary<string, MachineState> _states = new Dictionary<string, MachineState>();
        private readonly Dictionary<string, string> _displayNames = new Dictionary<string, string>();

        public event Action<string, string, MachineState> MachineStateChanged; // (machineId, displayName, state)

        public IReadOnlyDictionary<string, MachineState> CurrentStates => _states;
        public IReadOnlyDictionary<string, string> DisplayNames => _displayNames;

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
        }
    }
}
