using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Production
{
    // One instance per scene. Discovers every MachineBase at runtime (not
    // hardcoded - the roster still changes daily) and forwards state changes
    // into the shared channel. Knows nothing about HMI panels.
    [DisallowMultipleComponent]
    public class MachineOverviewReporter : MonoBehaviour
    {
        [SerializeField] private SO_MachineOverviewChannel _channel;

        private readonly Dictionary<string, int> _machineTypes = new();
        private readonly Dictionary<string, MachineBase> _machinesById = new();
        public MachineBase GetMachineById(string machineId) => _machinesById.TryGetValue(machineId, out var machine) ? machine : null;
        private readonly Dictionary<string, Action<MachineState, MachineState>> _handlers = new();

        private readonly Dictionary<string, Action<bool, string>> _warningHandlers = new();

        private void Awake()
        {
            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);

            foreach (MachineBase machine in machines)
            {
                if (machine.gameObject.activeInHierarchy == false)
                    continue;

                _machineTypes.TryGetValue(machine.NameKey, out int count);
                _machineTypes[machine.NameKey] = count + 1;
                machine.SetNumber(count + 1);
                _machinesById.Add(machine.Id, machine);

                // Capture by value for the closure - not the loop variable.
                string machineId = machine.Id;

                Action<MachineState, MachineState> handler = (_, next) =>
                    _channel.ReportState(machineId, machine.DisplayName, next);

                machine.StateChanged += handler;
                _handlers.Add(machineId, handler);

                Action<bool, string> warningHandler = (hasWarning, reason) =>
                    _channel.ReportWarning(machineId, hasWarning, reason);
                machine.WarningChanged += warningHandler;
                _warningHandlers.Add(machineId, warningHandler);

                // Belts only appear in the overview while they are in Fault.
                _channel.SetHiddenUnlessFault(machineId, machine is ConveyorBelt);

                _channel.ReportState(machineId, machine.DisplayName, machine.CurrentState);
            }
        }

        /// <summary>
        /// Awake may run before the string tables are loaded (scene started directly in the editor - names are
        /// the GameObject names then). The language only changes in the main menu, so each machine is reported
        /// once more as soon as its translated name is available. Waiting only for the localization
        /// initialisation is not enough: the name's own table operation may still be running, and machines
        /// that do not change state on their own (dough mixers) would keep the GameObject name in the overview.
        /// </summary>
        private IEnumerator Start()
        {
            yield return LocalizationSettings.InitializationOperation;

            var pending = new List<KeyValuePair<string, MachineBase>>(_machinesById);
            float giveUpTime = Time.realtimeSinceStartup + NameTimeoutSeconds;
            while (pending.Count > 0)
            {
                bool timedOut = Time.realtimeSinceStartup > giveUpTime;
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    MachineBase machine = pending[i].Value;
                    if (machine == null)
                    {
                        pending.RemoveAt(i);
                    }
                    else if (machine.IsNameLoaded || timedOut)
                    {
                        _channel.ReportState(pending[i].Key, machine.DisplayName, machine.CurrentState);
                        pending.RemoveAt(i);
                    }
                }

                yield return null;
            }
        }

        private const float NameTimeoutSeconds = 10f;

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, MachineBase> entry in _machinesById)
            {
                if (entry.Value != null && _handlers.TryGetValue(entry.Key, out var handler))
                {
                    entry.Value.StateChanged -= handler;
                }

                if (entry.Value != null && _warningHandlers.TryGetValue(entry.Key, out var warningHandler))
                {
                    entry.Value.WarningChanged -= warningHandler;
                }
            }
        }

    }
}
