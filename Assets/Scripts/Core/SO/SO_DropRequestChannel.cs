using System;
using UnityEngine;

namespace Game.Core
{
    // Fired whenever the player presses the dedicated Drop input.
    // Only the currently held PickupInteractable subscribes to this
    // (it subscribes on pickup, unsubscribes on release), so exactly
    // one item reacts per request — no registry or static reference needed.
    [CreateAssetMenu(fileName = "DropRequestChannel", menuName = "Channels/Drop Request Channel")]
    public class SO_DropRequestChannel : ScriptableObject
    {
        public event Action DropRequested;

        public void RequestDrop()
        {
            DropRequested?.Invoke();
        }
    }
}
