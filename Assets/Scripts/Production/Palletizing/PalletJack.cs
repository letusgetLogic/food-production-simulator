using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Hand pallet truck (Hubwagen). The player grabs the handle (E, <see cref="PalletJackHandle"/>) and pushes
    /// it around - it stays on the floor and follows the player's position and view direction. R raises the
    /// forks: a pallet lying over the forks (plus any pallets stacked on it) is lifted and moves with the jack;
    /// R again lowers and sets it down. Q lets go of the handle.
    ///
    /// Moved kinematically like a carried item: a sweep test stops the jack (and the lifted pallets) at walls,
    /// machines and other pallets. Forks have no colliders so they slide under a pallet; the chassis stops at
    /// the pallet edge.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PalletJack : MonoBehaviour, ISaveableState, IReleaseable
    {
        /// <summary>How far the forks lift a pallet (m).</summary>
        public const float LiftHeight = 0.1f;

        [Header("Channels")]
        [SerializeField] private SO_HoldChannel _holdChannel;
        [SerializeField] private SO_HoldPoint _holdPoint;
        [SerializeField] private SO_DropRequestChannel _dropRequestChannel;
        [SerializeField] private SO_UseRequestChannel _useRequestChannel;

        [Header("Parts")]
        [Tooltip("Fork frame that moves up when lifting.")]
        [SerializeField] private Transform _lift;
        [Tooltip("Centre of the area over the forks where a pallet's centre must be to be picked up.")]
        [SerializeField] private Transform _forkCenter;

        [Header("Handling")]
        [Tooltip("Distance from the player to the jack origin (rear, handle base) while pushing.")]
        [Min(0.3f)] [SerializeField] private float _reach = 1.05f;
        [Min(0.1f)] [SerializeField] private float _followSpeed = 8f;
        [Min(0.1f)] [SerializeField] private float _turnSpeed = 6f;
        [Tooltip("Lets go automatically when it gets stuck this far behind the player.")]
        [Min(0.5f)] [SerializeField] private float _maxStuckDistance = 1.2f;
        [Min(0.1f)] [SerializeField] private float _liftSpeed = 0.1f;
        [Tooltip("Pick-up window around the fork centre (local x = across, z = along the forks), metres.")]
        [SerializeField] private Vector2 _pickupWindow = new Vector2(0.35f, 0.45f);
        [Tooltip("Max. yaw difference between pallet and jack (modulo 90°) for a pick-up.")]
        [Range(0f, 45f)] [SerializeField] private float _pickupAngle = 20f;

        private struct CarriedPallet
        {
            public Pallet Pallet;
            public Vector3 LocalPosition; // relative to the lift, in jack space
            public Quaternion LocalRotation;
        }

        private readonly List<CarriedPallet> _carried = new List<CarriedPallet>();
        private readonly List<Pallet> _pickBuffer = new List<Pallet>();
        private Rigidbody _body;
        private Vector3 _liftBaseLocal;
        private float _liftOffset;
        private bool _raised;
        private bool _isHeld;
        private float _groundY;
        private int _lastToggleFrame = -1;

        public bool IsHeld => _isHeld;

        /// <inheritdoc />
        public string ReleaseText =>
            $"(R) {InteractableBase.Localizer("Forks up/down")}   (Q) {InteractableBase.Localizer("Let go")}";
        public bool IsRaised => _raised;
        public int CarriedCount => _carried.Count;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _groundY = transform.position.y;
            if (_lift != null)
            {
                _liftBaseLocal = _lift.localPosition;
            }
        }

        // ---- Handle ----

        /// <summary>Called by the handle (E): the player takes the jack.</summary>
        public void Grab()
        {
            if (_isHeld || _holdChannel == null || _holdPoint == null || !_holdChannel.TryClaim(this))
            {
                return;
            }

            _isHeld = true;
            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested += Release;
            }
            if (_useRequestChannel != null)
            {
                _useRequestChannel.UseRequested += ToggleLift;
            }
        }

        /// <summary>Lets go of the handle (Q). The jack (and a lifted pallet) stays where it is.</summary>
        public void Release()
        {
            if (!_isHeld)
            {
                return;
            }

            _isHeld = false;
            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested -= Release;
            }
            if (_useRequestChannel != null)
            {
                _useRequestChannel.UseRequested -= ToggleLift;
            }
            _holdChannel?.ReleaseClaim();
        }

        private void OnDisable() => Release();

        // ---- Lift ----

        /// <summary>R: raise (picking up the pallet over the forks) or lower (setting it down).</summary>
        public void ToggleLift()
        {
            if (_lastToggleFrame == Time.frameCount)
            {
                return; // one key press = one toggle, even if several listeners relay it
            }
            _lastToggleFrame = Time.frameCount;

            if (_raised)
            {
                _raised = false; // pallets are released when the forks are down again
                return;
            }

            if (_liftOffset > 0.001f)
            {
                return; // still lowering
            }

            TryPickUp();
            _raised = true;
        }

        private void TryPickUp()
        {
            Pallet best = null;
            float bestDistance = float.MaxValue;
            Vector3 center = _forkCenter != null ? _forkCenter.position : transform.position;
            Quaternion inverse = Quaternion.Inverse(transform.rotation);

            foreach (Pallet pallet in Pallet.All)
            {
                if (pallet.IsCarried || pallet.IsHeldByHand)
                {
                    continue;
                }

                Vector3 local = inverse * (pallet.transform.position - center);
                if (Mathf.Abs(local.x) > _pickupWindow.x || Mathf.Abs(local.z) > _pickupWindow.y
                    || pallet.transform.position.y - _groundY > 0.12f)
                {
                    continue; // not over the forks, or not standing on the floor (forks only go under the bottom pallet)
                }

                float yaw = Mathf.Abs(Mathf.DeltaAngle(pallet.transform.eulerAngles.y, transform.eulerAngles.y)) % 90f;
                if (Mathf.Min(yaw, 90f - yaw) > _pickupAngle)
                {
                    continue; // forks would not fit between the blocks
                }

                float distance = new Vector2(local.x, local.z).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = pallet;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                return;
            }

            _pickBuffer.Clear();
            _pickBuffer.Add(best);
            best.CollectStackAbove(_pickBuffer);

            Vector3 liftWorld = LiftWorldPosition(transform.position, transform.rotation);
            foreach (Pallet pallet in _pickBuffer)
            {
                _carried.Add(new CarriedPallet
                {
                    Pallet = pallet,
                    LocalPosition = inverse * (pallet.transform.position - liftWorld),
                    LocalRotation = inverse * pallet.transform.rotation
                });
                pallet.SetCarried(true);
            }
        }

        private void DropAll()
        {
            foreach (CarriedPallet carried in _carried)
            {
                if (carried.Pallet != null)
                {
                    carried.Pallet.SetCarried(false);
                }
            }
            _carried.Clear();
        }

        private Vector3 LiftWorldPosition(Vector3 rootPosition, Quaternion rootRotation) =>
            rootPosition + rootRotation * (_liftBaseLocal + Vector3.up * _liftOffset);

        // ---- Movement ----

        private void FixedUpdate()
        {
            Vector3 position = _body.position;
            Quaternion rotation = _body.rotation;

            if (_isHeld)
            {
                Transform holdPoint = _holdPoint.HoldPoint;
                Transform player = holdPoint != null ? holdPoint.root : null;
                if (player != null)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(holdPoint.forward, Vector3.up);
                    if (forward.sqrMagnitude < 1e-4f)
                    {
                        forward = player.forward;
                    }
                    forward.Normalize();

                    Vector3 target = player.position + forward * _reach;
                    target.y = _groundY;
                    Quaternion targetRotation = Quaternion.LookRotation(forward, Vector3.up);

                    float t = 1f - Mathf.Exp(-_followSpeed * Time.fixedDeltaTime);
                    float r = 1f - Mathf.Exp(-_turnSpeed * Time.fixedDeltaTime);
                    Quaternion nextRotation = Quaternion.Slerp(rotation, targetRotation, r);
                    Vector3 next = Vector3.Lerp(position, target, t);
                    next = Constrain(position, next, player);

                    if ((target - next).sqrMagnitude > _maxStuckDistance * _maxStuckDistance)
                    {
                        Release(); // stuck behind something while the player walked on
                    }
                    else
                    {
                        position = next;
                        rotation = nextRotation;
                        _body.MovePosition(position);
                        _body.MoveRotation(rotation);
                    }
                }
            }

            // Forks up/down.
            float targetOffset = _raised ? LiftHeight : 0f;
            _liftOffset = Mathf.MoveTowards(_liftOffset, targetOffset, _liftSpeed * Time.fixedDeltaTime);
            if (_lift != null)
            {
                _lift.localPosition = _liftBaseLocal + Vector3.up * _liftOffset;
            }

            if (!_raised && _liftOffset <= 0.0001f && _carried.Count > 0)
            {
                MoveCarried(position, rotation);
                DropAll();
                return;
            }

            MoveCarried(position, rotation);
        }

        private void MoveCarried(Vector3 rootPosition, Quaternion rootRotation)
        {
            Vector3 liftWorld = LiftWorldPosition(rootPosition, rootRotation);
            foreach (CarriedPallet carried in _carried)
            {
                if (carried.Pallet == null)
                {
                    continue;
                }

                Rigidbody body = carried.Pallet.Body;
                body.MovePosition(liftWorld + rootRotation * carried.LocalPosition);
                body.MoveRotation(rootRotation * carried.LocalRotation);
            }
        }

        // ---- Collision ----

        private const float SkinWidth = 0.02f;

        /// <summary>Limits the move so neither the jack nor a lifted pallet enters walls, machines or other pallets.</summary>
        private Vector3 Constrain(Vector3 from, Vector3 to, Transform carrier)
        {
            Vector3 move = to - from;
            float distance = move.magnitude;
            if (distance < 1e-5f)
            {
                return to;
            }

            Vector3 direction = move / distance;
            float allowed = distance;
            allowed = Mathf.Min(allowed, AllowedDistance(_body, direction, distance, carrier));
            foreach (CarriedPallet carried in _carried)
            {
                if (carried.Pallet != null)
                {
                    allowed = Mathf.Min(allowed, AllowedDistance(carried.Pallet.Body, direction, distance, carrier));
                }
            }

            return from + direction * allowed;
        }

        private float AllowedDistance(Rigidbody body, Vector3 direction, float distance, Transform carrier)
        {
            float allowed = distance;
            foreach (RaycastHit hit in body.SweepTestAll(direction, distance + SkinWidth, QueryTriggerInteraction.Ignore))
            {
                if (IsBlocking(hit.collider, carrier))
                {
                    allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - SkinWidth));
                }
            }
            return allowed;
        }

        private bool IsBlocking(Collider other, Transform carrier)
        {
            if (other == null || other.isTrigger || other.attachedRigidbody == _body || other.transform.IsChildOf(transform))
            {
                return false;
            }

            if (carrier != null && other.transform.IsChildOf(carrier))
            {
                return false;
            }

            foreach (CarriedPallet carried in _carried)
            {
                if (carried.Pallet != null && other.transform.IsChildOf(carried.Pallet.transform))
                {
                    return false; // own load
                }
            }

            if (other.GetComponentInParent<Pallet>() != null)
            {
                return true; // other pallets stop the jack (chassis at the pallet edge)
            }

            Rigidbody body = other.attachedRigidbody;
            if (body != null && !body.isKinematic)
            {
                return false; // loose products etc. are pushed
            }

            // The floor is not an obstacle (sweeps start just above it).
            return !(other.bounds.max.y <= _groundY + 0.03f);
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("px", transform.position.x);
            values.Set("pz", transform.position.z);
            values.Set("yaw", transform.eulerAngles.y);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            var position = new Vector3(values.GetFloat("px", transform.position.x), _groundY, values.GetFloat("pz", transform.position.z));
            Quaternion rotation = Quaternion.Euler(0f, values.GetFloat("yaw", transform.eulerAngles.y), 0f);
            transform.SetPositionAndRotation(position, rotation);
            _body.position = position;
            _body.rotation = rotation;
        }
    }
}
