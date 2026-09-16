using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Shared runtime flag telling the platform layer that a UI surface has focus
    /// and player control should be suspended.
    ///
    /// Note: this is a small piece of shared runtime state, NOT the general
    /// ScriptableObject event-channel architecture that is still up for a team
    /// decision. It exists so Game.HMI and Game.Platform can talk without either
    /// referencing the other.
    /// </summary>
    [CreateAssetMenu(menuName = "Core/UI Focus Channel", fileName = "UiFocusChannel")]
    public class UiFocusChannel : ScriptableObject
    {
        [NonSerialized] private int _focusRequests;

        public bool IsUiFocused => _focusRequests > 0;

        public event Action<bool> FocusChanged;

        /// <summary>Reference-counted so several surfaces can hold focus at once.</summary>
        public void PushFocus()
        {
            _focusRequests++;
            if (_focusRequests == 1)
            {
                FocusChanged?.Invoke(true);
            }
        }

        public void PopFocus()
        {
            if (_focusRequests == 0)
            {
                return;
            }

            _focusRequests--;
            if (_focusRequests == 0)
            {
                FocusChanged?.Invoke(false);
            }
        }

        /// <summary>ScriptableObjects survive play-mode exits in the editor – reset on load.</summary>
        private void OnEnable() => _focusRequests = 0;
    }
}
