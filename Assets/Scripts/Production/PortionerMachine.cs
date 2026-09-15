using Game.Sensors;
using System.Collections;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Second production station: portions MixedDough into PortionedDough by weight.
    /// Implements IMachine (via MachineBase) and IProductProcessor, same pattern as DoughMixerMachine.
    /// No IInteractable — operated exclusively via Hotspot/HMI (Dev C), consistent with the Teigmischer.
    /// No direct RecipeDefinition reference: the operator reads the recipe at the HMI panel and sets
    /// SetPortioningDuration() / SetTargetWeight() / SetToleranceGrams() manually.
    /// </summary>
    public class PortionerMachine : MachineBase, IProductProcessor
    {
        [SerializeField] private SO_PortionerConfig _config;

        [Tooltip("Weight sensor used to evaluate the portioned dough. Machine performs the evaluation; " +
                 "the sensor only ever reports CurrentValue.")]
        [SerializeField] private WeightSensor _weightSensor;

        private float _portioningDurationSeconds;
        private float _targetWeightGrams;
        private float _toleranceGrams;

        private float _processingTimer;
        private bool _isProcessing;
        private bool _isProcessingComplete;

        private ProductInstance _heldProduct;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        public ProductState ExpectedInputState => ProductState.MixedDough;

        public ProductState OutputState => ProductState.PortionedDough;

        public bool CanAcceptProduct =>
            CurrentState == MachineState.Running && _heldProduct == null && !_isProcessing;

        public bool IsProcessingComplete => _isProcessingComplete;

        private void Awake()
        {
            // Defaults from content; overridable at runtime by the operator via the HMI.
            _portioningDurationSeconds = _config.DefaultPortioningDurationSeconds;
            _targetWeightGrams = _config.DefaultTargetWeightGrams;
            _toleranceGrams = _config.DefaultToleranceGrams;
        }

        private void Update()
        {
            if (!_isProcessing)
            {
                return;
            }

            _processingTimer -= Time.deltaTime;
            if (_processingTimer <= 0f)
            {
                _isProcessing = false;
                _isProcessingComplete = true;
                EvaluatePortionWeight();
            }
        }

        public bool TryBeginProcessing(ProductInstance product)
        {
            if (!CanAcceptProduct || product == null || product.CurrentState != ExpectedInputState)
            {
                return false;
            }

            _heldProduct = product;
            _isProcessing = true;
            _isProcessingComplete = false;
            _processingTimer = _portioningDurationSeconds;
            return true;
        }

        public bool TryCollectProcessedProduct(out ProductInstance product)
        {
            if (!_isProcessingComplete || _heldProduct == null)
            {
                product = null;
                return false;
            }

            product = _heldProduct;
            product.CurrentState = OutputState;

            _heldProduct = null;
            _isProcessingComplete = false;
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

        /// <summary>Set by the operator at the HMI, read off the current recipe.</summary>
        public void SetToleranceGrams(float grams)
        {
            _toleranceGrams = Mathf.Max(0f, grams);
        }

        private void EvaluatePortionWeight()
        {
            if (_weightSensor == null)
            {
                return;
            }

            float measured = _weightSensor.CurrentValue;
            bool withinRange = Mathf.Abs(measured - _targetWeightGrams) <= _toleranceGrams;

            // Machine performs the assessment; sensor only ever reports raw values (Tag 2 convention).
            _weightSensor.SetWithinNormalRange(withinRange);
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

        protected override void OnEnterFault(string faultDescription)
        {
            // Minimal safeguard only, same as DoughMixerMachine — stops running timing coroutines.
            // Real fault handling for a portion in progress (_heldProduct, _isProcessing) is still
            // open, same as for the mixer (see project status: FaultSystem, Week 2).
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