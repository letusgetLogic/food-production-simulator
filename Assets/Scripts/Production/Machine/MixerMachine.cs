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
// 6) Drum tilt is a visible rotation of _drumTransform around the local X-axis, animated over
//    SO_MixerConfig.TiltRotationDurationSeconds. Tilt button rotates 0 -> 90 deg (and, as before,
//    spawns the dough ball immediately when the tilt is requested). A second, separate button
//    rotates the drum back 90 -> 0 deg. The two buttons gate each other via _isTilted so the drum
//    can't be tilted twice in a row or reset from an already-upright state.

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
        [SerializeField] private Transform _drumTransform;

        private const float TiltedAngleDegrees = 100f;
        private const float UprightAngleDegrees = 0f;

        private float? _mixingDurationOverrideSeconds;
        private bool _isMixingComplete;
        private bool _isTilted;
        private Coroutine _mixingTimerCoroutine;
        private Coroutine _drumRotationCoroutine;
        private SO_RecipeDefinition _pendingRecipe;

        /// <summary>Raised once the mixing timer elapses (i.e. a run has finished).</summary>
        public event Action MixingCompleted;

        /// <summary>Raised once the drum finishes rotating to the tilted (90 deg) position.</summary>
        public event Action DrumTilted;

        /// <summary>Raised once the drum finishes rotating back to the upright (0 deg) position.</summary>
        public event Action DrumReset;

        public bool IsMixingComplete => _isMixingComplete;
        public bool IsTilted => _isTilted;

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
            if (_drumRotationCoroutine != null)
            {
                StopCoroutine(_drumRotationCoroutine);
                _drumRotationCoroutine = null;
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
        /// Also starts the drum's visual rotation to 100 deg on the X-axis. Fails if mixing isn't complete
        /// yet or the drum is already tilted (must be reset via TryResetDrum first).
        /// </summary>
        public bool TryTiltDrum(out ProductInstance dough)
        {
            dough = null;

            if (!_isMixingComplete)
            {
                Debug.LogWarning($"{name}: TryTiltDrum called before mixing finished.");
                return false;
            }

            if (_isTilted)
            {
                Debug.LogWarning($"{name}: TryTiltDrum called while drum is already tilted.");
                return false;
            }

            dough = new ProductInstance(Guid.NewGuid().ToString(), _pendingRecipe, ProductState.MixedDough)
            {
                MeasuredWeightGrams = _config.DoughBallWeightGrams
            };

            SpawnDoughSphere(dough);

            _isMixingComplete = false;
            _isTilted = true;
            StartDrumRotation(TiltedAngleDegrees, DrumTilted);

            return true;
        }

        /// <summary>
        /// Called from the separate reset button on the mixer HMI. Rotates the drum back to
        /// 0 deg on the X-axis. Only valid while the drum is currently tilted.
        /// </summary>
        public bool TryResetDrum()
        {
            if (!_isTilted)
            {
                Debug.LogWarning($"{name}: TryResetDrum called while drum is not tilted.");
                return false;
            }

            _isTilted = false;
            StartDrumRotation(UprightAngleDegrees, DrumReset);

            return true;
        }

        private void StartDrumRotation(float targetAngleDegrees, Action onComplete)
        {
            if (_drumRotationCoroutine != null)
            {
                StopCoroutine(_drumRotationCoroutine);
            }
            _drumRotationCoroutine = StartCoroutine(RotateDrumRoutine(targetAngleDegrees, onComplete));
        }

        private IEnumerator RotateDrumRoutine(float targetAngleDegrees, Action onComplete)
        {
            Quaternion startRotation = _drumTransform.localRotation;
            Quaternion targetRotation = Quaternion.Euler(targetAngleDegrees, 0f, 0f);

            float durationSeconds = Mathf.Max(0f, _config.TiltRotationDurationSeconds);
            float elapsedSeconds = 0f;

            while (elapsedSeconds < durationSeconds)
            {
                elapsedSeconds += Time.deltaTime;
                float t = durationSeconds > 0f ? Mathf.Clamp01(elapsedSeconds / durationSeconds) : 1f;
                _drumTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            _drumTransform.localRotation = targetRotation;
            _drumRotationCoroutine = null;
            onComplete?.Invoke();
        }

        private void SpawnDoughSphere(ProductInstance dough)
        {
            GameObject sphere = Instantiate(_doughSpherePrefab, _doughDropPoint.position, _doughDropPoint.rotation);
            ProductToken token = sphere.GetComponent<ProductToken>();
            token.Product = dough;
        }
    }
}