using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// Free 3D movement for the factory floor. Walking only – no jump, no crouch,
    /// deliberately slow so machine stations stay readable.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputReader))]
    public class FirstPersonController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _cameraPivot;

        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 2.6f;
        [SerializeField] private float _sprintSpeed = 4.6f;
        [SerializeField] private float _acceleration = 14f;
        [SerializeField] private float _gravity = -18f;

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.08f;
        [SerializeField] private float _minPitch = -80f;
        [SerializeField] private float _maxPitch = 80f;
        [SerializeField] private bool _lockCursor = true;

        private CharacterController _controller;
        private PlayerInputReader _input;
        public PlayerInputReader Input;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _pitch;

        /// <summary>Lets HMI panels or cutscenes suspend player control.</summary>
        public bool ControlEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            if (_cameraPivot == null)
            {
                Debug.LogError($"{nameof(FirstPersonController)}: camera pivot is not assigned.", this);
            }
        }

        private void OnEnable() => ApplyCursorState(_lockCursor);

        private void OnDisable() => ApplyCursorState(false);

        private void Update()
        {
            if (!ControlEnabled)
            {
                _horizontalVelocity = Vector3.zero;
                return;
            }

            UpdateLook();
            UpdateMove();
        }

        private void UpdateLook()
        {
            Vector2 look = _input.Look * _lookSensitivity;

            transform.Rotate(Vector3.up, look.x, Space.Self);

            _pitch = Mathf.Clamp(_pitch - look.y, _minPitch, _maxPitch);
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void UpdateMove()
        {
            Vector2 move = Vector2.ClampMagnitude(_input.Move, 1f);
            float targetSpeed = _input.SprintHeld ? _sprintSpeed : _walkSpeed;

            Vector3 target = (transform.right * move.x + transform.forward * move.y) * targetSpeed;
            _horizontalVelocity = Vector3.MoveTowards(
                _horizontalVelocity, target, _acceleration * Time.deltaTime);

            _verticalVelocity = _controller.isGrounded && _verticalVelocity < 0f
                ? -2f
                : _verticalVelocity + _gravity * Time.deltaTime;

            Vector3 velocity = _horizontalVelocity + Vector3.up * _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static void ApplyCursorState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
