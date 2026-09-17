
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Measures the ambient temperature within its own zone (e.g. inside an
    /// oven cavity or a cooling tunnel segment). Prepared now for later use
    /// by the oven/dosing-station machines - per the sensor contract it only
    /// ever reports the raw CurrentValue; whether that value is within spec
    /// (against RecipeDefinition.TargetBakeTemperature etc.) is decided by
    /// the machine, not here.
    ///
    /// Unlike PresenceSensor/WeightSensor this is not primarily a trigger-
    /// zone occupancy scan - a temperature sensor still measures something
    /// even when no product is present (an empty oven still has a
    /// temperature). Instead it runs a minimal thermal simulation: it drifts
    /// toward AmbientTemperature by default, and toward a hotter target while
    /// an external heat source (the owning machine) marks the zone as
    /// actively heated via SetHeating(). This keeps the sensor decoupled from
    /// IMachine - the machine calls SetHeating(true/false), the sensor does
    /// not know why it's heating or what for.
    /// </summary>
    public class TemperatureSensor : MonoBehaviour, ISensor<float>
    {
        [SerializeField] private string _sensorId;
        [SerializeField] private float _ambientTemperature = 20f;
        [SerializeField] private float _heatingTargetTemperature = 220f;
        [SerializeField] private float _thermalRatePerSecond = 15f; // degrees/sec toward current target

        private bool _isHeating;
        private float _lastReadingTimestamp;

        public string SensorId => _sensorId;
        public float CurrentValue { get; private set; }
        public bool IsWithinNormalRange { get; private set; } // set by owning machine via SetNormalRangeExternally, see below
        public float LastReadingTimestamp => _lastReadingTimestamp;

        public event System.Action<float> OnValueChanged;

        private void Awake()
        {
            CurrentValue = _ambientTemperature;
        }

        private void Update()
        {
            UpdateReading();
        }

        public void UpdateReading()
        {
            float target = _isHeating ? _heatingTargetTemperature : _ambientTemperature;
            float previous = CurrentValue;

            CurrentValue = Mathf.MoveTowards(CurrentValue, target, _thermalRatePerSecond * Time.deltaTime);
            _lastReadingTimestamp = Time.time;

            if (!Mathf.Approximately(previous, CurrentValue))
            {
                OnValueChanged?.Invoke(CurrentValue);
            }
        }

        /// <summary>
        /// Called by the owning machine (later: oven), not derived internally.
        /// The sensor has no notion of "on"/"off" beyond this external flag -
        /// same separation of concerns as WeightSensor not knowing what a
        /// normal weight is.
        /// </summary>
        public void SetHeating(bool isHeating) => _isHeating = isHeating;

        /// <summary>
        /// Per the ISensor contract, IsWithinNormalRange is a fachliche
        /// Bewertung that belongs to the machine, not the sensor. The machine
        /// pushes its verdict back in here purely so it's readable off the
        /// sensor component itself (e.g. for HMI), not because the sensor
        /// computed it.
        /// </summary>
        public void SetNormalRangeExternally(bool isWithinRange) => IsWithinNormalRange = isWithinRange;
    }

}