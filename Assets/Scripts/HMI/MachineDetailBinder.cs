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

        [Tooltip("How often setpoints/actual values are pulled from the machine (seconds, unscaled).")]
        [SerializeField] private float _refreshIntervalSeconds = 0.2f;

        private MachineBase _boundMachine;
        public MachineBase BoundMachine => _boundMachine;
        private Action<MachineState, MachineState> _stateHandler;
        private float _nextRefreshTime;

        // Start instead of Awake: the machine on the same GameObject must have run its own Awake
        // (config defaults, clip length) before its setpoints are read.
        private void Start()
        {
            if (_machineInstance)
            {
                Bind(_machineInstance);
            }
        }

        private void Update()
        {
            if (_boundMachine == null || _detailPanel == null || Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + _refreshIntervalSeconds;
            if (_detailPanel.isActiveAndEnabled && _detailPanel.IsVisible)
            {
                _detailPanel.RefreshValues();
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
            machine.ContentChanged += _detailPanel.SetContent;

            _detailPanel.SetMachineName(machine.DisplayName);
            _detailPanel.SetState(machine.CurrentState);

            if (_boundMachine)
            {
                _detailPanel.StartRequested += () => _boundMachine.StartRun();
                _detailPanel.StopRequested += () => _boundMachine.StopRun();
                _detailPanel.AcknowledgeFaultRequested += () => _boundMachine.AcknowledgeFault();
                _detailPanel.CompleteMaintenanceRequested += () => _boundMachine.CompleteMaintenance();
                _detailPanel.ResetToIdleRequested += () => _boundMachine.ResetToIdle();
            }

            _detailPanel.SetValueSource(machine as IMachineParameterSource);
        }

        private void Unbind()
        {
            if (_boundMachine != null)
            {
                if (_stateHandler != null)
                    _boundMachine.StateChanged -= _stateHandler;

                _boundMachine.ContentChanged -= _detailPanel.SetContent;
            }

            if (_detailPanel != null)
            {
                _detailPanel.ClearDelegates();
                _detailPanel.SetValueSource(null);
            }

            _boundMachine = null;
            _stateHandler = null;
        }

        private void OnDestroy() => Unbind();
    }
}
