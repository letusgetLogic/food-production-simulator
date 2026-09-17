using Game.Production;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Bridges the data-free HMI views (<see cref="OverviewPanel"/>,
    /// <see cref="MachineDetailPanel"/>) to the actual <see cref="MachineBase"/>
    /// instances in the scene. Discovers machines at runtime instead of a
    /// hardcoded list, since the machine roster still changes daily.
    ///
    /// Row matching: each <see cref="MachineStateRowView"/> in the Overview must
    /// have its Machine Name set in the editor to exactly the bound machine's
    /// <see cref="IMachine.MachineId"/>. If they don't match, that row simply
    /// never updates - this class does not validate the row list against the
    /// discovered machines.
    /// </summary>
    [DisallowMultipleComponent]
    public class MachineHmiBinder : MonoBehaviour
    {
        [SerializeField] private OverviewPanel _overviewPanel;
        [SerializeField] private MachineDetailPanel _detailPanel;

        private readonly Dictionary<string, MachineBase> _machinesById = new Dictionary<string, MachineBase>();
        // Keeps the exact delegate instance per machine so OnDestroy can unsubscribe it again.
        private readonly Dictionary<string, Action<MachineState, MachineState>> _overviewHandlers =
            new Dictionary<string, Action<MachineState, MachineState>>();

        private MachineBase _boundDetailMachine;
        private Action<MachineState, MachineState> _detailStateHandler;

        private void Awake()
        {
            DiscoverAndBindOverview();
            WireDetailPanelCommands();
        }

        private void OnDestroy()
        {
            foreach (KeyValuePair<string, MachineBase> entry in _machinesById)
            {
                if (entry.Value != null && _overviewHandlers.TryGetValue(entry.Key, out var handler))
                {
                    entry.Value.StateChanged -= handler;
                }
            }

            UnbindDetail();
        }

        private void DiscoverAndBindOverview()
        {
            // Not hardcoded on purpose: how many machines exist is still changing
            // daily this week. Re-run this (e.g. reload the HMI scene section) if
            // machines are added/removed at runtime later on.
            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);

            foreach (MachineBase machine in machines)
            {
                if (string.IsNullOrEmpty(machine.MachineId))
                {
                    Debug.LogWarning(
                        $"{nameof(MachineHmiBinder)}: machine on '{machine.name}' has no MachineId, skipping.",
                        machine);
                    continue;
                }

                if (_machinesById.ContainsKey(machine.MachineId))
                {
                    Debug.LogError(
                        $"{nameof(MachineHmiBinder)}: duplicate MachineId '{machine.MachineId}'.", machine);
                    continue;
                }

                _machinesById.Add(machine.MachineId, machine);

                // Capture machineId by value for the closure - not the loop variable.
                string machineId = machine.MachineId;
                Action<MachineState, MachineState> handler = (_, next) => _overviewPanel.SetMachineState(machineId, next);

                machine.StateChanged += handler;
                _overviewHandlers.Add(machineId, handler);

                _overviewPanel.SetMachineState(machineId, machine.CurrentState);
            }
        }

        /// <summary>
        /// Switches the detail panel to show and operate the given machine. Called by
        /// a machine's <see cref="HmiTerminalInteractable"/> right before it opens the
        /// "machine" panel.
        /// </summary>
        public void BindDetail(MachineBase machine)
        {
            if (ReferenceEquals(_boundDetailMachine, machine))
            {
                return;
            }

            UnbindDetail();

            _boundDetailMachine = machine;

            if (machine == null)
            {
                return;
            }

            _detailStateHandler = (_, next) => _detailPanel.SetState(next);
            machine.StateChanged += _detailStateHandler;

            _detailPanel.SetMachineName(machine.MachineId);
            _detailPanel.SetState(machine.CurrentState);
        }

        private void UnbindDetail()
        {
            if (_boundDetailMachine != null && _detailStateHandler != null)
            {
                _boundDetailMachine.StateChanged -= _detailStateHandler;
            }

            _boundDetailMachine = null;
            _detailStateHandler = null;
        }

        private void WireDetailPanelCommands()
        {
            // Subscribed once for the panel's lifetime. Each handler forwards to
            // whichever machine is currently bound via BindDetail(), not a fixed one.
            _detailPanel.StartRequested += () => _boundDetailMachine?.StartMachine();
            _detailPanel.StopRequested += () => _boundDetailMachine?.StopMachine();
            _detailPanel.AcknowledgeFaultRequested += () => _boundDetailMachine?.AcknowledgeFault();
            _detailPanel.CompleteMaintenanceRequested += () => _boundDetailMachine?.CompleteMaintenance();
            _detailPanel.ResetToIdleRequested += () => _boundDetailMachine?.ResetToIdle();
        }
    }
}