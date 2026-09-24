using System;
using UnityEngine;

namespace Game.Core
{
    // Shared reference to the player's current hold point (usually a transform
    // parented in front of the camera). Written once by the player rig,
    // read by any PickupInteractable that wants to attach itself while held.
    // No hard dependency between Game.Platform and wherever pickup items live.
    [CreateAssetMenu(fileName = "HoldPointChannel", menuName = "Channels/Hold Point Channel")]
    public class SO_HoldPointChannel : ScriptableObject
    {
        public Transform HoldPoint { get; private set; }

        // Simple claim flag so only one PickupInteractable can be held at a time.
        // Lives here (not on a separate channel) since "is something held" is
        // inherently about the same player-carrying concept as the hold point itself.
        private bool _isHeldItem;

        public bool IsHeldItem 
        {
            get => _isHeldItem;
            protected set
            {
                if (_isHeldItem == value)
                {
                    return;
                }

                _isHeldItem = value;
                HoldChanged?.Invoke(_isHeldItem);
            }
        }

        public event Action<bool> HoldChanged;

        public void SetHoldPoint(Transform holdPoint)
        {
            HoldPoint = holdPoint;
        }

        // Returns true and marks the slot as taken if nothing else is currently held.
        // Returns false if another item already claimed it.
        public bool TryClaim()
        {
            if (IsHeldItem)
            {
                return false;
            }

            IsHeldItem = true;
            return true;
        }

        public void ReleaseClaim()
        {
            IsHeldItem = false;
        }

        private void OnEnable()
        {
            // ScriptableObjects persist between play sessions in the editor —
            // reset the claim so a stale "held" state can't survive domain reload.
            IsHeldItem = false;
        }
    }
}