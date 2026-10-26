using System;

namespace Game.Core
{
    /// <summary>
    /// Represents anything that can perform an interaction against an <see cref="IInteractable"/>.
    /// Today this is implemented by <c>MouseInteractor</c> (Game.Platform); from Week 5 an
    /// <c>XrInteractor</c> will implement it too. Deliberately platform-agnostic: no mouse,
    /// ray, or XR-controller types appear on this interface, only the outcome (which
    /// interactable is currently targeted, and the ability to trigger it).
    /// </summary>
    public interface IInteractor
    {
        /// <summary>
        /// The interactable currently targeted by this interactor, or null if none.
        /// Platform implementations decide how "targeted" is determined (raycast, gaze,
        /// controller pointer, ...).
        /// </summary>
        IInteractable CurrentTarget { get; }

        /// <summary>
        /// Raised whenever <see cref="CurrentTarget"/> changes. Passes the previous and the
        /// new target (either may be null) so listeners can clear/apply hover state without
        /// re-querying.
        /// </summary>
        event Action<IInteractable, IInteractable> TargetChanged;

        /// <summary>
        /// Attempts to trigger <see cref="CurrentTarget"/>'s interaction, if any and if it is
        /// currently interactable. Returns true if an interaction was actually triggered.
        /// </summary>
        bool TryInteract();
    }
}
