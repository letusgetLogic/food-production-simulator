using Game.Core;
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
        public string InputKeyInteract = "E";
        public string InputKeyRelease = "Q";
        public string PromptInteract = "Interact";
        public string PromptRelease => InteractableBase.Localizer("Put down");


        private InputActionMap _map;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _sprintAction;
        private InputAction _interactAction;
        private InputAction _escapeAction;
        private InputAction _releaseHoldAction;
        private InputAction _useAction;
        private InputAction _modeAction;

        public Vector2 Move => _moveAction.ReadValue<Vector2>();
        public Vector2 Look => _lookAction.ReadValue<Vector2>();
        public bool SprintHeld => _sprintAction.IsPressed();

        /// <summary>Raised once per interact button press.</summary>
        public event Action InteractPerformed;
        /// <summary>Raised once per escape button press.</summary>
        public event Action EscapePerformed;
        public event Action ReleasePerformed;

        /// <summary>Secondary action of the held tool (R): pallet jack forks up/down.</summary>
        public event Action UsePerformed;

        /// <summary>Mode switch of the held tool (T): ride the pallet jack.</summary>
        public event Action ModePerformed;

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
            _interactAction.performed += OnInteractPerformed;

            // Closes the focused UI. Q is the main key: in the browser Escape leaves fullscreen instead.
            // Q only drops held items while no UI is focused (HoldItem is disabled during UI focus).
            _escapeAction = _map.AddAction("Escape", InputActionType.Button, "<Keyboard>/q");
            _escapeAction.AddBinding("<Keyboard>/escape");
            _escapeAction.AddBinding("<Mouse>/rightButton"); // right click closes a screen (left click opens it)
            _escapeAction.performed += OnEscapePerformed;

            _releaseHoldAction = _map.AddAction("Release", InputActionType.Button, "<Keyboard>/q");
            _releaseHoldAction.performed += OnDropPerformed;

            _useAction = _map.AddAction("Use", InputActionType.Button, "<Keyboard>/r");
            _useAction.performed += OnUsePerformed;

            _modeAction = _map.AddAction("Mode", InputActionType.Button, "<Keyboard>/t");
            _modeAction.performed += OnModePerformed;
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
        private void OnDropPerformed(InputAction.CallbackContext context) => ReleasePerformed?.Invoke();
        private void OnUsePerformed(InputAction.CallbackContext context) => UsePerformed?.Invoke();
        private void OnModePerformed(InputAction.CallbackContext context) => ModePerformed?.Invoke();
    }
}
