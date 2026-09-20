using Game.Core;
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
        [SerializeField] private SO_LanguageSwitcherChannel _languageChannel;

        private readonly Dictionary<string, int> _machineTypes = new();
        private readonly Dictionary<string, MachineBase> _machinesById = new();
        private readonly Dictionary<string, Action<MachineState, MachineState>> _handlers = new();

        private readonly Dictionary<string, Action> _nameHandlers = new();

        private void Awake()
        {
            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);

            foreach (MachineBase machine in machines)
            {
                int index = 0;
                if (_machineTypes.ContainsKey(machine.NameKey))
                {
                    _machineTypes[machine.NameKey]++;
                    index = _machineTypes[machine.NameKey];
                }
                else
                {
                    _machineTypes.Add(machine.NameKey, 1);
                    index = _machineTypes[machine.NameKey];
                }
                machine.SetNumber(index);
                _machinesById.Add(machine.Id, machine);

                // Capture by value for the closure - not the loop variable.
                string machineId = machine.Id;
                string DisplayName() => $"{machine.Name} {index}";

                Action<MachineState, MachineState> handler = (_, next) =>
                    _channel.ReportState(machineId, DisplayName(), next);
                Action nameHandler = () =>
                    _channel.ReportState(machineId, DisplayName(), machine.CurrentState);

                machine.StateChanged += handler;
                _handlers.Add(machineId, handler);

                _languageChannel.LanguageChanged += nameHandler;
                _nameHandlers.Add(machineId, nameHandler);

                _channel.ReportState(machineId, DisplayName(), machine.CurrentState);
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

            foreach (KeyValuePair<string, Action> entry in _nameHandlers)
            {
                if (_languageChannel != null)
                    _languageChannel.LanguageChanged -= entry.Value;
            }
        }
    }
}
