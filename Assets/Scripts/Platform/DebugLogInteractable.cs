using Game.Core;
using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// Temporary smoke-test component: logs to the console on hover/interact. Drop this on
    /// one of the 8 station placeholder volumes (with a Collider) to verify the full
    /// MouseInteractor -> IInteractable chain works before Day 2 replaces it with the real
    /// navigation arrows. Safe to delete once real IInteractable implementations exist.
    /// </summary>
    [AddComponentMenu("Game/Platform/Debug Log Interactable (Smoke Test)")]
    public class DebugLogInteractable : InteractableBase
    {
        protected override void OnInteract(IInteractor interactor)
        {
            Debug.Log($"[Smoke Test] {name} interacted with by {interactor.GetType().Name}.", this);
        }

        public override void OnHoverEnter(IInteractor interactor)
        {
            Debug.Log($"[Smoke Test] {name} hover enter.", this);
        }

        public override void OnHoverExit(IInteractor interactor)
        {
            Debug.Log($"[Smoke Test] {name} hover exit.", this);
        }
    }
}
