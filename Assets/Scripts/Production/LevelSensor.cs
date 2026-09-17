
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Measures the fill level of a container (e.g. a sauce or topping
    /// reservoir on the future dosing station), normalized 0..1. Prepared now
    /// for later use, same as TemperatureSensor.
    ///
    /// A level sensor has nothing to scan via OnTriggerEnter/Exit the way
    /// PresenceSensor/WeightSensor do - there's no discrete product passing
    /// through, just a continuously changing fill state on whatever it's
    /// attached to. So instead of a trigger zone, it polls an IFillLevelSource
    /// on the same GameObject via TryGetComponent, exactly once per reading,
    /// the same way WeightSensor pulls ProductInstance data off an incoming
    /// collider rather than tracking it itself.
    ///
    /// IFillLevelSource is a small, source-agnostic contract so the future
    /// reservoir/container component (whoever ends up owning it - likely
    /// Dev A alongside the dosing-station machine) can be built independently
    /// of the sensor. Until that component exists, this sensor simply reports
    /// 0 and IsWithinNormalRange stays false - it degrades safely rather than
    /// throwing.
    /// </summary>
    public interface IFillLevelSource
    {
        /// <summary>Normalized fill level, 0 (empty) to 1 (full).</summary>
        float NormalizedLevel { get; }
    }

    public class LevelSensor : MonoBehaviour, ISensor<float>
    {
        [SerializeField] private string _sensorId;

        private IFillLevelSource _source;
        private float _lastReadingTimestamp;

        public string SensorId => _sensorId;
        public float CurrentValue { get; private set; }
        public bool IsWithinNormalRange { get; private set; }
        public float LastReadingTimestamp => _lastReadingTimestamp;

        public event System.Action<float> OnValueChanged;

        private void Awake()
        {
            TryGetComponent(out _source);
        }

        private void Update()
        {
            UpdateReading();
        }

        public void UpdateReading()
        {
            float previous = CurrentValue;
            CurrentValue = _source != null ? _source.NormalizedLevel : 0f;
            _lastReadingTimestamp = Time.time;

            if (!Mathf.Approximately(previous, CurrentValue))
            {
                OnValueChanged?.Invoke(CurrentValue);
            }
        }

        /// <summary>
        /// Same pattern as TemperatureSensor.SetNormalRangeExternally - the
        /// owning machine (dosing station) decides what counts as "normal"
        /// (e.g. above a refill threshold), the sensor only exposes it.
        /// </summary>
        public void SetNormalRangeExternally(bool isWithinRange) => IsWithinNormalRange = isWithinRange;
    }

}