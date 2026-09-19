// Game.Production/MixerMachine.cs
// (formerly DoughMixerMachine — renamed, see PROJECT STATUS)
//
// Source station, same as PortionerMachine: produces a product from raw material instead of
// processing an existing ProductInstance. Deliberately does not implement IProductProcessor.
//
// Confirmed decisions:
// 1) Tilting is an explicit operator action on the HMI (tilt button), no auto-tilt on IsProcessingComplete
// 2) The dough ball drops at the drop point regardless of whether a container is present there -
//    no sensor, no gating. The worker can start the machine and tilt it even without content in place;
//    catching the dough ball is on them, not something the machine validates.
// 3) The ProductInstance is created entirely new on tilt, not already at the start of mixing
// 4) MeasuredWeightGrams on tilt comes from SO_MixerConfig.DoughBallWeightGrams (fixed machine value,
//    not a sensor reading or operator input)
// 5) Mixing duration is timed via OnEnterRunning; MixingCompleted fires once the timer elapses

using System;
using System.Collections;
using UnityEngine;

namespace Game.Production
{
    public class MixerMachine : MachineBase
    {
        [SerializeField] private SO_MixerConfig _config;
        [SerializeField] private Transform _doughDropPoint;
        [SerializeField] private GameObject _doughSpherePrefab;

        private float? _mixingDurationOverrideSeconds;
        private bool _isMixingComplete;
        private Coroutine _mixingTimerCoroutine;
        private SO_RecipeDefinition _pendingRecipe;

        /// <summary>Raised once the mixing timer elapses (i.e. a run has finished).</summary>
        public event Action MixingCompleted;

        public bool IsMixingComplete => _isMixingComplete;

        /// <summary>
        /// Effective mixing duration: the operator's HMI override if set via SetMixingDuration,
        /// otherwise SO_MixerConfig.DefaultMixingDurationSeconds.
        /// </summary>
        private float MixingDurationSeconds => _mixingDurationOverrideSeconds ?? _config.DefaultMixingDurationSeconds;

        /// <summary>Operator-facing HMI setting, analogous to SetFormingDuration on FormerMachine.</summary>
        public void SetMixingDuration(float seconds)
        {
            _mixingDurationOverrideSeconds = Mathf.Max(0f, seconds);
        }

        protected override void OnEnterRunning()
        {
            base.OnEnterRunning();
            _isMixingComplete = false;
            _mixingTimerCoroutine = StartCoroutine(MixingTimerRoutine(MixingDurationSeconds));
        }

        protected override void OnEnterFault(string reason)
        {
            base.OnEnterFault(reason);
            if (_mixingTimerCoroutine != null)
            {
                StopCoroutine(_mixingTimerCoroutine);
                _mixingTimerCoroutine = null;
            }
        }

        private IEnumerator MixingTimerRoutine(float durationSeconds)
        {
            yield return new WaitForSeconds(durationSeconds);
            _isMixingComplete = true;
            _mixingTimerCoroutine = null;
            MixingCompleted?.Invoke();
        }

        /// <summary>
        /// Called from the tilt button on the mixer HMI. Explicit operator action — never tilts automatically.
        /// No container/sensor check: the ball is spawned at the drop point regardless of what's underneath.
        /// </summary>
        public bool TryTiltDrum(out ProductInstance dough)
        {
            dough = null;

            if (!_isMixingComplete)
            {
                Debug.LogWarning($"{name}: TryTiltDrum called before mixing finished.");
                return false;
            }

            dough = new ProductInstance(Guid.NewGuid().ToString(), _pendingRecipe, ProductState.MixedDough)
            {
                MeasuredWeightGrams = _config.DoughBallWeightGrams
            };

            SpawnDoughSphere(dough);

            _isMixingComplete = false;
            return true;
        }

        private void SpawnDoughSphere(ProductInstance dough)
        {
            GameObject sphere = Instantiate(_doughSpherePrefab, _doughDropPoint.position, _doughDropPoint.rotation);
            ProductToken token = sphere.GetComponent<ProductToken>();
            token.Product = dough;
        }
    }
}