using Game.Core;
using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// The physical terminal in the hall. Sits on its own collider next to a
    /// machine - the machine itself stays free of IInteractable. If _machine is
    /// assigned, interacting rebinds the shared MachineDetailPanel to this
    /// machine before opening it; leave _machine empty for a generic terminal
    /// that only opens the plant-wide overview.
    /// </summary>
    public class HmiTerminalInteractable : InteractableBase
    {
        [SerializeField] private HmiScreenController _screen;
        [SerializeField] private MachineHmiBinder _binder;
        [SerializeField] private MachineBase _machine;
        [SerializeField] private string _panelId = "overview";

        public override void OnInteract(IInteractor interactor)
        {
            if (_screen == null)
            {
                Debug.LogError($"{nameof(HmiTerminalInteractable)}: no screen assigned.", this);
                return;
            }

            if (_machine != null && _binder != null)
            {
                _binder.BindDetail(_machine);
            }

            _screen.Open(_panelId);
        }
    }
}