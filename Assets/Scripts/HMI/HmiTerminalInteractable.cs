using Game.Core;
using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    // The physical terminal in the hall. Sits on its own collider next to a
    // machine - the machine itself stays free of IInteractable. If _machine is
    // assigned, interacting rebinds this terminal's own MachineDetailBinder to
    // this machine before opening it; leave _machine empty for a generic
    // terminal that only opens the plant-wide overview.
    public class HmiTerminalInteractable : InteractableBase
    {
        [SerializeField] private SO_HmiInteractChannel _interactChannel;
        [SerializeField] private MachineBase _machine;
        [SerializeField] private string _panelId = "overview";

        public override void OnInteract(IInteractor interactor)
        {
            if (_interactChannel != null)
            {
                _interactChannel.Request(_panelId, _machine);
            }
            else
            {
                Debug.LogWarning($"HmiTerminalInteractable {name} has no interact channel assigned.");
            }
        }
    }
}