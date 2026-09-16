using System;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Reports the weight (in grams) of whatever load currently occupies the
    /// sensor's trigger zone, resolved via that load's ProductToken ->
    /// ProductInstance.MeasuredWeightGrams. Second sensor needed for the
    /// dough mixer: once presence is confirmed, the mixer needs to verify
    /// portion weight before it can proceed.
    ///
    /// Deliberately does NOT go through IConveyor (see PresenceSensor remarks)
    /// - it scans its own trigger zone and resolves identity itself via the
    /// ProductToken component on whatever collider entered, matching "sensors
    /// scan the load and, via its attached ProductToken, resolve the
    /// corresponding ProductInstance" from the IConveyor contract docs.
    ///
    /// IsWithinNormalRange: the sensor does NOT judge this itself - it only
    /// reports the raw reading via CurrentValue. The owning machine decides
    /// what "normal" means (e.g. against RecipeDefinition portion tolerances)
    /// and writes the verdict back via SetWithinNormalRange. Defaults to true
    /// until a machine says otherwise.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeightSensor : MonoBehaviour, ISensor<float>
    {
        [SerializeField] private string _sensorId;

        [Tooltip("Value reported when no load is present. Kept explicit rather " +
                 "than an implicit 0 so downstream logic can distinguish " +
                 "'empty zone' from 'load weighs nothing'.")]
        [SerializeField] private float _noLoadValue = -1f;

        private ProductToken _currentToken;
        private float _currentValue;

        public string SensorId => _sensorId;
        public bool IsWithinNormalRange { get; private set; } = true;
        public float LastReadingTimestamp { get; private set; }

        public float CurrentValue
        {
            get => _currentValue;
            private set
            {
                if (Mathf.Approximately(_currentValue, value)) return;
                _currentValue = value;
                OnValueChanged?.Invoke(_currentValue);
            }
        }

        public event Action<float> OnValueChanged;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out ProductToken token))
                _currentToken = token;
        }

        private void OnTriggerExit(Collider other)
        {
            if (_currentToken != null && other.gameObject == _currentToken.gameObject)
                _currentToken = null;
        }

        private void Update() => UpdateReading();

        public void UpdateReading()
        {
            CurrentValue = _currentToken != null
                ? (float)_currentToken.Product.MeasuredWeightGrams
                : _noLoadValue;

            LastReadingTimestamp = Time.time;
        }

        /// <summary>
        /// Called by the owning machine after it has evaluated CurrentValue
        /// against its own criteria (e.g. RecipeDefinition portion tolerances).
        /// </summary>
        public void SetWithinNormalRange(bool isNormal) => IsWithinNormalRange = isNormal;
    }
}