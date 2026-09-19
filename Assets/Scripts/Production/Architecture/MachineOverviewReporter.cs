using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    // One instance per scene. Discovers every MachineBase at runtime (not
    // hardcoded - the roster still changes daily) and forwards state changes
    // into the shared channel. Knows nothing about HMI panels.
    [DisallowMultipleComponent]
    public class MachineOverviewReporter : MonoBehaviour
    {
        [SerializeField] private SO_MachineOverviewChannel _channel;

        private readonly Dictionary<string, int> _machineTypes = new Dictionary<string, int>();
        private readonly Dictionary<string, MachineBase> _machinesById = new Dictionary<string, MachineBase>();
        private readonly Dictionary<string, Action<MachineState, MachineState>> _handlers =
            new Dictionary<string, Action<MachineState, MachineState>>();

        private void Awake()
        {
            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);

            foreach (MachineBase machine in machines)
            {
                if (string.IsNullOrEmpty(machine.MachineId))
                {
                    Debug.LogWarning(
                        $"{nameof(MachineOverviewReporter)}: machine on '{machine.name}' has no MachineId, skipping.",
                        machine);
                    continue;
                }

                if (_machinesById.ContainsKey(machine.MachineId))
                {
                    Debug.LogError(
                        $"{nameof(MachineOverviewReporter)}: duplicate MachineId '{machine.MachineId}'.", machine);
                    continue;
                }

                int index = 0;
                if (_machineTypes.ContainsKey(machine.MachineName))
                {
                    index = _machineTypes[machine.MachineName]++;
                }
                else
                {
                    index = _machineTypes[machine.MachineName] = 1;
                }
                machine.SetMachineId($"{machine.MachineName} {index}");
                _machinesById.Add(machine.MachineId, machine);

                // Capture by value for the closure - not the loop variable.
                string machineId = machine.MachineId;
                string displayName = $"{machine.MachineName} {index}"; 

                Action<MachineState, MachineState> handler = (_, next) =>
                    _channel.ReportState(machineId, displayName, next);

                machine.StateChanged += handler;
                _handlers.Add(machineId, handler);

                _channel.ReportState(machineId, displayName, machine.CurrentState);
            }
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, MachineBase> entry in _machinesById)
            {
                if (entry.Value != null && _handlers.TryGetValue(entry.Key, out var handler))
                {
                    entry.Value.StateChanged -= handler;
                }
            }
        }
    }
}
