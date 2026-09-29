using System;
using UnityEngine;

namespace Game.Core
{
    // Shared reference to the player's current hold point (usually a transform
    // parented in front of the camera). Written once by the player rig,
    // read by any PickupInteractable that wants to attach itself while held.
    // No hard dependency between Game.Platform and wherever pickup items live.
    [CreateAssetMenu(fileName = "HoldChannel", menuName = "Channels/Hold Channel")]
    public class SO_HoldChannel : ScriptableObject
    {
        private HoldInteractable _holdingObject;
        public HoldInteractable HoldingObject
        {
            get => _holdingObject;
            protected set
            {
                if (_holdingObject == value)
                {
                    return;
                }

                _holdingObject = value;
                HoldChanged?.Invoke(_holdingObject != null);
            }
        }

        public event Action<bool> HoldChanged;

        // Returns true and marks the slot as taken if nothing else is currently held.
        // Returns false if another item already claimed it.
        public bool TryClaim(HoldInteractable holdingObject)
        {
            if (_holdingObject != null)
            {
                return false;
            }

            HoldingObject = holdingObject;
            return true;
        }

        public void ReleaseClaim()
        {
            HoldingObject = null;
        }

        private void OnEnable()
        {
            // ScriptableObjects persist between play sessions in the editor —
            // reset the claim so a stale "held" state can't survive domain reload.
            HoldingObject = null;
        }
    }
}