using Game.Core;
using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// Switches between "walking around" and "operating a UI panel": suspends the
    /// controller and the aim interactor, releases the cursor, and reverses it all
    /// when focus is released.
    /// </summary>
    [DisallowMultipleComponent]
    public class InputModeController : MonoBehaviour
    {
        [SerializeField] private UiFocusChannel _uiFocusChannel;
        [SerializeField] private FirstPersonController _controller;
        [SerializeField] private AimInteractor _aimInteractor;

        private void OnEnable()
        {
            _uiFocusChannel.FocusChanged += OnFocusChanged;
            OnFocusChanged(_uiFocusChannel.IsUiFocused);
        }

        private void OnDisable() => _uiFocusChannel.FocusChanged -= OnFocusChanged;

        private void OnFocusChanged(bool uiFocused)
        {
            if (_controller != null)
            {
                _controller.ControlEnabled = !uiFocused;
            }

            if (_aimInteractor != null)
            {
                // Disabling clears the current hover target via OnDisable.
                _aimInteractor.enabled = !uiFocused;
            }

            Cursor.lockState = uiFocused ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiFocused;
        }
    }
}
