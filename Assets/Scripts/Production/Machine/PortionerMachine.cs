using System.Collections;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Second production station: draws dough from a DoughHopper (filled via PortionerElevator)
    /// and produces PortionedDough ProductInstances by weight.
    ///
    /// Deliberately does NOT implement IProductProcessor: that interface is for a station that
    /// takes in an existing ProductInstance and advances its state. The Portionierer is the first
    /// point where a ProductInstance for an individual pizza exists at all — it is a source, not a
    /// pipe stage, since dough arrives as bulk fill level in a hopper rather than as a handed-off
    /// product. TODO: confirm this deviation with PM/Dev B before treating it as convention for
    /// future stations.
    ///
    /// No IInteractable — operated exclusively via Hotspot/HMI (Dev C), consistent with the
    /// Teigmischer. No direct RecipeDefinition binding in code: SetTargetWeight()/SetToleranceGrams()/
    /// SetPortioningDuration() are set by the operator at the HMI, reading the recipe off the panel.
    /// </summary>
    public class PortionerMachine : MachineBase
    {
        [SerializeField] private SO_PortionerConfig _config;
        [SerializeField] private Hopper _hopper;

        [Tooltip("Optional: reports the hopper's NormalizedLevel via IFillLevelSource, same GameObject as _hopper.")]
        [SerializeField] private LevelSensor _levelSensor;

        private float _portioningDurationSeconds;
        private float _targetWeightGrams;
        private float _toleranceGrams;

        private float _processingTimer;
        private bool _isProcessing;
        private bool _isPortionReady;

        private ProductInstance _readyPortion;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        public bool CanProducePortion =>
            CurrentState == MachineState.Running && !_isProcessing && !_isPortionReady
            && _hopper.CurrentAmountGrams >= _targetWeightGrams;

        public bool IsPortionReady => _isPortionReady;

        private void Awake()
        {
            _portioningDurationSeconds = _config.DefaultPortioningDurationSeconds;
            _targetWeightGrams = _config.DefaultTargetWeightGrams;
            _toleranceGrams = _config.DefaultToleranceGrams;
        }

        private void Update()
        {
            UpdateHopperLevelEvaluation();

            if (CanProducePortion)
            {
                BeginPortioning();
                return;
            }

            if (!_isProcessing)
            {
                return;
            }

            _processingTimer -= Time.deltaTime;
            if (_processingTimer <= 0f)
            {
                CompletePortioning();
            }
        }

        private void UpdateHopperLevelEvaluation()
        {
            // Machine performs the assessment; sensor only ever reports the raw normalized level
            // (same convention as WeightSensor/TemperatureSensor, Tag 2).
            if (_levelSensor != null)
            {
                _levelSensor.SetNormalRangeExternally(_hopper.CurrentAmountGrams >= _targetWeightGrams);
            }
        }

        private void BeginPortioning()
        {
            _isProcessing = true;
            _processingTimer = _portioningDurationSeconds;
        }

        private void CompletePortioning()
        {
            _isProcessing = false;

            _hopper.ConsumeDough(_targetWeightGrams);

            // TODO: recipe reference for the new ProductInstance — no RecipeDefinition is bound in
            // code (operator-set convention), so this currently passes null. Revisit once it's
            // decided how the active recipe reaches the Portionierer (e.g. a SetActiveRecipe() HMI
            // call, matching SetTargetWeight()/SetPortioningDuration()).
            _readyPortion = new ProductInstance(
                System.Guid.NewGuid().ToString(),
                recipe: null,
                initialState: ProductState.PortionedDough);
            _readyPortion.MeasuredWeightGrams = _targetWeightGrams;

            _isPortionReady = true;
        }

        public bool TryCollectPortion(out ProductInstance product)
        {
            if (!_isPortionReady)
            {
                product = null;
                return false;
            }

            product = _readyPortion;
            _readyPortion = null;
            _isPortionReady = false;
            return true;
        }

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
        /// Set by the operator at the HMI, read off the current recipe. Currently unused — the
        /// produced portion is always exactly TargetWeightGrams, no variance is modeled yet. Kept
        /// for the future QualitySystem (Woche 2) once a variance/quality model exists.
        /// </summary>
        public void SetToleranceGrams(float grams)
        {
            _toleranceGrams = Mathf.Max(0f, grams);
        }

        protected override void OnEnterStarting()
        {
            if (_startingRoutine != null)
            {
                StopCoroutine(_startingRoutine);
            }
            _startingRoutine = StartCoroutine(StartingRoutine());
        }

        protected override void OnEnterStopping()
        {
            if (_stoppingRoutine != null)
            {
                StopCoroutine(_stoppingRoutine);
            }
            _stoppingRoutine = StartCoroutine(StoppingRoutine());
        }

        protected override void OnEnterFault(string faultCode)
        {
            // Minimal safeguard only, same as the other stations — stops running timing coroutines.
            // Real fault handling for dough mid-portioning is still open (FaultSystem, Week 2).
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
    }
}