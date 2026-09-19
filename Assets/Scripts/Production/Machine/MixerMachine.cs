using System.Collections;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// First real machine implementation, replacing DummyMachine.
    /// Represents the dough mixer station: accepts a RawDough-stage
    /// ProductInstance, mixes it for an operator-configured duration, and
    /// makes the result available via IProductProcessor.TryCollectProcessedProduct.
    ///
    /// Does NOT implement IInteractable. Start/stop is triggered externally via
    /// StartMachine()/StopMachine() (inherited from MachineBase) by a separate
    /// Hotspot/HMI component (Dev C), per the MachineBase/InteractableBase
    /// composition decision.
    ///
    /// Recipe handling: per team decision, this machine does NOT hold a
    /// RecipeDefinition reference. The operator reads the active recipe at the
    /// HMI and dials in the mixing duration there; the HMI calls
    /// SetMixingDuration() on this machine. Correctness checking against the
    /// recipe target (e.g. Margherita's 20s) is presumably a QualitySystem
    /// concern for a later week - not handled here.
    ///
    /// Starting/Stopping timing: this machine owns its own grace periods via
    /// DoughMixerConfig.startupDurationSeconds/shutdownDurationSeconds, driven
    /// through the OnEnterStarting/OnEnterStopping hooks + SetState(), as
    /// confirmed against the real MachineBase source.
    /// </summary>
    [DisallowMultipleComponent]
    public class MixerMachine : MachineBase, IProductProcessor
    {
        [Header("Machine Configuration")]
        [SerializeField] private SO_DoughMixerConfig _config;

        [Header("Debug / Read-Only")]
        [SerializeField] private float _configuredMixingDurationSeconds;
        [SerializeField] private float _mixingElapsedSeconds;
        [SerializeField] private ProductInstance _heldProduct;
        [SerializeField] private bool _isProcessingComplete;

        public ProductState ExpectedInputState => ProductState.RawDough;
        public ProductState OutputState => ProductState.MixedDough;

        public bool CanAcceptProduct =>
            _heldProduct == null && CurrentState == MachineState.Idle;

        public bool IsProcessingComplete => _isProcessingComplete;


        /// <summary>
        /// Called by the HMI/control-panel component when the operator dials in
        /// the mixing duration after reading it off the active recipe. Ignored
        /// while a product is already being mixed.
        /// </summary>
        public void SetMixingDuration(float seconds)
        {
            if (_heldProduct != null)
            {
                Debug.LogWarning($"{name}: cannot change mixing duration while a product is being processed.", this);
                return;
            }

            if (seconds <= 0f)
            {
                Debug.LogWarning($"{name}: mixing duration must be positive.", this);
                return;
            }

            _configuredMixingDurationSeconds = seconds;
        }

        public bool TryBeginProcessing(ProductInstance product)
        {
            if (!CanAcceptProduct) return false;
            if (product == null || product.CurrentState != ExpectedInputState) return false;

            if (_configuredMixingDurationSeconds <= 0f)
            {
                Debug.LogWarning($"{name}: no mixing duration configured via HMI yet.", this);
                return false;
            }

            _heldProduct = product;
            _mixingElapsedSeconds = 0f;
            _isProcessingComplete = false;

            StartMachine(); // Idle -> Starting; OnEnterStarting() takes it to Running after the grace period.
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
            _mixingElapsedSeconds = 0f;

            return true;
        }

        private void Update()
        {
            if (_heldProduct == null || _isProcessingComplete) return;
            if (CurrentState != MachineState.Running) return;

            _mixingElapsedSeconds += Time.deltaTime;

            if (_mixingElapsedSeconds >= _configuredMixingDurationSeconds)
            {
                _isProcessingComplete = true;
                StopMachine(); // Running -> Stopping; OnEnterStopping() takes it to Stopped after the grace period.
            }
        }

        // ---- MachineBase hooks: own the Starting/Stopping grace periods ----

        protected override void OnEnterStarting()
        {
            StartCoroutine(AdvanceAfterDelay(
                _config != null ? _config.StartupDurationSeconds : 0f,
                MachineState.Running));
        }

        protected override void OnEnterStopping()
        {
            StartCoroutine(AdvanceAfterDelay(
                _config != null ? _config.ShutdownDurationSeconds : 0f,
                MachineState.Stopped));
        }

        protected override void OnEnterFault(string reason)
        {
            // Prevents a pending startup/shutdown coroutine from firing a stale
            // SetState() call after a fault has interrupted the transition.
            // (SetState() would reject it anyway since Fault only leads to
            // Maintenance, but stopping the coroutine keeps intent clear.)
            StopAllCoroutines();
        }

        private IEnumerator AdvanceAfterDelay(float delaySeconds, MachineState target)
        {
            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            SetState(target);
        }
    }
}
