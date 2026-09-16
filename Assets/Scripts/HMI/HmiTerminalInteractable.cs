using Game.Core;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// The physical terminal in the hall. Sits on its own collider next to a
    /// machine – the machine itself stays free of IInteractable. Inherits
    /// IsInteractable / InteractableChanged / OnHoverEnter / OnHoverExit from
    /// InteractableBase and only supplies the actual action.
    /// </summary>
    public class HmiTerminalInteractable : InteractableBase
    {
        [SerializeField] private HmiScreenController _screen;
        [SerializeField] private string _panelId = "overview";

        // Adapt the base call/signature below if InteractableBase's Interact
        // is not a plain override (e.g. requires calling a base implementation).
        public override void OnInteract(IInteractor interactor)
        {
            if (_screen == null)
            {
                Debug.LogError($"{nameof(HmiTerminalInteractable)}: no screen assigned.", this);
                return;
            }

            _screen.Open(_panelId);
        }
    }
}