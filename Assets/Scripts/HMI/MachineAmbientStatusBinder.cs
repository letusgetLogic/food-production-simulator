using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Small always-visible status display mounted on/near one specific machine's
    /// physical terminal prop - readable from nearby even without interacting.
    /// Separate from the interactive popup opened via HmiTerminalInteractable.
    /// Reuses MachineStateRowView; subscribes directly to this one machine's
    /// StateChanged since it only ever shows a single, fixed machine - no need
    /// for the shared MachineOverviewChannel here.
    /// </summary>
    [DisallowMultipleComponent]
    public class MachineAmbientStatusBinder : MonoBehaviour
    {
        [SerializeField] private MachineBase _machine;
        [SerializeField] private MachineStateRowView _row;

        private void OnEnable()
        {
            if (_machine == null || _row == null)
            {
                Debug.LogError($"{nameof(MachineAmbientStatusBinder)}: machine or row not assigned.", this);
                return;
            }

            _row.SetMachineName(_machine.Id);
            _row.SetState(_machine.CurrentState);
            _machine.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (_machine != null)
            {
                _machine.StateChanged -= OnStateChanged;
            }
        }

        private void OnStateChanged(MachineState previous, MachineState next) => _row.SetState(next);
    }
}