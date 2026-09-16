using System.Collections;
using UnityEngine;

namespace Game.Production
{

    /// <summary>
    /// Third production station: forms PortionedDough into a FormedPizza (dough press/spreader).
    /// Implements IMachine (via MachineBase) and IProductProcessor, same pattern as DoughMixerMachine
    /// and PortionerMachine. No IInteractable — operated exclusively via Hotspot/HMI (Dev C).
    /// No direct RecipeDefinition reference: the operator reads the recipe at the HMI panel and calls
    /// SetFormingDuration() manually.
    ///
    /// Unlike PortionerMachine, this station does not yet evaluate product quality: no dimension
    /// sensor (diameter/thickness) exists in Game.Sensors yet (only PresenceSensor/WeightSensor are
    /// confirmed as of Tag 2). DefaultTargetDiameterCm/DefaultTargetThicknessMm in FormerConfig are
    /// content reference values only, intended for later QualitySystem use (Woche 2) — not checked here.
    /// </summary>
    public class FormerMachine : MachineBase, IProductProcessor
    {
        [SerializeField] private SO_FormerConfig _config;

        private float _formingDurationSeconds;

        private float _processingTimer;
        private bool _isProcessing;
        private bool _isProcessingComplete;

        private ProductInstance _heldProduct;

        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        public ProductState ExpectedInputState => ProductState.PortionedDough;

        public ProductState OutputState => ProductState.FormedPizza;

        public bool CanAcceptProduct =>
            CurrentState == MachineState.Running && _heldProduct == null && !_isProcessing;

        public bool IsProcessingComplete => _isProcessingComplete;

        private void Awake()
        {
            // Default from content; overridable at runtime by the operator via the HMI.
            _formingDurationSeconds = _config.DefaultFormingDurationSeconds;
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
            _processingTimer = _formingDurationSeconds;
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
        public void SetFormingDuration(float seconds)
        {
            _formingDurationSeconds = Mathf.Max(0f, seconds);
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
            // Minimal safeguard only, same as DoughMixerMachine/PortionerMachine — stops running
            // timing coroutines. Real fault handling for a product in progress (_heldProduct,
            // _isProcessing) is still open, same as for the other stations (FaultSystem, Week 2).
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