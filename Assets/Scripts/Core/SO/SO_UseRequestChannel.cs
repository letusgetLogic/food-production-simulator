using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Fired when the player presses the "Use" input (R) - the secondary action of the tool currently held,
    /// e.g. raise/lower the forks of the pallet jack, and the "Mode" input (T), e.g. ride the pallet jack.
    /// Only the held tool subscribes (like the drop channel).
    /// </summary>
    [CreateAssetMenu(fileName = "UseRequestChannel", menuName = "Channels/Use Request Channel")]
    public class SO_UseRequestChannel : ScriptableObject
    {
        public event Action UseRequested;

        public void RequestUse() => UseRequested?.Invoke();

        /// <summary>"Mode" input (T): switches the held tool's mode, e.g. riding the pallet jack.</summary>
        public event Action ModeToggleRequested;

        public void RequestModeToggle() => ModeToggleRequested?.Invoke();
    }
}
