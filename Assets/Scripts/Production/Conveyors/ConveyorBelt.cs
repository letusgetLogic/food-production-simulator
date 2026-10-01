using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Physically simulated flat conveyor belt segment for dough products.
    ///
    /// Principle:
    ///  - A single trigger collider covers the belt surface. A product is driven by the belt whose
    ///    collider contains the product's centre - so at a transfer between two belts exactly one belt
    ///    moves it, never both.
    ///  - Products ride WITH the belt: when the belt moves, every product on it gets the belt velocity;
    ///    when the belt stands, every product stands. Nothing ever slides over a moving belt.
    ///  - The motor state (MachineState) and the momentary motion are separate:
    ///      Running + moving   = normal transport
    ///      Running + paused   = <see cref="IsHeld"/>: the belt waits (stop-and-go buffer, downstream full)
    ///  - Three kinds of belt, by configuration:
    ///      a) Transport belt with <see cref="_downstream"/> (e.g. under the portioner): runs continuously,
    ///         pauses only while a product waits at its discharge sensor and the downstream element cannot
    ///         accept (buffer full). Warning while paused.
    ///      b) Buffer belt with an <see cref="AccumulationZone"/>: zoned, every slot runs or stands on its own
    ///         (the zone decides per product). Warning from the fill level, Fault "BufferFull" if full and
    ///         the press takes nothing for too long.
    ///      c) Free belt without either (e.g. outfeed, tunnel belt): always moves while Running.
    ///  - Jam detection: a product on a MOVING belt barely moves = Fault "Jam".
    ///  - Recovery from Fault follows the MachineBase convention (no self-reset):
    ///    AcknowledgeFault -> Maintenance -> CompleteMaintenance -> Ready -> RequestRun.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ConveyorBelt : MachineBase, IInfeedReadiness, IDownstreamLink
    {
        /// <summary>Content messages this belt reports via NotifyContentChanged.</summary>
        public enum ConveyorInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            HeldByDownstream,
            BufferFilling,
            BufferFull,
            Jammed
        }

        public const string FaultReasonJam = "Jam";
        public const string FaultReasonBufferFull = "BufferFull";
        public const string WarningReasonBufferFilling = "BufferFilling";
        public const string WarningReasonBufferFull = "BufferFull";
        public const string WarningReasonHeldByDownstream = "HeldByDownstream";

        [SerializeField] private SO_ConveyorConfig _config;

        [Tooltip("Transform whose forward axis defines the belt's travel direction. Defaults to this transform.")]
        [SerializeField] private Transform _beltDirectionReference;

        [Header("Flow Control")]
        [Tooltip("Makes this belt a stop-and-go buffer belt (Puffer-Band) in front of a taktende machine.")]
        [SerializeField] private AccumulationZone _accumulationZone;

        [Tooltip("Next element in the line. Transport belt: pauses while it cannot accept. Buffer belt: passed on to the zone (press). " +
                 "Overwritten by ConveyorLineController when auto-wiring is on.")]
        [SerializeField] private MachineBase _downstream;

        [Tooltip("Transport belt only, optional: PresenceSensor at the discharge end. The belt pauses only while this sensor " +
                 "sees a product AND the downstream element cannot accept. Without it, the belt pauses as soon as downstream cannot accept.")]
        [SerializeField] private PresenceSensor _dischargeSensor;

        [Header("HMI Content")]
        [SerializeField] private List<Content<ConveyorInfo>> _contents = new List<Content<ConveyorInfo>>();

        // Products touching the belt trigger -> number of their colliders inside it.
        private readonly Dictionary<ProductToken, int> _colliderCounts = new Dictionary<ProductToken, int>();

        // Last checked position per product, basis for the jam heuristic.
        private readonly Dictionary<ProductToken, Vector3> _lastPositions = new Dictionary<ProductToken, Vector3>();

        private readonly List<ProductToken> _scratch = new List<ProductToken>();

        private BoxCollider _surface;
        private float _speed;
        private bool _isHeld;
        private bool _isJammed;
        private float _jamCheckTimer;
        private float _stuckTimer;
        private float _blockedFullTimer;

        private ConveyorInfo? _lastReportedInfo;
        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        public bool IsJammed => _isJammed;

        /// <summary>True while the motor is Running but the belt is paused (buffer waiting / downstream cannot accept).</summary>
        public bool IsHeld => _isHeld;

        /// <summary>
        /// True if the belt surface actually moves right now (plank visual, upstream readiness).
        /// For a buffer belt this only means "motor running" - use <see cref="IsSegmentMoving"/> per slot.
        /// </summary>
        public bool IsMoving => CurrentState == MachineState.Running && !_isHeld;

        /// <inheritdoc />
        /// <remarks>Buffer belt: can it take a product on its rear slot? Other belts: is it moving?</remarks>
        public bool IsReadyForInfeed => _accumulationZone != null
            ? CurrentState == MachineState.Running && _accumulationZone.CanAccept
            : IsMoving;

        /// <summary>Buffer belt: whether slot zone <paramref name="slot"/> runs. Other belts: same as <see cref="IsMoving"/>.</summary>
        public bool IsSegmentMoving(int slot) => _accumulationZone != null
            ? CurrentState == MachineState.Running && _accumulationZone.IsSegmentMoving(slot)
            : IsMoving;

        public float CurrentSpeedMetersPerSecond => _speed;
        public Vector3 TravelDirection => _beltDirectionReference.forward;
        public int ProductCountOnBelt => _colliderCounts.Count;
        public AccumulationZone AccumulationZone => _accumulationZone;
        public MachineBase Downstream => _downstream;
        public SO_ConveyorConfig Config => _config;

        // ---- Unity lifecycle ----

        private void Awake()
        {
            if (_beltDirectionReference == null)
            {
                _beltDirectionReference = transform;
            }

            _surface = GetComponent<BoxCollider>();
            _surface.isTrigger = true;
            _speed = _config.BeltSpeedMetersPerSecond;

            if (_accumulationZone != null && _downstream != null)
            {
                _accumulationZone.SetDownstream(_downstream);
            }
        }

        private void OnEnable()
        {
            foreach (Content<ConveyorInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged += content.SetInfo;
                }
            }
        }

        private void OnDisable()
        {
            foreach (Content<ConveyorInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged -= content.SetInfo;
                }
            }
        }

        private void FixedUpdate()
        {
            if (CurrentState != MachineState.Running)
            {
                return;
            }

            PruneDestroyedProducts();
            float deltaTime = Time.fixedDeltaTime;

            if (_accumulationZone != null)
            {
                _accumulationZone.Evaluate(_colliderCounts.Keys, isBeltRunning: true);
                UpdateBufferFault(deltaTime);
                if (CurrentState != MachineState.Running)
                {
                    return; // BufferFull fault was just raised.
                }

                UpdateWarning();
                DriveProducts(TravelDirection * _speed);
                CheckForJam(deltaTime);
                return;
            }

            UpdateHeldState();
            UpdateWarning();

            if (_isHeld)
            {
                DriveProducts(Vector3.zero);
                ResetJamTracking();
                return;
            }

            DriveProducts(TravelDirection * _speed);
            CheckForJam(deltaTime);
        }

        // ---- Public API ----

        /// <summary>
        /// Wires the next element of the line and passes it on to the accumulation zone.
        /// Called by ConveyorLineController when auto-wiring.
        /// </summary>
        public void SetDownstream(MachineBase downstream)
        {
            _downstream = downstream;

            if (_accumulationZone != null)
            {
                _accumulationZone.SetDownstream(downstream);
            }
        }

        /// <summary>Sets the belt speed (line controller, tunnel station). Clamped to the config range.</summary>
        public void SetSpeed(float metersPerSecond)
        {
            _speed = Mathf.Clamp(metersPerSecond,
                _config.MinBeltSpeedMetersPerSecond, _config.MaxBeltSpeedMetersPerSecond);
        }

        // ---- Flow control ----

        private void UpdateHeldState()
        {
            bool isProductWaiting = _dischargeSensor == null || _dischargeSensor.CurrentValue;
            bool shouldHold = _downstream != null && isProductWaiting
                && !InfeedReadinessUtility.IsReady(_downstream, null);

            if (shouldHold == _isHeld)
            {
                return;
            }

            _isHeld = shouldHold;
            Report(_isHeld ? ConveyorInfo.HeldByDownstream : ConveyorInfo.Running);
        }

        private void UpdateBufferFault(float deltaTime)
        {
            if (!_accumulationZone.IsBlockedFull)
            {
                _blockedFullTimer = 0f;
                return;
            }

            _blockedFullTimer += deltaTime;
            if (_blockedFullTimer >= _accumulationZone.Config.FullFaultDelaySeconds)
            {
                _blockedFullTimer = 0f;
                TriggerFault(FaultReasonBufferFull);
            }
        }

        private void UpdateWarning()
        {
            if (_accumulationZone == null)
            {
                SetWarning(_isHeld, WarningReasonHeldByDownstream);
                return;
            }

            if (_accumulationZone.IsFull)
            {
                SetWarning(true, WarningReasonBufferFull);
                Report(ConveyorInfo.BufferFull);
            }
            else if (_accumulationZone.IsAboveWarningLevel)
            {
                SetWarning(true, WarningReasonBufferFilling);
                Report(ConveyorInfo.BufferFilling);
            }
            else
            {
                SetWarning(false);
                Report(ConveyorInfo.Running);
            }
        }

        /// <summary>Remaining seconds until a blocked full buffer turns into a fault (for HMI countdowns).</summary>
        public float SecondsUntilBufferFault => _accumulationZone != null && _accumulationZone.IsBlockedFull
            ? Mathf.Max(0f, _accumulationZone.Config.FullFaultDelaySeconds - _blockedFullTimer)
            : float.PositiveInfinity;

        // ---- Product transport ----

        private void DriveProducts(Vector3 horizontalVelocity)
        {
            foreach (ProductToken token in _colliderCounts.Keys)
            {
                if (token == null || !IsDrivenByThisBelt(token)
                    || !token.TryGetComponent(out Rigidbody body) || body.isKinematic)
                {
                    continue;
                }

                // Buffer belt: the zone under a waiting product stands, so the product stands.
                Vector3 velocity = _accumulationZone != null && !_accumulationZone.ShouldMove(token)
                    ? Vector3.zero
                    : horizontalVelocity;

                if (velocity != Vector3.zero && _config.CenterProducts)
                {
                    velocity += CenteringVelocity(token.transform.position, velocity.magnitude);
                }

                // Vertical component (gravity, impact rebound) stays real physics,
                // only the horizontal motion is set explicitly.
                Vector3 vertical = Vector3.Project(body.linearVelocity, Vector3.up);
                body.linearVelocity = velocity + vertical;
            }
        }

        /// <summary>
        /// A product is driven by the belt its centre lies over (horizontal footprint of the trigger),
        /// so two adjacent belts never fight over the same product.
        /// </summary>
        private bool IsDrivenByThisBelt(ProductToken token)
        {
            Vector3 local = _surface.transform.InverseTransformPoint(token.transform.position) - _surface.center;
            Vector3 half = _surface.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>
        /// Lateral correction toward the belt centre line (side guides). Limited to the belt speed so a
        /// product is never thrown sideways.
        /// </summary>
        private Vector3 CenteringVelocity(Vector3 productPosition, float beltSpeed)
        {
            Vector3 side = _surface.transform.right;
            Vector3 centre = _surface.transform.TransformPoint(_surface.center);
            float lateralOffset = Vector3.Dot(productPosition - centre, side);
            float lateralSpeed = Mathf.Clamp(-lateralOffset * _config.CenteringRate, -beltSpeed, beltSpeed);
            return side * lateralSpeed;
        }

        private void StopAllProducts()
        {
            if (_accumulationZone != null)
            {
                _accumulationZone.Halt();
            }

            DriveProducts(Vector3.zero);
        }

        // ---- Jam detection ----

        private void CheckForJam(float deltaTime)
        {
            _jamCheckTimer += deltaTime;
            if (_jamCheckTimer < _config.JamCheckIntervalSeconds)
            {
                return;
            }
            _jamCheckTimer = 0f;

            bool anyProductStuck = false;

            foreach (ProductToken token in _colliderCounts.Keys)
            {
                if (token == null)
                {
                    continue;
                }

                Vector3 currentPosition = token.transform.position;
                bool isExempt = !IsDrivenByThisBelt(token)
                    || (token.TryGetComponent(out Rigidbody body) && body.isKinematic)
                    || (_accumulationZone != null && !_accumulationZone.ShouldMove(token));

                if (!isExempt && _lastPositions.TryGetValue(token, out Vector3 lastPosition)
                    && Vector3.Distance(lastPosition, currentPosition) < _config.JamPositionThresholdMeters)
                {
                    anyProductStuck = true;
                }

                _lastPositions[token] = currentPosition;
            }

            if (!anyProductStuck)
            {
                _stuckTimer = 0f;
                return;
            }

            _stuckTimer += _config.JamCheckIntervalSeconds;
            if (_stuckTimer >= _config.JamTimeToTriggerSeconds && !_isJammed)
            {
                _isJammed = true;
                Report(ConveyorInfo.Jammed, force: true);
                TriggerFault(FaultReasonJam);
            }
        }

        private void ResetJamTracking()
        {
            _jamCheckTimer = 0f;
            _stuckTimer = 0f;
            _lastPositions.Clear();
        }

        private void PruneDestroyedProducts()
        {
            _scratch.Clear();
            foreach (ProductToken token in _colliderCounts.Keys)
            {
                if (token == null)
                {
                    _scratch.Add(token);
                }
            }

            foreach (ProductToken token in _scratch)
            {
                _colliderCounts.Remove(token);
                _lastPositions.Remove(token);
            }
        }

        // ---- Trigger tracking ----

        private void OnTriggerEnter(Collider other)
        {
            ProductToken token = ProductColliderUtility.FindToken(other);
            if (token == null)
            {
                return;
            }

            _colliderCounts.TryGetValue(token, out int count);
            _colliderCounts[token] = count + 1;
        }

        private void OnTriggerExit(Collider other)
        {
            ProductToken token = ProductColliderUtility.FindToken(other);
            if (token == null || !_colliderCounts.TryGetValue(token, out int count))
            {
                return;
            }

            if (count <= 1)
            {
                _colliderCounts.Remove(token);
                _lastPositions.Remove(token);
            }
            else
            {
                _colliderCounts[token] = count - 1;
            }
        }

        // ---- Content messages ----

        private void Report(ConveyorInfo info, bool force = false)
        {
            if (!force && _lastReportedInfo == info)
            {
                return;
            }

            _lastReportedInfo = info;
            string localized = _contents.Find(c => c.State == info)?.Info;
            NotifyContentChanged(string.IsNullOrEmpty(localized) ? FallbackText(info) : localized);
        }

        /// <summary>Used until the localization keys exist in the table.</summary>
        private static string FallbackText(ConveyorInfo info) => info switch
        {
            ConveyorInfo.HeldByDownstream => "Paused - next station cannot accept",
            ConveyorInfo.BufferFilling => "Buffer filling",
            ConveyorInfo.BufferFull => "Buffer full",
            ConveyorInfo.Jammed => "Jam detected",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady() => Report(ConveyorInfo.Ready, force: true);

        protected override void OnEnterRunning()
        {
            _isHeld = false;
            ResetJamTracking();
            Report(ConveyorInfo.Running, force: true);
        }

        protected override void OnEnterStopped() => Report(ConveyorInfo.Stopped, force: true);

        protected override void OnEnterStarting()
        {
            Report(ConveyorInfo.Starting, force: true);
            RestartRoutine(ref _startingRoutine, TransitionAfter(_config.StartupDurationSeconds, MachineState.Running));
        }

        protected override void OnEnterStopping()
        {
            Report(ConveyorInfo.Stopping, force: true);
            StopAllProducts();
            SetWarning(false);
            RestartRoutine(ref _stoppingRoutine, TransitionAfter(_config.ShutdownDurationSeconds, MachineState.Stopped));
        }

        protected override void OnEnterFault(string reason)
        {
            if (reason == FaultReasonBufferFull)
            {
                Report(ConveyorInfo.BufferFull, force: true);
            }
            else if (reason != FaultReasonJam)
            {
                Report(ConveyorInfo.Fault, force: true);
            }

            SetWarning(false); // The fault supersedes any warning.
            StopAllProducts();
            StopRoutine(ref _startingRoutine);
            StopRoutine(ref _stoppingRoutine);
        }

        protected override void OnEnterMaintenance()
        {
            // Operator is clearing the belt: forget all fault bookkeeping.
            _isJammed = false;
            _isHeld = false;
            _blockedFullTimer = 0f;
            ResetJamTracking();
            SetWarning(false);
            Report(ConveyorInfo.Maintenance, force: true);
        }

        private IEnumerator TransitionAfter(float seconds, MachineState target)
        {
            if (seconds > 0f)
            {
                yield return new WaitForSeconds(seconds);
            }
            else
            {
                yield return null;
            }

            SetState(target);
        }

        private void RestartRoutine(ref Coroutine routine, IEnumerator body)
        {
            StopRoutine(ref routine);
            routine = StartCoroutine(body);
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform reference = _beltDirectionReference != null ? _beltDirectionReference : transform;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(reference.position, reference.forward * 0.75f);
        }
    }
}
