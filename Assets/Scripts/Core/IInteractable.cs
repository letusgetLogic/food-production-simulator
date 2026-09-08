using System;

namespace Game.Core
{
    /// <summary>
    /// Represents anything in the scene that can be interacted with by an <see cref="IInteractor"/>.
    /// Implemented today by navigation arrows and (from Day 3) HMI buttons; from Week 5 also by
    /// VR interaction targets. Deliberately has no dependency on any specific platform, machine,
    /// or product type.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Whether this object can currently be interacted with. Use this to disable a navigation
        /// arrow while a machine is still loading, or to disable an HMI button while its underlying
        /// action is not available.
        /// </summary>
        bool IsInteractable { get; }

        /// <summary>
        /// Raised whenever <see cref="IsInteractable"/> changes value, so UI/visual elements
        /// (e.g. arrow highlight, button enabled state) can react without polling every frame.
        /// </summary>
        event Action<bool> InteractableChanged;

        /// <summary>
        /// Called by an <see cref="IInteractor"/> to trigger this interactable's action.
        /// Implementations should no-op (or throw, at the implementer's discretion) if
        /// <see cref="IsInteractable"/> is false at the time of the call.
        /// </summary>
        /// <param name="interactor">The interactor that triggered the interaction.</param>
        void Interact(IInteractor interactor);

        /// <summary>
        /// Called when an interactor begins hovering/targeting this interactable, so it can
        /// show a visual highlight (outline, glow, cursor change, etc.). Implementations that
        /// don't need a highlight can leave this empty.
        /// </summary>
        void OnHoverEnter(IInteractor interactor);

        /// <summary>
        /// Called when an interactor stops hovering/targeting this interactable, to clear
        /// any highlight applied in <see cref="OnHoverEnter"/>.
        /// </summary>
        void OnHoverExit(IInteractor interactor);
    }
}
