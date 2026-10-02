using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Third production station: presses PortionedDough into a FormedPizza.
    ///
    /// Material flow at this station:
    ///   1. The portion arrives physically on the belt (Portionierer -> Formanlage conveyor) and
    ///      comes to rest in the press zone below the piston (<see cref="_pressPoint"/>).
    ///   2. While Running, the machine takes the product (kinematic, snapped to the press point)
    ///      and plays one stroke of the piston clip (piston-thin-round, clip "toggle").
    ///   3. The stroke speed is regulated by <see cref="SetPressSpeed"/> (operator/HMI). The clip is
    ///      sampled manually on the legacy Animation component, so the speed also defines the cycle time:
    ///      cycle = clipLength / pressSpeed. Changing the speed mid-stroke takes effect immediately.
    ///   4. When the piston top touches the dough, the pizza prefab switches its own visual to
    ///      FormedPizza (PizzaStateVisual + ProductStateTrigger on the piston top). At the end of the
    ///      stroke the machine confirms FormedPizza and records the operator's diameter/thickness.
    ///   5. The formed pizza is released where it is (non-kinematic again) and the belt under the press
    ///      carries it on - no teleport to an output point. Already pressed products (FormedPizza and
    ///      later) passing through the press zone are ignored. If the optional output sensor is occupied,
    ///      the pizza stays in the press until it is free.
    ///
    /// Deliberately does NOT implement IProductProcessor: products are handed over by physics
    /// (belt), not through TryBeginProcessing/TryCollectProcessedProduct.
    ///
    /// No quality evaluation yet: there is no dimension sensor. Diameter/thickness are only
    /// recorded on the ProductInstance for the future QualitySystem (Woche 2).
    ///
    /// The piston model (piston-thin-round.glb, glTFast import) carries a legacy Animation component
    /// with the clips toggle-on / toggle-off / toggle. The machine drives the "toggle" state's time
    /// itself (speed 0 + Sample), so no AnimatorController is needed.
    /// </summary>
    public class PressMachine : MachineBase, IMachineParameterSource
    {
        /// <summary>Content messages this machine reports via NotifyContentChanged.</summary>
        public enum PressInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            WaitingForDough,
            Pressing,
            Formed,
            OutputBlocked,
            WrongProduct
        }

        public const string FaultReasonWrongProduct = "WrongProduct";
        public const string RejectReasonWrongProduct = "WrongProduct";
        public const string RejectReasonPressInterrupted = "PressInterrupted";

        [SerializeField] private SO_PressConfig _config;

        [Header("Piston")]
        [Tooltip("Legacy Animation component on piston-thin-round (added by the glTFast importer). Auto-filled from children if empty.")]
        [SerializeField] private Animation _pistonAnimation;

        [Tooltip("Name of the press clip on the Animation component ('toggle' = down and back up in one stroke).")]
        [SerializeField] private string _pressClipName = "toggle";

        [Header("Press Zone")]
        [Tooltip("Position under the piston where the dough is pressed (on the belt surface).")]
        [SerializeField] private Transform _pressPoint;

        [Tooltip("Trigger collider under the piston top (child 'TriggerZone' of the piston). The stroke starts as soon as the " +
                 "pizza's ContactCollider touches it. Auto-filled from children if empty. Without it, the press falls back to " +
                 "'product centre within CenterToleranceMeters of the press point'.")]
        [SerializeField] private Collider _pistonTriggerZone;


        [Header("Output")]
        [Tooltip("Optional: PresenceSensor (trigger) downstream of the press on the belt. While occupied, the formed pizza stays in the press (backpressure).")]
        [SerializeField] private PresenceSensor _outputSensor;

        [Header("HMI Content")]
        [SerializeField] private List<Content<PressInfo>> _contents = new List<Content<PressInfo>>();

        private readonly Collider[] _overlapBuffer = new Collider[8];

        private float _pressSpeed;
        private float _targetDiameterCm;
        private float _targetThicknessMm;

        private ProductToken _heldProduct;
        private Rigidbody _heldBody;
        private float _strokeTime;
        private bool _isPressing;

        private AnimationState _pressState;

        private PressInfo? _lastReportedInfo;
        private string _lastReportedDetail;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        private int _formedCount;
        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

        /// <summary>Raised when a pizza base has been formed (before it is released to the output).</summary>
        public event Action<ProductInstance> ProductFormed;

        public float PressSpeed => _pressSpeed;
        public float TargetDiameterCm => _targetDiameterCm;
        public float TargetThicknessMm => _targetThicknessMm;
        public bool IsPressing => _isPressing;
        public bool IsHoldingProduct => _heldProduct != null;
        public bool IsOutputBlocked => _outputSensor != null && _outputSensor.CurrentValue;

        /// <summary>Number of pizza bases formed since scene start.</summary>
        public int FormedCount => _formedCount;

        /// <summary>Duration of one forming cycle at the current speed (for the HMI "Zykluszeit" readout).</summary>
        public float CycleTimeSeconds => ClipLength / _pressSpeed;

        /// <summary>0..1 progress of the current stroke.</summary>
        public float StrokeProgress01 => _isPressing ? Mathf.Clamp01(_strokeTime / ClipLength) : 0f;

        private float ClipLength => _pressState != null && _pressState.length > 0f ? _pressState.length : 0.5f;

        // ---- Unity lifecycle ----

        private void Reset() => AutoAssignAnimation();

        private void OnValidate() => AutoAssignAnimation();

        private void Awake()
        {
            AutoAssignAnimation();

            _pressSpeed = _config.DefaultPressSpeed;
            _targetDiameterCm = _config.DefaultTargetDiameterCm;
            _targetThicknessMm = _config.DefaultTargetThicknessMm;

            SetupPistonAnimation();
            SamplePiston(0f); // retracted rest pose
        }

        private void OnEnable()
        {
            foreach (Content<PressInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged += content.SetInfo;
                }
            }
        }

        private void OnDisable()
        {
            foreach (Content<PressInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged -= content.SetInfo;
                }
            }
        }

        private void Update()
        {
            if (CurrentState != MachineState.Running)
            {
                // Stroke freezes in place while not running and resumes on restart.
                return;
            }

            if (_heldProduct == null)
            {
                TryTakeProduct();
                return;
            }

            if (_isPressing)
            {
                AdvanceStroke(Time.deltaTime);
                return;
            }

            TryReleaseToOutput();
        }

        // ---- HMI parameters ----

        /// <summary>
        /// Operator setting: piston animation speed (1 = original clip speed).
        /// Also defines the cycle time (clipLength / speed). Applies immediately, even mid-stroke.
        /// </summary>
        public void SetPressSpeed(float speed)
        {
            _pressSpeed = Mathf.Clamp(speed, _config.MinPressSpeed, _config.MaxPressSpeed);
        }

        /// <summary>Convenience for the HMI: set the speed via the desired cycle time in seconds.</summary>
        public void SetCycleTime(float seconds)
        {
            SetPressSpeed(ClipLength / Mathf.Max(0.01f, seconds));
        }

        /// <summary>Set by the operator at the HMI, read off the recipe terminal.</summary>
        public void SetTargetDiameter(float diameterCm)
        {
            _targetDiameterCm = Mathf.Clamp(diameterCm, _config.MinDiameterCm, _config.MaxDiameterCm);
        }

        /// <summary>Set by the operator at the HMI, read off the recipe terminal.</summary>
        public void SetTargetThickness(float thicknessMm)
        {
            _targetThicknessMm = Mathf.Clamp(thicknessMm, _config.MinThicknessMm, _config.MaxThicknessMm);
        }

        // ---- HMI terminal values (IMachineParameterSource) ----

        /// <inheritdoc />
        public IReadOnlyList<MachineParameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    BuildHmiValues();
                }
                return _parameters;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<MachineReadout> Readouts
        {
            get
            {
                if (_readouts == null)
                {
                    BuildHmiValues();
                }
                return _readouts;
            }
        }

        private void BuildHmiValues()
        {
            // The operator sets the cycle time; it maps onto the press speed (cycle = clipLength / speed),
            // so its limits follow from the speed limits in SO_PressConfig.
            float minCycle = ClipLength / _config.MaxPressSpeed;
            float maxCycle = ClipLength / _config.MinPressSpeed;

            // TODO localization: labels are English fallbacks until keys exist in the table.
            _parameters = new List<MachineParameter>
            {
                new MachineParameter("cycleTime", "Cycle time", "s",
                    minCycle, maxCycle, _config.CycleTimeStepSeconds, "0.0",
                    () => CycleTimeSeconds, SetCycleTime),
                new MachineParameter("diameter", "Diameter", "cm",
                    _config.MinDiameterCm, _config.MaxDiameterCm, _config.DiameterStepCm, "0.0",
                    () => _targetDiameterCm, SetTargetDiameter),
                new MachineParameter("thickness", "Thickness", "mm",
                    _config.MinThicknessMm, _config.MaxThicknessMm, _config.ThicknessStepMm, "0.0",
                    () => _targetThicknessMm, SetTargetThickness),
            };

            _readouts = new List<MachineReadout>
            {
                new MachineReadout("cycleTime", "Cycle time", "s",
                    () => CycleTimeSeconds.ToString("0.0"),
                    () => MachineValueLevel.Normal, MachineReadoutSlot.CycleTime),
                new MachineReadout("setpoint", "Setpoint", "",
                    () => $"\u00d8{_targetDiameterCm:0.#} cm / {_targetThicknessMm:0.#} mm",
                    () => MachineValueLevel.Normal, MachineReadoutSlot.Setpoint),
                new MachineReadout("stroke", "Stroke", "%",
                    () => _isPressing ? (StrokeProgress01 * 100f).ToString("0") : "--",
                    () => _isPressing ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
                new MachineReadout("productInPress", "Dough in press", "",
                    () => IsHoldingProduct ? "Yes" : "No",
                    () => IsHoldingProduct ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
                new MachineReadout("formedCount", "Bases formed", "pcs",
                    () => _formedCount.ToString()),
                new MachineReadout("output", "Output", "",
                    () => IsOutputBlocked ? "Blocked" : "Free",
                    () => IsOutputBlocked ? MachineValueLevel.Warning : MachineValueLevel.Normal),
            };
        }

        // ---- Forming cycle ----

        private void TryTakeProduct()
        {
            ProductToken token = FindProductInPressZone();
            if (token == null)
            {
                Report(PressInfo.WaitingForDough);
                return;
            }

            if (token.Product.CurrentState != ProductState.PortionedDough)
            {
                // Marked as scrap so it is ignored after maintenance and rides out of the press
                // instead of faulting the press again on every restart.
                token.Product.Reject(RejectReasonWrongProduct);
                Report(PressInfo.WrongProduct, force: true);
                TriggerFault($"WrongProduct:{token.Product.CurrentState}");
                return;
            }

            _heldProduct = token;
            _heldBody = token.GetComponent<Rigidbody>();
            if (_heldBody != null)
            {
                _heldBody.linearVelocity = Vector3.zero;
                _heldBody.angularVelocity = Vector3.zero;
                _heldBody.isKinematic = true;
            }

            // Centre under the piston, but keep the height the product has on the belt - so it can
            // simply ride on after pressing instead of dropping from the press point.
            Vector3 centre = _pressPoint.position;
            centre.y = token.transform.position.y;
            token.transform.SetPositionAndRotation(centre, _pressPoint.rotation);

            _strokeTime = 0f;
            _isPressing = true;
            Report(PressInfo.Pressing, force: true);
        }

        private void AdvanceStroke(float deltaTime)
        {
            _strokeTime += deltaTime * _pressSpeed;
            float clampedTime = Mathf.Min(_strokeTime, ClipLength);
            SamplePiston(clampedTime);

            if (_strokeTime >= ClipLength)
            {
                FinishForming();
            }
        }

        private void FinishForming()
        {
            _isPressing = false;
            SamplePiston(0f);

            ProductInstance product = _heldProduct.Product;
            product.CurrentState = ProductState.FormedPizza;
            product.FormedDiameterCm = _targetDiameterCm;
            product.FormedThicknessMm = _targetThicknessMm;

            _formedCount++;
            ProductFormed?.Invoke(product);
            Report(PressInfo.Formed, force: true);

            TryReleaseToOutput();
        }

        private void TryReleaseToOutput()
        {
            if (IsOutputBlocked)
            {
                Report(PressInfo.OutputBlocked);
                return;
            }

            // Released in place: the belt under the press carries the formed pizza on.
            if (_heldBody != null)
            {
                _heldBody.isKinematic = false;
                _heldBody.linearVelocity = Vector3.zero;
                _heldBody.angularVelocity = Vector3.zero;
            }

            _heldProduct = null;
            _heldBody = null;
        }

        private ProductToken FindProductInPressZone()
        {
            if (_pressPoint == null)
            {
                return null;
            }

            return _pistonTriggerZone != null ? FindProductAtTriggerZone() : FindCentredProductInPressZone();
        }

        /// <summary>
        /// The pizza's ContactCollider (its trigger collider) touches the piston's TriggerZone.
        /// </summary>
        private ProductToken FindProductAtTriggerZone()
        {
            Bounds zone = _pistonTriggerZone.bounds;
            int count = Physics.OverlapBoxNonAlloc(
                zone.center, zone.extents, _overlapBuffer, Quaternion.identity,
                _config.ProductLayer, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider hit = _overlapBuffer[i];
                if (hit == _pistonTriggerZone || !hit.isTrigger)
                {
                    continue; // only the product's ContactCollider counts
                }

                ProductToken token = ProductColliderUtility.FindToken(hit);
                if (IsPressable(token))
                {
                    return token;
                }
            }
            return null;
        }

        /// <summary>Fallback without TriggerZone: product centre close enough to the press point.</summary>
        private ProductToken FindCentredProductInPressZone()
        {
            int count = Physics.OverlapBoxNonAlloc(
                _pressPoint.position, _config.PressZoneHalfExtents, _overlapBuffer,
                _pressPoint.rotation, _config.ProductLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                ProductToken token = ProductColliderUtility.FindToken(_overlapBuffer[i]);
                if (!IsPressable(token))
                {
                    continue;
                }

                Vector3 offset = token.transform.position - _pressPoint.position;
                offset.y = 0f;
                if (offset.magnitude <= _config.CenterToleranceMeters)
                {
                    return token;
                }
            }
            return null;
        }

        private static bool IsPressable(ProductToken token)
        {
            if (token == null || token.Product == null)
            {
                return false;
            }

            // Already pressed (or further), or already scrap - just passing out of the press on the belt.
            if (token.Product.CurrentState > ProductState.PortionedDough || token.Product.IsRejected)
            {
                return false;
            }

            // Products carried by a worker / already held are ignored.
            return !(token.TryGetComponent(out Rigidbody body) && body.isKinematic);
        }

        // ---- Piston animation (legacy Animation, manually sampled) ----

        private void AutoAssignAnimation()
        {
            if (_pistonTriggerZone == null)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "TriggerZone" && child.TryGetComponent(out Collider zone))
                    {
                        _pistonTriggerZone = zone;
                        break;
                    }
                }
            }

            if (_pistonAnimation == null)
            {
                _pistonAnimation = GetComponentInChildren<Animation>(true);
            }
        }

        private void SetupPistonAnimation()
        {
            if (_pistonAnimation == null)
            {
                Debug.LogWarning($"{name}: no piston Animation component found - pressing runs without animation.", this);
                return;
            }

            _pressState = _pistonAnimation[_pressClipName];
            if (_pressState == null)
            {
                Debug.LogWarning($"{name}: clip '{_pressClipName}' not found on {_pistonAnimation.name} - pressing runs without animation.", this);
                return;
            }

            // The machine owns the timing: no auto-play, the state is only advanced by AdvanceStroke().
            _pistonAnimation.playAutomatically = false;
            _pistonAnimation.Stop();
            _pressState.wrapMode = WrapMode.ClampForever;
            _pressState.speed = 0f;
            _pressState.weight = 1f;
            _pressState.enabled = true;
        }

        private void SamplePiston(float time)
        {
            if (_pressState == null)
            {
                return;
            }

            _pressState.enabled = true;
            _pressState.time = time;
            _pistonAnimation.Sample();
        }

        // ---- Content messages ----

        private void Report(PressInfo info, string detail = null, bool force = false)
        {
            if (!force && _lastReportedInfo == info && _lastReportedDetail == detail)
            {
                return;
            }

            _lastReportedInfo = info;
            _lastReportedDetail = detail;
            NotifyContentChanged(string.IsNullOrEmpty(detail) ? Info(info) : detail + "\n" + Info(info));
        }

        private string Info(PressInfo info)
        {
            string localized = _contents.Find(c => c.State == info)?.Info;
            return string.IsNullOrEmpty(localized) ? FallbackText(info) : localized;
        }

        /// <summary>Used until the localization keys exist in the table.</summary>
        private static string FallbackText(PressInfo info) => info switch
        {
            PressInfo.WaitingForDough => "Waiting for dough",
            PressInfo.Pressing => "Pressing",
            PressInfo.Formed => "Pizza base formed",
            PressInfo.OutputBlocked => "Output blocked",
            PressInfo.WrongProduct => "Wrong product in press",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady() => Report(PressInfo.Ready, force: true);

        protected override void OnEnterRunning() => Report(PressInfo.Running, force: true);

        protected override void OnEnterStopped() => Report(PressInfo.Stopped, force: true);

        protected override void OnEnterMaintenance() => Report(PressInfo.Maintenance, force: true);

        protected override void OnEnterStarting()
        {
            Report(PressInfo.Starting, force: true);
            if (_startingRoutine != null)
            {
                StopCoroutine(_startingRoutine);
            }
            _startingRoutine = StartCoroutine(StartingRoutine());
        }

        protected override void OnEnterStopping()
        {
            Report(PressInfo.Stopping, force: true);
            if (_stoppingRoutine != null)
            {
                StopCoroutine(_stoppingRoutine);
            }
            _stoppingRoutine = StartCoroutine(StoppingRoutine());
        }

        protected override void OnEnterFault(string reason)
        {
            // FaultSystem rule "product held in a fault":
            //  - stroke in progress -> piston retracts, the half-pressed dough is released as scrap
            //    (RejectReason PressInterrupted) and rides out; it is never pressed a second time.
            //  - stroke finished, only waiting for a free output -> the formed base stays held and is
            //    released after the restart (it is a good product).
            if (_lastReportedInfo != PressInfo.WrongProduct)
            {
                Report(PressInfo.Fault, force: true);
            }

            if (_isPressing && _heldProduct != null)
            {
                _isPressing = false;
                SamplePiston(0f);
                _heldProduct.Product?.Reject(RejectReasonPressInterrupted);

                if (_heldBody != null)
                {
                    _heldBody.isKinematic = false;
                    _heldBody.linearVelocity = Vector3.zero;
                    _heldBody.angularVelocity = Vector3.zero;
                }

                _heldProduct = null;
                _heldBody = null;
            }

            if (_startingRoutine != null)
            {
                StopCoroutine(_startingRoutine);
                _startingRoutine = null;
            }
            if (_stoppingRoutine != null)
            {
                StopCoroutine(_stoppingRoutine);
                _stoppingRoutine = null;
            }
        }

        private IEnumerator StartingRoutine()
        {
            yield return new WaitForSeconds(_config.StartupDurationSeconds);
            SetState(MachineState.Running);
        }

        private IEnumerator StoppingRoutine()
        {
            yield return new WaitForSeconds(_config.ShutdownDurationSeconds);
            SetState(MachineState.Stopped);
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null || _pressPoint == null)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            Gizmos.matrix = Matrix4x4.TRS(_pressPoint.position, _pressPoint.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, _config.PressZoneHalfExtents * 2f);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
