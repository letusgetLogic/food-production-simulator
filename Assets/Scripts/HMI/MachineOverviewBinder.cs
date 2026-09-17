using Game.Production;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Findet alle zur Laufzeit vorhandenen IMachine-Instanzen und hält die
    /// MachineStateRowViews im OverviewPanel synchron. Kennt keine feste
    /// Maschinenanzahl.
    /// </summary>
    public sealed class MachineOverviewBinder : MonoBehaviour
    {
        [SerializeField] private OverviewPanel _overviewPanel;

        private readonly Dictionary<IMachine, MachineStateRowView> _rows = new();
        private readonly Dictionary<IMachine, Action<MachineState, MachineState>> _handlers = new();

        private void OnEnable()
        {
            var machines = FindObjectsByType<MonoBehaviour>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OfType<IMachine>();

            foreach (var machine in machines)
                BindMachine(machine);
        }

        private void OnDisable()
        {
            foreach (var machine in _rows.Keys.ToList())
                UnbindMachine(machine);
        }

        private void BindMachine(IMachine machine)
        {
            if (_rows.ContainsKey(machine)) return;

            var row = _overviewPanel.AddOrGetRow(machine.MachineName);
            row.SetState(machine.CurrentState);
            _rows[machine] = row;

            void Handler(MachineState previous, MachineState current) => row.SetState(current);
            machine.StateChanged += Handler;
            _handlers[machine] = Handler;
        }

        private void UnbindMachine(IMachine machine)
        {
            if (_handlers.TryGetValue(machine, out var handler))
            {
                machine.StateChanged -= handler;
                _handlers.Remove(machine);
            }
            _rows.Remove(machine);
        }
    }
}
