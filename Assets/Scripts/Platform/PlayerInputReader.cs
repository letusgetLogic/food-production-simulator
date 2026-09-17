using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Platform
{
    /// <summary>
    /// Single entry point for player input. Actions are defined in code so no
    /// .inputactions asset has to be wired up; swap this out for a generated
    /// wrapper later without touching the consumers.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        private InputActionMap _map;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _interactAction;
        private InputAction _escapeAction;

        public Vector2 Move => _moveAction.ReadValue<Vector2>();
        public Vector2 Look => _lookAction.ReadValue<Vector2>();
        public bool SprintHeld => _sprintAction.IsPressed();

        /// <summary>Raised once per interact button press.</summary>
        public event Action InteractPerformed;
        /// <summary>Raised once per escape button press.</summary>
        public event Action EscapePerformed;

        private void Awake()
        {
            _map = new InputActionMap("Player");

            _moveAction = _map.AddAction("Move", InputActionType.Value);
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _lookAction = _map.AddAction("Look", InputActionType.Value, "<Mouse>/delta");

            _sprintAction = _map.AddAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");

            _interactAction = _map.AddAction("Interact", InputActionType.Button, "<Keyboard>/e");
            _interactAction.AddBinding("<Mouse>/leftButton");
            _interactAction.performed += OnInteractPerformed;

            _escapeAction = _map.AddAction("Escape", InputActionType.Button, "<Keyboard>/escape");
            _escapeAction.performed += OnEscapePerformed;
        }

        private void OnEnable() => _map.Enable();

        private void OnDisable() => _map.Disable();

        private void OnDestroy()
        {
            _interactAction.performed -= OnInteractPerformed;
            _escapeAction.performed -= OnEscapePerformed;
            _map.Dispose();
        }

        private void OnInteractPerformed(InputAction.CallbackContext context) => InteractPerformed?.Invoke();
        private void OnEscapePerformed(InputAction.CallbackContext context) => EscapePerformed?.Invoke();
    }
}
