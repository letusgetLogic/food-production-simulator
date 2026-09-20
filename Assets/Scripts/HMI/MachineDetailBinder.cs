using Game.Production;
using System;
using UnityEngine;

namespace Game.HMI
{
    // One per physical detail panel (terminal, tablet). Bound directly by
    // whichever HmiTerminalInteractable opens it - selection is local to that
    // display, not shared via a channel.
    public class MachineDetailBinder : MonoBehaviour
    {
        [SerializeField] private MachineDetailPanel _detailPanel;
        [SerializeField] private MachineBase _machineInstance;

        private MachineBase _boundMachine;
        private Action<MachineState, MachineState> _stateHandler;

        private void Awake()
        {
            if (_boundMachine)
            {
                _detailPanel.StartRequested += () => _boundMachine.StartMachine();
                _detailPanel.StopRequested += () => _boundMachine.StopMachine();
                _detailPanel.AcknowledgeFaultRequested += () => _boundMachine.AcknowledgeFault();
                _detailPanel.CompleteMaintenanceRequested += () => _boundMachine.CompleteMaintenance();
                _detailPanel.ResetToIdleRequested += () => _boundMachine.ResetToIdle();
            }

            if (_machineInstance)
            {
                Bind(_machineInstance);
            }
        }

        public void Bind(MachineBase machine)
        {
            if (ReferenceEquals(_boundMachine, machine))
            {
                return;
            }

            Unbind();
            _boundMachine = machine;

            if (machine == null)
            {
                return;
            }

            _stateHandler = (_, next) => _detailPanel.SetState(next);
            machine.StateChanged += _stateHandler;

            _detailPanel.SetMachineName(machine.Name);
            _detailPanel.SetState(machine.CurrentState);
        }

        private void Unbind()
        {
            if (_boundMachine != null && _stateHandler != null)
            {
                _boundMachine.StateChanged -= _stateHandler;
            }

            _boundMachine = null;
            _stateHandler = null;
        }

        private void OnDestroy() => Unbind();
    }
}
