using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Hand pallet truck (Hubwagen). The player grabs the handle (E, <see cref="PalletJackHandle"/>) and pushes
    /// it around - it stays on the floor and follows the player's position and view direction. R raises the
    /// forks: a pallet lying over the forks (plus any pallets stacked on it) is lifted and moves with the jack,
    /// and so is every loose physics object over the forks or on the lifted pallet (pot, dough, pizzas ...);
    /// R again lowers and sets everything down. Q lets go of the handle.
    ///
    /// T (empty jack, forks down) switches to riding: the player stands on the forks facing the handle and drives
    /// it like a scooter, handle first (<see cref="IRideable"/>). W drives (faster than sprinting), S reverses
    /// slowly, the view direction steers: the handle (RotateHandle) turns 15/30/45° towards it and the jack turns
    /// accordingly while moving (at a standstill only the handle turns). While riding nothing else works - Q gets off.
    ///
    /// A real physics body (default 100 kg, moves in the floor plane only): pushing and riding set its velocity,
    /// collisions are left to the physics engine - it stops at walls and machines, shoves loose objects and
    /// bounces off on impact. Forks have no colliders so they slide under a pallet; the chassis stops at the pallet
    /// edge. Parts without own colliders (forks and rider while riding, lifted loads) are checked with casts and
    /// limit the velocity instead.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PalletJack : MonoBehaviour, ISaveableState, IHoldingCustomHint, IRideable
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

        [Header("Physics")]
        [Min(1f)] [SerializeField] private float _mass = 100f;
        [Tooltip("Max. velocity change per second when pushed or driven (m/s²) - how hard it pushes against obstacles.")]
        [Min(0.5f)] [SerializeField] private float _driveAcceleration = 20f;
        [Tooltip("Braking (linear damping) while nobody pushes or drives it.")]
        [Min(0f)] [SerializeField] private float _idleDamping = 3f;
        [Min(0.5f)] [SerializeField] private float _maxPushSpeed = 5f;

        [Header("Handling")]
        [Tooltip("Distance from the player to the jack origin (rear, handle base) while pushing.")]
        [Min(0.3f)][SerializeField] private float _reach = 1.05f;
        [Min(0.1f)][SerializeField] private float _followSpeed = 8f;
        [Tooltip("Max. turn rate while pushing (°/s): the jack is turned directly towards the view, half of the " +
                 "remaining angle per physics step up to this rate.")]
        [Min(10f)][SerializeField] private float _maxPushTurnRate = 300f;
        [Tooltip("Lets go automatically when it gets stuck this far behind the player.")]
        [Min(0.5f)][SerializeField] private float _maxStuckDistance = 1.2f;
        [Min(0.1f)][SerializeField] private float _liftSpeed = 0.1f;
        [Tooltip("Pick-up window around the fork centre (local x = across, z = along the forks), metres.")]
        [SerializeField] private Vector2 _pickupWindow = new Vector2(0.35f, 0.45f);
        [Tooltip("Max. yaw difference between pallet and jack (modulo 90°) for a pick-up.")]
        [Range(0f, 45f)][SerializeField] private float _pickupAngle = 20f;
        [Tooltip("Without a pallet: loose objects whose centre lies in this area around the fork centre are lifted " +
                 "(local x = across, z = along the forks, half sizes in metres).")]
        [SerializeField] private Vector2 _looseLoadArea = new Vector2(0.4f, 0.6f);
        [Tooltip("How high above the floor (or above a lifted pallet) a loose object may start to be lifted (m).")]
        [Min(0.01f)][SerializeField] private float _looseLoadMaxGap = 0.15f;

        [Header("Riding (T)")]
        [Tooltip("Top speed forwards, handle first (m/s).")]
        [Min(0.5f)][SerializeField] private float _rideSpeed = 6f;
        [Tooltip("Top speed backwards, forks first (m/s).")]
        [Min(0.1f)][SerializeField] private float _rideReverseSpeed = 1.2f;
        [Min(0.5f)][SerializeField] private float _rideAcceleration = 5f;
        [Tooltip("Rotating part of the handle (RotateHandle) - turns around its Y axis when steering.")]
        [SerializeField] private Transform _steeringHandle;
        [Tooltip("Handle grip - the rider looks at it when getting on.")]
        [SerializeField] private Transform _grip;
        [Tooltip("Handle angles (°) for small / medium / large deviation of the view from the driving direction.")]
        [SerializeField] private Vector3 _steerSteps = new Vector3(15f, 30f, 45f);
        [Tooltip("Turn rate of the jack at full steer (45°) and full speed (°/s).")]
        [Min(1f)][SerializeField] private float _rideMaxTurnRate = 90f;
        [Tooltip("Below this speed (m/s) the jack turns proportionally slower - at a standstill only the handle turns.")]
        [Min(0.01f)][SerializeField] private float _rideTurnFullSpeed = 1f;
        [Tooltip("Where the rider stands, local to the jack (feet). Empty = halfway between handle and fork centre.")]
        [SerializeField] private Transform _rideStandPoint;
        [Tooltip("Height of the rider's feet above the floor (standing on the forks).")]
        [Min(0f)][SerializeField] private float _rideStandHeight = 0.08f;

        /// <summary>Something on the forks: a pallet (pallet logic decides kinematic) or any loose rigidbody.</summary>
        private struct CarriedLoad
        {
            public Rigidbody Body;
            public Pallet Pallet;
            public bool WasKinematic;
            public RigidbodyInterpolation Interpolation;
            public Vector3 LocalPosition; // relative to the lift, in jack space
            public Quaternion LocalRotation;
        }

        private readonly List<CarriedLoad> _carried = new List<CarriedLoad>();
        private readonly List<Pallet> _pickBuffer = new List<Pallet>();
        private readonly HashSet<Rigidbody> _looseBuffer = new HashSet<Rigidbody>();
        private readonly Collider[] _overlaps = new Collider[64];
        private Collider[] _ownColliders = System.Array.Empty<Collider>();
        private readonly List<Vector3> _blockNormals = new List<Vector3>();
        private readonly List<ContactPoint> _contacts = new List<ContactPoint>();
        private bool _isRiding;
        private float _throttle;
        private float _rideSpeedNow; // signed, + = handle first
        private float _steerAngle;   // quantized handle angle (°), + = to the right
        private Quaternion _steeringHandleBase;
        private float _holdStillUntil;
        private Rigidbody _body;
        private Vector3 _liftBaseLocal;
        private float _liftOffset;
        private bool _raised;
        private bool _isHeld;
        private float _groundY;
        private int _lastToggleFrame = -1;

        public bool IsHeld => _isHeld;

        /// <inheritdoc />
        public string HintText => _isRiding
            ? $"(W/S) {InteractableBase.Localizer("Drive")}   (Q) {InteractableBase.Localizer("Get off")}"
            : CanRide
                ? $"(R) {InteractableBase.Localizer("Forks up/down")}   (T) {InteractableBase.Localizer("Ride")}   (Q) {InteractableBase.Localizer("Let go")}"
                : $"(R) {InteractableBase.Localizer("Forks up/down")}   (Q) {InteractableBase.Localizer("Let go")}";

        /// <summary>Riding needs an empty jack with the forks down.</summary>
        public bool CanRide => _isHeld && !_raised && _liftOffset <= 0.001f && _carried.Count == 0;

        // ---- IRideable ----

        public bool IsRiding => _isRiding;
        public Vector3 RiderLookTarget =>
            _grip != null ? _grip.position : transform.position + Vector3.up * 1.1f - transform.forward * 0.3f;
        public Vector3 RiderPosition => transform.TransformPoint(RideStandLocal) + Vector3.up * _rideStandHeight;
        public Vector3 DismountPosition
        {
            get
            {
                Vector3 back = transform.position - transform.forward * _reach;
                back.y = _groundY;
                return back;
            }
        }

        public void Drive(float throttle) => _throttle = Mathf.Clamp(throttle, -1f, 1f);

        /// <summary>Signed driving speed while riding (m/s, + = handle first).</summary>
        public float RideSpeedNow => _rideSpeedNow;

        /// <summary>Current handle angle while riding (°).</summary>
        public float SteerAngle => _steerAngle;

        private Vector3 RideStandLocal
        {
            get
            {
                if (_rideStandPoint != null)
                {
                    Vector3 local = transform.InverseTransformPoint(_rideStandPoint.position);
                    local.y = 0f;
                    return local;
                }
                Vector3 fork = _forkCenter != null ? transform.InverseTransformPoint(_forkCenter.position) : Vector3.forward;
                return new Vector3(0f, 0f, fork.z * 0.5f);
            }
        }
        public bool IsRaised => _raised;
        public int CarriedCount => _carried.Count;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = false;
            _body.mass = _mass;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX
                                | RigidbodyConstraints.FreezeRotationZ;
            _body.linearDamping = _idleDamping;
            _body.angularDamping = _idleDamping;
            _ownColliders = GetComponentsInChildren<Collider>();
            _groundY = transform.position.y;

            _liftBaseLocal = _lift.localPosition;

            _steeringHandleBase = _steeringHandle.localRotation;
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
            SetCollisionWithHolder(false);
            _dropRequestChannel.DropRequested += Release;
            _useRequestChannel.UseRequested += ToggleLift;
            _useRequestChannel.ModeToggleRequested += ToggleRide;
        }

        /// <summary>
        /// Q: while riding the player gets off (and keeps the handle), otherwise lets go of the handle.
        /// The jack (and its load) stays where it is.
        /// </summary>
        public void Release()
        {
            if (!_isHeld)
            {
                return;
            }

            if (_isRiding)
            {
                StopRide();
                return;
            }

            ReleaseHandle();
        }

        private void ReleaseHandle()
        {
            StopRide();

            SetCollisionWithHolder(true);
            _isHeld = false;
            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested -= Release;
            }
            if (_useRequestChannel != null)
            {
                _useRequestChannel.UseRequested -= ToggleLift;
                _useRequestChannel.ModeToggleRequested -= ToggleRide;
            }
            _holdChannel?.ReleaseClaim();
        }

        private void OnDisable()
        {
            if (_isHeld)
            {
                ReleaseHandle();
            }
        }

        // ---- Riding ----

        /// <summary>T: get on (empty jack, forks down). Getting off is Q - while riding only Q works.</summary>
        public void ToggleRide()
        {
            if (_isRiding || !CanRide)
            {
                return;
            }

            _isRiding = true;
            _throttle = 0f;
            _rideSpeedNow = 0f;
        }

        private void StopRide()
        {
            if (_isRiding)
            {
                // The rider steps off in the player's next Update - until then the jack stays put and does not
                // count the player (still on the forks) as "too far away".
                _holdStillUntil = Time.time + 0.25f;
            }
            _isRiding = false;
            _throttle = 0f;
            _rideSpeedNow = 0f;
            _steerAngle = 0f;
        }

        /// <summary>0 / ±15 / ±30 / ±45: the handle angle for a deviation of the view from the driving direction.</summary>
        private float QuantizeSteer(float deviation)
        {
            float magnitude = Mathf.Abs(deviation);
            float step = magnitude < _steerSteps.x * 0.5f ? 0f
                : magnitude < (_steerSteps.x + _steerSteps.y) * 0.5f ? _steerSteps.x
                : magnitude < (_steerSteps.y + _steerSteps.z) * 0.5f ? _steerSteps.y
                : _steerSteps.z;
            return Mathf.Sign(deviation) * step;
        }

        private void LateUpdate()
        {
            if (_steeringHandle == null)
            {
                return;
            }

            // Handle follows the steering angle (snappy but not instant).
            Quaternion target = _steeringHandleBase * Quaternion.Euler(0f, _steerAngle, 0f);
            _steeringHandle.localRotation = Quaternion.RotateTowards(_steeringHandle.localRotation, target,
                360f * Time.deltaTime);
        }

        // ---- Lift ----

        /// <summary>R: raise (picking up the pallet over the forks) or lower (setting it down).</summary>
        public void ToggleLift()
        {
            if (_isRiding || _lastToggleFrame == Time.frameCount)
            {
                return; // riding: forks locked; one key press = one toggle, even if several listeners relay it
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

            Vector3 liftWorld = LiftWorldPosition(transform.position, transform.rotation);
            _pickBuffer.Clear();
            if (best != null)
            {
                _pickBuffer.Add(best);
                best.CollectStackAbove(_pickBuffer);
            }

            foreach (Pallet pallet in _pickBuffer)
            {
                _carried.Add(new CarriedLoad
                {
                    Body = pallet.Body,
                    Pallet = pallet,
                    LocalPosition = inverse * (pallet.transform.position - liftWorld),
                    LocalRotation = inverse * pallet.transform.rotation
                });
                pallet.SetCarried(true);
            }

            // Loose objects: over the forks (no pallet) or standing on the lifted pallet(s).
            CollectLooseLoad(center, inverse);
            foreach (Rigidbody body in _looseBuffer)
            {
                _carried.Add(new CarriedLoad
                {
                    Body = body,
                    WasKinematic = body.isKinematic,
                    Interpolation = body.interpolation,
                    LocalPosition = inverse * (body.position - liftWorld),
                    LocalRotation = inverse * body.rotation
                });
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }
            _looseBuffer.Clear();

            // The kinematic load would shove the (dynamic) jack it sits on.
            foreach (CarriedLoad carried in _carried)
            {
                SetCollisionWithJack(carried.Body, false);
            }
        }

        private Collider[] _holderColliders;

        /// <summary>
        /// While the player holds the handle, the jack and the player's body do not collide: the handle end swings
        /// through the player's capsule when the jack turns with the view. With contact the capsule blocked the
        /// turn and the jack stayed at an angle to the view (and the contact counted as an obstacle).
        /// </summary>
        private void SetCollisionWithHolder(bool enabled)
        {
            if (!enabled)
            {
                Transform holder = _holdPoint != null && _holdPoint.HoldPoint != null ? _holdPoint.HoldPoint.root : null;
                _holderColliders = holder != null ? holder.GetComponentsInChildren<Collider>(true) : null;
            }
            if (_holderColliders == null)
            {
                return;
            }

            foreach (Collider other in _holderColliders)
            {
                foreach (Collider own in _ownColliders)
                {
                    if (own != null && other != null)
                    {
                        Physics.IgnoreCollision(own, other, !enabled);
                    }
                }
            }

            if (enabled)
            {
                _holderColliders = null;
            }
        }

        private void SetCollisionWithJack(Rigidbody body, bool enabled)
        {
            if (body == null)
            {
                return;
            }
            foreach (Collider other in body.GetComponentsInChildren<Collider>())
            {
                foreach (Collider own in _ownColliders)
                {
                    if (own != null && other != null)
                    {
                        Physics.IgnoreCollision(own, other, !enabled);
                    }
                }
            }
        }

        /// <summary>
        /// Loose (non-kinematic) rigidbodies whose centre lies over the load area and whose bottom is at most
        /// <see cref="_looseLoadMaxGap"/> above the floor or above the lifted pallets - objects resting on those
        /// are collected too (pot with dough, cartons on a carton ...).
        /// </summary>
        private void CollectLooseLoad(Vector3 forkCenter, Quaternion inverse)
        {
            _looseBuffer.Clear();

            // Area: the lifted pallets, or the forks.
            Bounds area;
            float supportTop = _groundY;
            if (_pickBuffer.Count > 0)
            {
                area = PalletBounds(_pickBuffer[0]);
                foreach (Pallet pallet in _pickBuffer)
                {
                    Bounds b = PalletBounds(pallet);
                    area.Encapsulate(b);
                    supportTop = Mathf.Max(supportTop, b.max.y);
                }
            }
            else
            {
                area = new Bounds(forkCenter, Vector3.zero);
            }

            Vector3 halfExtents = _pickBuffer.Count > 0
                ? new Vector3(area.extents.x, 1.5f, area.extents.z)
                : new Vector3(_looseLoadArea.x, 1.5f, _looseLoadArea.y);
            Quaternion boxRotation = _pickBuffer.Count > 0 ? Quaternion.identity : transform.rotation;
            Vector3 boxCenter = new Vector3(area.center.x, _groundY + 1.5f, area.center.z);
            int count = Physics.OverlapBoxNonAlloc(boxCenter, halfExtents, _overlaps, boxRotation, ~0,
                QueryTriggerInteraction.Ignore);

            // Grow from the support upwards: a body counts when it rests on the floor/pallet or on a collected body.
            bool added = true;
            var levels = new List<float> { supportTop };
            while (added)
            {
                added = false;
                for (int i = 0; i < count; i++)
                {
                    Rigidbody body = _overlaps[i].attachedRigidbody;
                    if (!IsLooseLoad(body, forkCenter, inverse, halfExtents) || _looseBuffer.Contains(body))
                    {
                        continue;
                    }

                    Bounds bounds = BodyBounds(body);
                    int levelCount = levels.Count;
                    for (int l = 0; l < levelCount; l++)
                    {
                        if (bounds.min.y <= levels[l] + _looseLoadMaxGap)
                        {
                            _looseBuffer.Add(body);
                            levels.Add(bounds.max.y);
                            added = true;
                            break;
                        }
                    }
                }
            }
        }

        private bool IsLooseLoad(Rigidbody body, Vector3 forkCenter, Quaternion inverse, Vector3 halfExtents)
        {
            if (body == null || body == _body || body.isKinematic || body.GetComponentInParent<Pallet>() != null)
            {
                return false; // static, own, held/parked/station items, pallets (handled above)
            }

            Transform holder = _holdPoint != null && _holdPoint.HoldPoint != null ? _holdPoint.HoldPoint.root : null;
            if (holder != null && body.transform.IsChildOf(holder))
            {
                return false;
            }

            if (_pickBuffer.Count == 0)
            {
                Vector3 local = inverse * (body.worldCenterOfMass - forkCenter);
                return Mathf.Abs(local.x) <= halfExtents.x && Mathf.Abs(local.z) <= halfExtents.z;
            }
            return true;
        }

        private static Bounds PalletBounds(Pallet pallet) => BodyBounds(pallet.Body);

        private static Bounds BodyBounds(Rigidbody body)
        {
            bool found = false;
            var bounds = new Bounds(body.position, Vector3.zero);
            foreach (Collider collider in body.GetComponentsInChildren<Collider>())
            {
                if (collider.isTrigger || collider.attachedRigidbody != body)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            return bounds;
        }

        private void DropAll()
        {
            foreach (CarriedLoad carried in _carried)
            {
                SetCollisionWithJack(carried.Body, true);
                if (carried.Pallet != null)
                {
                    carried.Pallet.SetCarried(false);
                }
                else if (carried.Body != null)
                {
                    carried.Body.interpolation = carried.Interpolation;
                    carried.Body.isKinematic = carried.WasKinematic;
                    if (!carried.WasKinematic)
                    {
                        carried.Body.WakeUp();
                    }
                }
            }
            _carried.Clear();
        }

        private Vector3 LiftWorldPosition(Vector3 rootPosition, Quaternion rootRotation) =>
            rootPosition + rootRotation * (_liftBaseLocal + Vector3.up * _liftOffset);

        // ---- Movement ----

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 position = _body.position;
            Quaternion rotation = _body.rotation;
            Vector3 desiredVelocity = Vector3.zero;
            float yawRate = 0f; // °/s
            Quaternion pushTurn = Quaternion.identity; // pushing: turn applied directly this step
            bool driven = false;
            Vector3 nextVelocity = _body.linearVelocity;
            Transform player = null;

            if (_isHeld && _isRiding)
            {
                Transform holdPoint = _holdPoint.HoldPoint;
                player = holdPoint != null ? holdPoint.root : null;

                // Driving direction = handle first (the rider faces the handle).
                Vector3 travel = -(rotation * Vector3.forward);
                travel.y = 0f;
                travel.Normalize();

                // An impact slows the jack down - continue from the speed it really has.
                float measured = Vector3.Dot(_body.linearVelocity, travel);
                if (Mathf.Abs(measured) < Mathf.Abs(_rideSpeedNow) - 0.5f)
                {
                    _rideSpeedNow = measured;
                }

                // Speed: W forwards fast, S backwards slowly.
                float targetSpeed = _throttle >= 0f ? _throttle * _rideSpeed : _throttle * _rideReverseSpeed;
                _rideSpeedNow = Mathf.MoveTowards(_rideSpeedNow, targetSpeed, _rideAcceleration * dt);

                // Steering: the view's deviation from the driving direction picks a handle angle (15/30/45°);
                // the jack turns with a rate from that angle and its speed, never past the view direction.
                Vector3 view = holdPoint != null ? Vector3.ProjectOnPlane(holdPoint.forward, Vector3.up) : travel;
                float deviation = view.sqrMagnitude > 1e-4f ? Vector3.SignedAngle(travel, view, Vector3.up) : 0f;
                _steerAngle = QuantizeSteer(deviation);
                Vector3 pivotShift = Vector3.zero;
                if (_steerAngle != 0f)
                {
                    // Without throttle only the handle turns (LateUpdate). With throttle the jack turns - also when
                    // it is held up by an obstacle, so it can steer away from a wall.
                    float speed = Mathf.Max(Mathf.Abs(_rideSpeedNow), Mathf.Abs(targetSpeed));
                    float speedFactor = Mathf.Clamp01(speed / _rideTurnFullSpeed);
                    float turn = _rideMaxTurnRate * (_steerAngle / _steerSteps.z) * speedFactor * dt;
                    turn = Mathf.Sign(turn) * Mathf.Min(Mathf.Abs(turn), Mathf.Abs(deviation));
                    Quaternion delta = Quaternion.AngleAxis(turn, Vector3.up);
                    bool turned = false;
                    if (turn != 0f && !ForksBlockedAt(position, delta * rotation, player))
                    {
                        turned = true; // turn around the handle (steer wheels)
                    }
                    else if (turn != 0f)
                    {
                        // The forks would swing into something: turn around the fork centre instead (forks stay,
                        // the handle end swings - it collides physically).
                        Vector3 forkCenter = position + rotation * ForkCenterLocalFlat;
                        Vector3 pivoted = forkCenter + delta * (position - forkCenter);
                        if (!ForksBlockedAt(pivoted, delta * rotation, player))
                        {
                            pivotShift = (pivoted - position) / dt;
                            turned = true;
                        }
                    }
                    if (turned)
                    {
                        yawRate = turn / dt;
                        travel = delta * travel;
                    }
                }

                desiredVelocity = travel * _rideSpeedNow + pivotShift;
                driven = true;
            }
            else if (_isHeld && Time.time >= _holdStillUntil)
            {
                Transform holdPoint = _holdPoint.HoldPoint;
                player = holdPoint != null ? holdPoint.root : null;
                if (player != null)
                {
                    Vector3 forward = Vector3.ProjectOnPlane(holdPoint.forward, Vector3.up);
                    if (forward.sqrMagnitude < 1e-4f)
                    {
                        forward = player.forward;
                    }
                    forward.Normalize();

                    Vector3 target = player.position + forward * _reach;
                    target.y = position.y;
                    Vector3 offset = target - position;
                    if (offset.sqrMagnitude > _maxStuckDistance * _maxStuckDistance)
                    {
                        Release(); // stuck behind something while the player walked on
                    }
                    else
                    {
                        desiredVelocity = Vector3.ClampMagnitude(offset * _followSpeed, _maxPushSpeed);
                        float yawError = Mathf.DeltaAngle(rotation.eulerAngles.y,
                            Quaternion.LookRotation(forward, Vector3.up).eulerAngles.y);
                        // The jack is turned directly (not by angular velocity): a contact (pallet stack, wall,
                        // parked pallets) must not hold it at an angle to the view - the physics then pushes it off
                        // the obstacle. Half of the remaining angle per step, capped; the last bit snaps.
                        float step = Mathf.Abs(yawError) < 0.5f ? yawError
                            : Mathf.Clamp(yawError * 0.5f, -_maxPushTurnRate * dt, _maxPushTurnRate * dt);
                        pushTurn = Quaternion.AngleAxis(step, Vector3.up);
                        _body.WakeUp();
                        driven = true;
                    }
                }
            }

            if (driven)
            {
                // Parts without own colliders (lifted loads, forks/rider while riding) limit the velocity.
                desiredVelocity = LimitVelocity(position, desiredVelocity, player);

                // Do not keep driving into a wall/machine the jack touches: without that push the physics can
                // move it off the obstacle while it turns, and it slides along walls at an angle.
                foreach (Vector3 normal in _blockNormals)
                {
                    float into = Vector3.Dot(desiredVelocity, normal);
                    if (into < 0f)
                    {
                        desiredVelocity -= normal * into;
                    }
                }

                Vector3 current = _body.linearVelocity;
                current.y = 0f;
                Vector3 change = Vector3.ClampMagnitude(desiredVelocity - current, _driveAcceleration * dt);
                _body.linearDamping = 0f;
                _body.angularDamping = 0f;
                _body.AddForce(change, ForceMode.VelocityChange);
                nextVelocity = current + change;
                _body.angularVelocity = new Vector3(0f, yawRate * Mathf.Deg2Rad, 0f);
                if (pushTurn != Quaternion.identity)
                {
                    _body.MoveRotation(pushTurn * rotation);
                }
            }
            else
            {
                _body.linearDamping = _idleDamping;
                _body.angularDamping = _idleDamping;
                if (_isRiding)
                {
                    _rideSpeedNow = 0f;
                }
            }

            _blockNormals.Clear(); // refilled by OnCollisionStay after this physics step

            // Pose after this step - the lifted load follows it (kinematic, without a step lag).
            nextVelocity.y = 0f;
            position += nextVelocity * dt;
            rotation = pushTurn * Quaternion.AngleAxis(_body.angularVelocity.y * Mathf.Rad2Deg * dt, Vector3.up) * rotation;

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

        /// <summary>Contacts with fixed obstacles (static or kinematic) - the drive does not push into them.</summary>
        private void OnCollisionStay(Collision collision)
        {
            Rigidbody other = collision.rigidbody;
            if (other != null && !other.isKinematic)
            {
                return; // loose objects are pushed
            }

            collision.GetContacts(_contacts);
            foreach (ContactPoint contact in _contacts)
            {
                Vector3 normal = contact.normal; // points from the obstacle towards the jack
                normal.y = 0f;
                if (normal.sqrMagnitude > 1e-4f)
                {
                    _blockNormals.Add(normal.normalized);
                }
            }
        }

        private void MoveCarried(Vector3 rootPosition, Quaternion rootRotation)
        {
            Vector3 liftWorld = LiftWorldPosition(rootPosition, rootRotation);
            foreach (CarriedLoad carried in _carried)
            {
                if (carried.Body == null)
                {
                    continue;
                }

                Rigidbody body = carried.Body;
                body.MovePosition(liftWorld + rootRotation * carried.LocalPosition);
                body.MoveRotation(rootRotation * carried.LocalRotation);
            }
        }

        // ---- Collision ----

        private const float SkinWidth = 0.02f;

        /// <summary>
        /// Limits the velocity so that parts the physics engine does not move - lifted loads (kinematic) and,
        /// while riding, the colliderless forks and the rider - do not run into walls, machines or other pallets.
        /// The jack's own chassis collides physically.
        /// </summary>
        private Vector3 LimitVelocity(Vector3 from, Vector3 velocity, Transform carrier)
        {
            float distance = velocity.magnitude * Time.fixedDeltaTime;
            if (distance < 1e-5f)
            {
                return velocity;
            }

            Vector3 direction = velocity.normalized;
            float allowed = distance;
            foreach (CarriedLoad carried in _carried)
            {
                if (carried.Body != null)
                {
                    allowed = Mathf.Min(allowed, AllowedDistance(carried.Body, direction, distance, carrier));
                }
            }

            if (_isRiding)
            {
                allowed = Mathf.Min(allowed, AllowedDistanceForks(from, direction, distance, carrier));
                allowed = Mathf.Min(allowed, AllowedDistanceRider(from, direction, distance, carrier));
            }

            return velocity * (allowed / distance);
        }

        /// <summary>Fork centre relative to the jack root, on the floor plane (local).</summary>
        private Vector3 ForkCenterLocalFlat
        {
            get
            {
                Vector3 local = _forkCenter != null ? transform.InverseTransformPoint(_forkCenter.position) : Vector3.forward;
                return new Vector3(local.x, 0f, local.z);
            }
        }

        private bool ForksBlockedAt(Vector3 position, Quaternion rotation, Transform carrier)
        {
            Vector3 center = position + rotation * ForkCenterLocalFlat + Vector3.up * 0.08f;
            var halfExtents = new Vector3(_looseLoadArea.x, 0.04f, _looseLoadArea.y);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (IsBlocking(_overlaps[i], carrier))
                {
                    return true;
                }
            }
            return false;
        }

        private float AllowedDistanceForks(Vector3 from, Vector3 direction, float distance, Transform carrier)
        {
            Vector3 forkLocal = _forkCenter != null ? transform.InverseTransformPoint(_forkCenter.position) : Vector3.forward;
            Vector3 center = from + transform.rotation * new Vector3(forkLocal.x, 0f, forkLocal.z) + Vector3.up * 0.08f;
            var halfExtents = new Vector3(_looseLoadArea.x, 0.04f, _looseLoadArea.y);
            float allowed = distance;
            foreach (RaycastHit hit in Physics.BoxCastAll(center, halfExtents, direction, transform.rotation,
                         distance + SkinWidth, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance > 0f && IsBlocking(hit.collider, carrier))
                {
                    allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - SkinWidth));
                }
            }
            return allowed;
        }

        private float AllowedDistanceRider(Vector3 from, Vector3 direction, float distance, Transform carrier)
        {
            CharacterController controller = carrier != null ? carrier.GetComponent<CharacterController>() : null;
            float radius = controller != null ? controller.radius : 0.3f;
            float height = controller != null ? controller.height : 1.8f;
            Vector3 feet = from + transform.rotation * RideStandLocal + Vector3.up * _rideStandHeight;
            // Bottom sphere above the jack's chassis, top sphere at head height.
            Vector3 bottom = feet + Vector3.up * Mathf.Max(radius, 0.35f);
            Vector3 top = feet + Vector3.up * Mathf.Max(radius, height - radius);
            float allowed = distance;
            foreach (RaycastHit hit in Physics.CapsuleCastAll(bottom, top, radius, direction, distance + SkinWidth, ~0,
                         QueryTriggerInteraction.Ignore))
            {
                if (hit.distance > 0f && IsBlocking(hit.collider, carrier))
                {
                    allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - SkinWidth));
                }
            }
            return allowed;
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

            foreach (CarriedLoad carried in _carried)
            {
                if (carried.Body != null && (other.attachedRigidbody == carried.Body || other.transform.IsChildOf(carried.Body.transform)))
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
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
    }
}
