using Game.Core;
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

        [Header("Pushing")]
        [Tooltip("Force (N) the player pushes loose physics objects with when walking into them (pallet 25 kg).")]
        [Min(0f)] [SerializeField] private float _pushForce = 450f;
        [Tooltip("Objects heavier than this (kg) cannot be pushed.")]
        [Min(0f)] [SerializeField] private float _maxPushMass = 200f;

        [Header("Riding")]
        [Tooltip("What the player holds - a held IRideable (pallet jack) can be ridden. Empty = from the AimInteractor.")]
        [SerializeField] private SO_HoldChannel _holdChannel;

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
        private bool _isRiding;
        private IRideable _ride;

        /// <summary>Lets HMI panels or cutscenes suspend player control.</summary>
        public bool ControlEnabled { get; set; } = true;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _input = GetComponent<PlayerInputReader>();

            if (_holdChannel == null)
            {
                AimInteractor aim = GetComponentInChildren<AimInteractor>(true);
                _holdChannel = aim != null ? aim.HoldChannel : null;
            }

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

            IRideable ride = CurrentRide();
            if (ride != null)
            {
                UpdateRide(ride);
                return;
            }

            if (_isRiding)
            {
                StopRiding();
            }
            UpdateMove();
        }

        // ---- Riding (pallet jack as a scooter) ----

        private IRideable CurrentRide() =>
            _holdChannel != null && _holdChannel.HoldingObject is IRideable ride && ride.IsRiding ? ride : null;

        /// <summary>
        /// The ridden tool moves (speed, steering, walls), the player stands on it: W/S are the throttle, the view
        /// direction steers. Getting on turns the player towards the tool's <see cref="IRideable.RiderLookTarget"/>.
        /// The CharacterController is off meanwhile, so it does not collide with the tool under the player's feet.
        /// </summary>
        private void UpdateRide(IRideable ride)
        {
            _ride = ride;
            if (!_isRiding)
            {
                _isRiding = true;
                _controller.enabled = false;
                _verticalVelocity = 0f;
                _horizontalVelocity = Vector3.zero;
                transform.position = ride.RiderPosition;
                LookAt(ride.RiderLookTarget);
            }

            ride.Drive(Mathf.Clamp(_input.Move.y, -1f, 1f));
            transform.position = ride.RiderPosition;
        }

        /// <summary>Turns body (yaw) and camera (pitch) towards a world point.</summary>
        private void LookAt(Vector3 target)
        {
            Vector3 eye = _cameraPivot != null ? _cameraPivot.position : transform.position;
            Vector3 toTarget = target - eye;
            Vector3 flat = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (flat.sqrMagnitude > 1e-4f)
            {
                transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
            }
            _pitch = Mathf.Clamp(-Mathf.Atan2(toTarget.y, flat.magnitude) * Mathf.Rad2Deg, _minPitch, _maxPitch);
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        /// <summary>Getting off: step to the tool's dismount point (behind the handle) if there is room.</summary>
        private void StopRiding()
        {
            _isRiding = false;
            _horizontalVelocity = Vector3.zero;
            if (_ride is Component tool && tool != null)
            {
                Dismount(_ride.DismountPosition, tool.transform);
                // Face the tool again (pushing it follows the view direction).
                Vector3 flat = Vector3.ProjectOnPlane(tool.transform.position - transform.position, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
                }
            }
            _ride = null;
            _controller.enabled = true;
        }

        private readonly Collider[] _dismountOverlaps = new Collider[16];

        /// <summary>Steps to <paramref name="position"/> unless something (other than the player and the tool) is there.</summary>
        private void Dismount(Vector3 position, Transform tool)
        {
            float radius = _controller.radius;
            Vector3 bottom = position + Vector3.up * (radius + _controller.stepOffset);
            Vector3 top = position + Vector3.up * Mathf.Max(radius, _controller.height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius * 0.9f, _dismountOverlaps, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform other = _dismountOverlaps[i].transform;
                if (!other.IsChildOf(transform) && !other.IsChildOf(tool))
                {
                    return; // blocked: stay where the player stands
                }
            }
            transform.position = position;
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

        /// <summary>
        /// A CharacterController does not push rigidbodies by itself: walking into a loose (non-kinematic) object
        /// pushes it horizontally at the contact point, so it slides or tips over an edge. Objects the player stands
        /// on, kinematic ones (parked pallet stacks, held items) and very heavy ones are not pushed.
        /// </summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic || body.mass > _maxPushMass || hit.moveDirection.y < -0.3f)
            {
                return;
            }

            // The held tool (pallet jack) follows the player by itself - walking into it must not shove it.
            if (_holdChannel != null && _holdChannel.HoldingObject != null
                && body.transform.IsChildOf(_holdChannel.HoldingObject.transform))
            {
                return;
            }

            Vector3 direction = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            direction.Normalize();

            // Not faster than the player walks into it.
            float speedAlong = Vector3.Dot(body.linearVelocity, direction);
            if (speedAlong >= _horizontalVelocity.magnitude)
            {
                return;
            }

            body.AddForceAtPosition(direction * (_pushForce * Time.deltaTime), hit.point, ForceMode.Impulse);
        }

        private static void ApplyCursorState(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
