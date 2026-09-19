using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
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
    }
}
