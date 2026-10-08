using Game.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Second production station: draws dough from the <see cref="Hopper"/> and produces
    /// PortionedDough products by weight, spawned as physical prefabs with a ProductToken.
    ///
    /// Material flow at this station:
    ///   1. The worker puts the dough ball (from the mixer) into the pot on the lift platform.
    ///      The pot's PresenceSensor detects it -> platform colour + HMI content message.
    ///   2. The operator raises the lift (<see cref="TryRaiseLift"/>, LiftUpButton):
    ///      <see cref="PortionerLiftArm"/> moves up and tips the pot, the dough ball falls into the
    ///      hopper, the Hopper's intake trigger books its weight. <see cref="TryLowerLift"/>
    ///      (LiftDownButton) brings it back down.
    ///   3. While Running and the hopper holds at least one portion's weight, the machine
    ///      portions: after the portioning duration the hopper's dough balls shrink by the portion
    ///      weight and a portion prefab is instantiated at the spawn point with a fresh
    ///      ProductInstance (PortionedDough). A rest below one portion waits in the hopper
    ///      (shown on the terminal) until the next dough ball arrives.
    ///   4. <see cref="TryDrainHopper"/> (HopperDrainButton) lets the whole hopper content drop
    ///      out without creating products.
    ///
    /// The Portionierer is a source station
    /// (bulk dough in, individual products out), same as MixerMachine.
    ///
    /// No direct RecipeDefinition binding in code: SetTargetWeight()/SetToleranceGrams()/
    /// SetPortioningDuration() are set by the operator at the HMI, reading the recipe off the panel.
    /// </summary>
    public class PortionerMachine : MachineBase, IDownstreamLink, IMachineParameterSource, ISaveableState
    {
        /// <summary>Content messages this machine reports via NotifyContentChanged.</summary>
        public enum PortionerInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            PotLoaded,
            PotEmpty,
            Tipping,
            Tipped,
            LiftReturned,
            HopperEmpty,
            Portioning,
            PortionDispatched,
            OutputBlocked,
            LiftLowering,
            NotEnoughDough,
            HopperDraining
        }

        [SerializeField] private SO_PortionerConfig _config;

        [Header("Hopper")]
        [SerializeField] private Hopper _hopper;

        [Tooltip("Reports the hopper's NormalizedLevel via IFillLevelSource (same GameObject as the hopper). Portioning only runs while it detects content.")]
        [SerializeField] private LevelSensor _levelSensor;

        [Header("Lift / Pot")]
        [SerializeField] private PortionerLiftArm _liftArm;
        [SerializeField] private GameObject _platformCenter;

        [Tooltip("PotSensor inside the pot on the lift platform.")]
        [SerializeField] private PotSensor _potSensor;

        [Tooltip("Renderer of the lift platform; its colour signals whether the pot is loaded.")]
        [SerializeField] private Renderer _platformRenderer;

        [SerializeField] private Color _platformEmptyColor = new Color(0.55f, 0.55f, 0.55f);
        [SerializeField] private Color _platformLoadedColor = new Color(0.2f, 0.75f, 0.3f);

        [Header("Portion Output")]
        [Tooltip("Prefab of the portioned dough. Must carry a ProductToken.")]
        [SerializeField] private ProductToken _portionPrefab;

        [SerializeField] private Transform _portionSpawnPoint;

        [Tooltip("Optional: PresenceSensor at the spawn point. While it detects something, no new portion is produced (backpressure).")]
        [SerializeField] private PresenceSensor _outputSensor;

        [Tooltip("Optional: belt below the spawn point. While it cannot accept (paused because the buffer before the press is full), " +
                 "no new portion is produced - the portioner pauses and continues by itself. Set by ConveyorLineController when auto-wiring.")]
        [SerializeField] private MachineBase _downstream;

        [Header("HMI Content")]
        [SerializeField] private List<Content<PortionerInfo>> _contents = new List<Content<PortionerInfo>>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private MaterialPropertyBlock _platformPropertyBlock;

        private float _portioningDurationSeconds;
        private float _targetWeightGrams;
        private float _toleranceGrams;

        private float _processingTimer;
        private bool _isProcessing;
        private PortionerInfo? _lastReportedInfo;
        private string _lastReportedDetail;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        private int _portionsProduced;
        private float _lastPortionWeightGrams = -1f;
        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

        /// <summary>Raised whenever a portion has been instantiated at the spawn point.</summary>
        public event Action<ProductInstance> PortionDispatched;

        public bool IsPortioning => _isProcessing;

        public float TargetWeightGrams => _targetWeightGrams;
        public float ToleranceGrams => _toleranceGrams;
        public float PortioningDurationSeconds => _portioningDurationSeconds;
        public int PortionsProduced => _portionsProduced;

        /// <summary>Weight of the last dispatched portion, or a negative value if none was produced yet.</summary>
        public float LastPortionWeightGrams => _lastPortionWeightGrams;

        /// <summary>0..1 progress of the portion currently being produced.</summary>
        public float PortioningProgress01 => _isProcessing && _portioningDurationSeconds > 0f
            ? Mathf.Clamp01(1f - _processingTimer / _portioningDurationSeconds)
            : 0f;

        public bool IsPotLoaded => _potSensor != null && _potSensor.CurrentValue;

        private HoldInteractable _currentPlaced;

        /// <summary>
        /// "Level sensor still detects content": uses the LevelSensor reading if one is assigned,
        /// otherwise falls back to the hopper's own amount.
        /// </summary>
        public bool HopperHasContent => _levelSensor != null
            ? _levelSensor.CurrentValue > 0f
            : _hopper != null && _hopper.HasDough;

        public bool IsOutputBlocked => (_outputSensor != null && _outputSensor.CurrentValue)
            || (_downstream != null && !InfeedReadinessUtility.IsReady(_downstream, null));

        /// <inheritdoc />
        public void SetDownstream(MachineBase downstream) => _downstream = downstream;

        private void Awake()
        {
            _portioningDurationSeconds = _config.DefaultPortioningDurationSeconds;
            _targetWeightGrams = _config.DefaultTargetWeightGrams;
            _toleranceGrams = _config.DefaultToleranceGrams;
        }

        private void OnEnable()
        {
            if (_potSensor != null)
            {
                _potSensor.OnValueChanged += HandlePotPresenceChanged;
            }

            if (_liftArm != null)
            {
                _liftArm.PositionChanged += HandleLiftPositionChanged;
            }

            if (_hopper != null)
            {
                _hopper.DrainFinished += HandleDrainFinished;
            }

            ApplyPlatformColor(IsPotLoaded);
        }

        private void OnDisable()
        {
            if (_potSensor != null)
            {
                _potSensor.OnValueChanged -= HandlePotPresenceChanged;
            }

            if (_liftArm != null)
            {
                _liftArm.PositionChanged -= HandleLiftPositionChanged;
            }

            if (_hopper != null)
            {
                _hopper.DrainFinished -= HandleDrainFinished;
            }
        }

        private void Update()
        {
            UpdateHopperLevelEvaluation();

            if (CurrentState != MachineState.Running)
            {
                return;
            }

            if (_isProcessing)
            {
                _processingTimer -= Time.deltaTime;
                if (_processingTimer <= 0f)
                {
                    CompletePortioning();
                }
                return;
            }

            if (_hopper.IsDraining)
            {
                return;
            }

            if (!HopperHasContent)
            {
                Report(PortionerInfo.HopperEmpty);
                return;
            }

            float available = _hopper.CurrentAmountGrams;
            if (available < _targetWeightGrams)
            {
                // Rest below one portion: wait for the next dough ball, show how much is there.
                Report(PortionerInfo.NotEnoughDough, $"{available:0} / {_targetWeightGrams:0} g");
                return;
            }

            if (IsOutputBlocked)
            {
                Report(PortionerInfo.OutputBlocked);
                return;
            }

            BeginPortioning();
        }

        // ---- Lift / pot ----

        /// <summary>
        /// Operator action (LiftUpButton): raise the lift and tip the pot. Not possible in
        /// Fault/Maintenance; the lift itself doesn't care whether a pot is on it.
        /// </summary>
        public bool TryRaiseLift() => CanOperateLift() && _liftArm.TryRaise();

        /// <summary>Operator action (LiftDownButton): tilt the pot back and lower the lift.</summary>
        public bool TryLowerLift() => CanOperateLift() && _liftArm.TryLower();

        /// <summary>
        /// Operator action (HopperDrainButton): lets the whole hopper content drop out without
        /// creating products. A portion currently in progress is cancelled.
        /// </summary>
        public bool TryDrainHopper()
        {
            if (_hopper == null || CurrentState == MachineState.Maintenance)
            {
                return false;
            }

            _isProcessing = false;
            if (!_hopper.TryDrain())
            {
                return false;
            }

            Report(PortionerInfo.HopperDraining, force: true);
            return true;
        }

        private void HandleDrainFinished() => Report(PortionerInfo.HopperEmpty, force: true);

        private bool CanOperateLift()
        {
            if (CurrentState == MachineState.Fault || CurrentState == MachineState.Maintenance)
            {
                Debug.LogWarning($"{name}: lift operation rejected in state {CurrentState}.", this);
                return false;
            }

            if (_liftArm == null)
            {
                Debug.LogWarning($"{name}: no PortionerLiftArm assigned.", this);
                return false;
            }

            return true;
        }

        /// <summary>
        /// PotSensor Enter/Exit Trigger
        /// </summary>
        /// <param name="current"></param>
        /// <param name="next"></param>
        private void HandlePotPresenceChanged(HoldInteractable current, HoldInteractable next)
        {
            if (next != null && current == null)
            {
                next.OnReleased += SetPotOnLift;
            }

            if (current != null && next == null)
            {
                current.OnReleased -= SetPotOnLift;
            }

            bool isPresent = next != null;
            ApplyPlatformColor(isPresent);

            // While the lift is moving, the presence change is just the content leaving the pot.
            if (_liftArm == null || _liftArm.IsDown)
            {
                Report(isPresent ? PortionerInfo.PotLoaded : PortionerInfo.PotEmpty, force: true);
            }
        }

        private void HandleLiftPositionChanged(PortionerLiftArm.LiftPosition position)
        {
            switch (position)
            {
                case PortionerLiftArm.LiftPosition.Raising:
                    Report(PortionerInfo.Tipping, force: true);
                    break;
                case PortionerLiftArm.LiftPosition.Up:
                    Report(PortionerInfo.Tipped, force: true);
                    break;
                case PortionerLiftArm.LiftPosition.Lowering:
                    Report(PortionerInfo.LiftLowering, force: true);
                    break;
                case PortionerLiftArm.LiftPosition.Down:
                    Report(PortionerInfo.LiftReturned, force: true);
                    break;
            }
        }

        private void ApplyPlatformColor(bool isLoaded)
        {
            if (_platformRenderer == null)
            {
                return;
            }

            _platformPropertyBlock ??= new MaterialPropertyBlock();
            _platformRenderer.GetPropertyBlock(_platformPropertyBlock);

            Color color = isLoaded ? _platformLoadedColor : _platformEmptyColor;
            _platformPropertyBlock.SetColor(BaseColorId, color); // URP Lit
            _platformPropertyBlock.SetColor(ColorId, color);     // Built-in/legacy shaders
            _platformRenderer.SetPropertyBlock(_platformPropertyBlock);
        }

        // ---- Portioning ----

        private void UpdateHopperLevelEvaluation()
        {
            // Machine performs the assessment; sensor only ever reports the raw normalized level
            // (same convention as WeightSensor/TemperatureSensor, Tag 2).
            if (_levelSensor != null && _hopper != null)
            {
                _levelSensor.SetNormalRangeExternally(_hopper.CurrentAmountGrams >= _targetWeightGrams);
            }
        }

        private void BeginPortioning()
        {
            _isProcessing = true;
            _processingTimer = _portioningDurationSeconds;
            Report(PortionerInfo.Portioning);
        }

        private void CompletePortioning()
        {
            _isProcessing = false;

            // Content may have changed during the portioning time (e.g. drained).
            if (_hopper.CurrentAmountGrams < _targetWeightGrams)
            {
                return;
            }

            float portionWeight = _hopper.ConsumeDough(_targetWeightGrams);

            // TODO: recipe reference - no RecipeDefinition is bound in code (operator-set
            // convention), so this currently passes null.
            ProductInstance portion = new ProductInstance(
                Guid.NewGuid().ToString(),
                recipe: null,
                initialState: ProductState.PortionedDough)
            {
                MeasuredWeightGrams = portionWeight
            };

            if (!SpawnPortion(portion))
            {
                return;
            }

            _portionsProduced++;
            _lastPortionWeightGrams = portionWeight;

            PortionDispatched?.Invoke(portion);
            Report(PortionerInfo.PortionDispatched, force: true);
        }

        private bool SpawnPortion(ProductInstance portion)
        {
            if (_portionPrefab == null || _portionSpawnPoint == null)
            {
                Debug.LogError($"{name}: portion prefab or spawn point not assigned - portion discarded.", this);
                return false;
            }

            ProductToken token = Instantiate(_portionPrefab, _portionSpawnPoint.position, _portionSpawnPoint.rotation);
            token.Product = portion;
            return true;
        }

        // ---- HMI parameters ----

        /// <summary>Set by the operator at the HMI, read off the current recipe.</summary>
        public void SetPortioningDuration(float seconds)
        {
            _portioningDurationSeconds = Mathf.Max(0f, seconds);
        }

        /// <summary>Set by the operator at the HMI, read off the current recipe.</summary>
        public void SetTargetWeight(float grams)
        {
            _targetWeightGrams = Mathf.Max(0f, grams);
        }

        /// <summary>
        /// Set by the operator at the HMI, read off the current recipe. Currently unused - kept for
        /// the future QualitySystem (Woche 2) once a variance/quality model exists.
        /// </summary>
        public void SetToleranceGrams(float grams)
        {
            _toleranceGrams = Mathf.Max(0f, grams);
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
            // TODO localization: labels are English fallbacks until keys exist in the table.
            _parameters = new List<MachineParameter>
            {
                new MachineParameter("targetWeight", "Target weight", "g",
                    _config.MinTargetWeightGrams, _config.MaxTargetWeightGrams, _config.TargetWeightStepGrams, "0",
                    () => _targetWeightGrams, SetTargetWeight),
                new MachineParameter("tolerance", "Tolerance", "\u00b1 g",
                    0f, _config.MaxToleranceGrams, _config.ToleranceStepGrams, "0",
                    () => _toleranceGrams, SetToleranceGrams),
                new MachineParameter("portioningDuration", "Portioning time", "s",
                    _config.MinPortioningDurationSeconds, _config.MaxPortioningDurationSeconds,
                    _config.PortioningDurationStepSeconds, "0.0",
                    () => _portioningDurationSeconds, SetPortioningDuration),
            };

            _readouts = new List<MachineReadout>
            {
                new MachineReadout("hopperLevel", "Hopper level", "%",
                    () => _hopper != null ? (_hopper.NormalizedLevel * 100f).ToString("0") : "--",
                    EvaluateHopperLevel, MachineReadoutSlot.FillLevel),
                new MachineReadout("cycleTime", "Cycle time", "s",
                    () => _portioningDurationSeconds.ToString("0.0"),
                    () => MachineValueLevel.Normal, MachineReadoutSlot.CycleTime),
                new MachineReadout("setpoint", "Setpoint", "g",
                    () => $"{_targetWeightGrams:0} \u00b1 {_toleranceGrams:0}",
                    () => MachineValueLevel.Normal, MachineReadoutSlot.Setpoint),
                new MachineReadout("hopperContent", "Dough in hopper", "kg",
                    () => _hopper != null ? (_hopper.CurrentAmountGrams / 1000f).ToString("0.00") : "--",
                    EvaluateHopperLevel),
                new MachineReadout("progress", "Portioning", "%",
                    () => _isProcessing ? (PortioningProgress01 * 100f).ToString("0") : "--",
                    () => _isProcessing ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
                new MachineReadout("lastPortion", "Last portion", "g",
                    () => _lastPortionWeightGrams >= 0f ? _lastPortionWeightGrams.ToString("0") : "--",
                    EvaluateLastPortion),
                new MachineReadout("portionsProduced", "Portions produced", "pcs",
                    () => _portionsProduced.ToString()),
                new MachineReadout("pot", "Pot on lift", "",
                    () => IsPotLoaded ? LocText.Get("hmi.yes", "Yes") : LocText.Get("hmi.no", "No"),
                    () => IsPotLoaded ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
                new MachineReadout("output", "Output", "",
                    () => IsOutputBlocked ? LocText.Get("hmi.blocked", "Blocked") : LocText.Get("hmi.free", "Free"),
                    () => IsOutputBlocked ? MachineValueLevel.Warning : MachineValueLevel.Normal),
            };
        }

        private MachineValueLevel EvaluateHopperLevel()
        {
            if (_hopper == null)
            {
                return MachineValueLevel.Inactive;
            }

            if (_hopper.CurrentAmountGrams >= _targetWeightGrams)
            {
                return MachineValueLevel.Normal;
            }

            // Running without enough dough for one portion: the line starves -> amber.
            return CurrentState == MachineState.Running ? MachineValueLevel.Warning : MachineValueLevel.Inactive;
        }

        private MachineValueLevel EvaluateLastPortion()
        {
            if (_lastPortionWeightGrams < 0f)
            {
                return MachineValueLevel.Inactive;
            }

            return Mathf.Abs(_lastPortionWeightGrams - _targetWeightGrams) <= _toleranceGrams
                ? MachineValueLevel.Normal
                : MachineValueLevel.Warning;
        }

        // ---- Content messages ----

        /// <summary>
        /// Sends the localized text for <paramref name="info"/> (plus an optional detail line, e.g.
        /// an amount) to the HMI. Repeated identical reports are suppressed unless
        /// <paramref name="force"/> is set, so per-frame checks in Update don't spam the channel.
        /// </summary>
        private void Report(PortionerInfo info, string detail = null, bool force = false)
        {
            if (!force && _lastReportedInfo == info && _lastReportedDetail == detail)
            {
                return;
            }

            _lastReportedInfo = info;
            _lastReportedDetail = detail;
            NotifyContentChanged(string.IsNullOrEmpty(detail) ? Info(info) : detail + "\n" + Info(info));
        }

        private string Info(PortionerInfo info)
        {
            string localized = _contents.Find(c => c.State == info)?.Info;
            return string.IsNullOrEmpty(localized) ? LocText.Info("portioner", info, FallbackText(info)) : localized;
        }

        /// <summary>Used until the localization keys exist in the table.</summary>
        private static string FallbackText(PortionerInfo info) => info switch
        {
            PortionerInfo.PotLoaded => "Pot loaded",
            PortionerInfo.PotEmpty => "Pot empty",
            PortionerInfo.Tipping => "Lifting pot",
            PortionerInfo.Tipped => "Pot tipped into hopper",
            PortionerInfo.LiftLowering => "Lowering lift",
            PortionerInfo.NotEnoughDough => "Not enough dough for a portion",
            PortionerInfo.HopperDraining => "Draining hopper",
            PortionerInfo.LiftReturned => "Lift in loading position",
            PortionerInfo.HopperEmpty => "Hopper empty",
            PortionerInfo.Portioning => "Portioning",
            PortionerInfo.PortionDispatched => "Portion dispatched",
            PortionerInfo.OutputBlocked => "Output blocked",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady() => Report(PortionerInfo.Ready, force: true);

        protected override void OnEnterRunning() => Report(PortionerInfo.Running, force: true);

        protected override void OnEnterStopped() => Report(PortionerInfo.Stopped, force: true);

        protected override void OnEnterMaintenance() => Report(PortionerInfo.Maintenance, force: true);

        protected override void OnEnterStarting()
        {
            Report(PortionerInfo.Starting, force: true);
            if (_startingRoutine != null)
            {
                StopCoroutine(_startingRoutine);
            }
            _startingRoutine = StartCoroutine(StartingRoutine());
        }

        protected override void OnEnterStopping()
        {
            Report(PortionerInfo.Stopping, force: true);

            // A portion already in progress is abandoned; its dough was not consumed yet.
            _isProcessing = false;

            if (_stoppingRoutine != null)
            {
                StopCoroutine(_stoppingRoutine);
            }
            _stoppingRoutine = StartCoroutine(StoppingRoutine());
        }

        protected override void OnEnterFault(string faultCode)
        {
            // Minimal safeguard only, same as the other stations - real fault handling is still
            // open (FaultSystem, Week 2). The lift keeps running its cycle so no product is left
            // hanging mid-air.
            Report(PortionerInfo.Fault, force: true);
            _isProcessing = false;

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

        private void SetPotOnLift(HoldInteractable pot)
        {
            var rb = pot.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
            pot.transform.SetParent(_platformCenter.transform, true);
            pot.transform.localPosition = Vector3.zero;
        }


        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("portionsProduced", _portionsProduced);
            values.Set("lastPortionWeight", _lastPortionWeightGrams);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            _portionsProduced = values.GetInt("portionsProduced", _portionsProduced);
            _lastPortionWeightGrams = values.GetFloat("lastPortionWeight", _lastPortionWeightGrams);
        }
    }
}
