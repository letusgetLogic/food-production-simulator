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
    ///   4. While the piston is in contact, the dough is flattened (scale). At the end of the stroke
    ///      the product becomes FormedPizza and the operator's diameter/thickness are recorded.
    ///   5. The product is released at <see cref="_outputPoint"/> (start of the outgoing belt). If the
    ///      optional output sensor is occupied, the product stays in the press and no new portion is
    ///      taken - the incoming belt backs up physically (domino effect).
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
    public class PressMachine : MachineBase
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

        [SerializeField] private SO_PressConfig _config;

        [Header("Piston")]
        [Tooltip("Legacy Animation component on piston-thin-round (added by the glTFast importer). Auto-filled from children if empty.")]
        [SerializeField] private Animation _pistonAnimation;

        [Tooltip("Name of the press clip on the Animation component ('toggle' = down and back up in one stroke).")]
        [SerializeField] private string _pressClipName = "toggle";

        [Header("Press Zone")]
        [Tooltip("Position under the piston where the dough is pressed (on the belt surface).")]
        [SerializeField] private Transform _pressPoint;

        [Header("Output")]
        [Tooltip("Start of the outgoing belt. The formed pizza is released here.")]
        [SerializeField] private Transform _outputPoint;

        [Tooltip("Optional: PresenceSensor (trigger) at the output point. While occupied, the pizza stays in the press (backpressure).")]
        [SerializeField] private PresenceSensor _outputSensor;

        [Header("HMI Content")]
        [SerializeField] private List<Content<PressInfo>> _contents = new List<Content<PressInfo>>();

        private readonly Collider[] _overlapBuffer = new Collider[8];

        private float _pressSpeed;
        private float _targetDiameterCm;
        private float _targetThicknessMm;

        private ProductToken _heldProduct;
        private Rigidbody _heldBody;
        private Renderer[] _heldRenderers;
        private Vector3 _startScale;
        private float _groundY;
        private float _strokeTime;
        private float _squash;
        private bool _isPressing;

        private AnimationState _pressState;

        private PressInfo? _lastReportedInfo;
        private string _lastReportedDetail;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        /// <summary>Raised when a pizza base has been formed (before it is released to the output).</summary>
        public event Action<ProductInstance> ProductFormed;

        public float PressSpeed => _pressSpeed;
        public float TargetDiameterCm => _targetDiameterCm;
        public float TargetThicknessMm => _targetThicknessMm;
        public bool IsPressing => _isPressing;
        public bool IsHoldingProduct => _heldProduct != null;
        public bool IsOutputBlocked => _outputSensor != null && _outputSensor.CurrentValue;

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

            token.transform.SetPositionAndRotation(_pressPoint.position, _pressPoint.rotation);
            _heldRenderers = token.GetComponentsInChildren<Renderer>();
            _startScale = token.transform.localScale;
            _groundY = GetBoundsMinY();

            _strokeTime = 0f;
            _squash = 0f;
            _isPressing = true;
            Report(PressInfo.Pressing, force: true);
        }

        private void AdvanceStroke(float deltaTime)
        {
            _strokeTime += deltaTime * _pressSpeed;
            float clampedTime = Mathf.Min(_strokeTime, ClipLength);
            SamplePiston(clampedTime);

            // Dough only gets flatter, never springs back while the piston rises again.
            float normalized = clampedTime / ClipLength;
            float contact = Mathf.InverseLerp(_config.ContactStartNormalized, _config.ContactEndNormalized, normalized);
            if (contact > _squash)
            {
                _squash = contact;
                ApplyDeformation();
            }

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

            if (_outputPoint != null)
            {
                _heldProduct.transform.position = _outputPoint.position;
            }
            else
            {
                Debug.LogWarning($"{name}: no output point assigned - pizza is released in the press.", this);
            }

            if (_heldBody != null)
            {
                _heldBody.isKinematic = false;
            }

            _heldProduct = null;
            _heldBody = null;
            _heldRenderers = null;
        }

        private void ApplyDeformation()
        {
            float widthFactor = _targetDiameterCm / _config.DefaultTargetDiameterCm;
            float heightFactor = _targetThicknessMm / _config.DefaultTargetThicknessMm;
            Vector3 multiplier = _config.FormedScaleMultiplier;
            Vector3 formedScale = Vector3.Scale(_startScale, new Vector3(
                multiplier.x * widthFactor,
                multiplier.y * heightFactor,
                multiplier.z * widthFactor));

            Transform product = _heldProduct.transform;
            product.localScale = Vector3.Lerp(_startScale, formedScale, _squash);

            // Keep the underside on the belt while the dough flattens.
            float offset = _groundY - GetBoundsMinY();
            product.position += Vector3.up * offset;
        }

        private float GetBoundsMinY()
        {
            if (_heldRenderers == null || _heldRenderers.Length == 0)
            {
                return _heldProduct.transform.position.y;
            }

            float minY = float.MaxValue;
            foreach (Renderer r in _heldRenderers)
            {
                if (r != null)
                {
                    minY = Mathf.Min(minY, r.bounds.min.y);
                }
            }
            return minY;
        }

        private ProductToken FindProductInPressZone()
        {
            if (_pressPoint == null)
            {
                return null;
            }

            int count = Physics.OverlapBoxNonAlloc(
                _pressPoint.position, _config.PressZoneHalfExtents, _overlapBuffer,
                _pressPoint.rotation, _config.ProductLayer, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                ProductToken token = _overlapBuffer[i].GetComponentInParent<ProductToken>();
                if (token != null && token.Product != null)
                {
                    // Products still being carried by a worker are ignored.
                    Rigidbody body = token.GetComponent<Rigidbody>();
                    if (body != null && body.isKinematic)
                    {
                        continue;
                    }
                    return token;
                }
            }
            return null;
        }

        // ---- Piston animation (legacy Animation, manually sampled) ----

        private void AutoAssignAnimation()
        {
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
            // Minimal safeguard only, same as the other stations - real fault handling is still
            // open (FaultSystem, Week 2). A stroke in progress freezes and resumes after restart.
            if (_lastReportedInfo != PressInfo.WrongProduct)
            {
                Report(PressInfo.Fault, force: true);
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

            if (_outputPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_outputPoint.position, 0.1f);
            }
        }
    }
}
