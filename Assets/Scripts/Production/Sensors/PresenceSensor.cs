using System;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Detects whether a physical load currently occupies the sensor's watch
    /// zone (a trigger collider on this GameObject). This is the first sensor
    /// a station like the dough mixer needs: "is there something here to
    /// process at all?" - independent of what it is. Concretely, the mixer
    /// uses exactly this sensor to check "is the bowl present?" before it is
    /// allowed to start.
    ///
    /// Deliberately does NOT go through IConveyor - per the conveyor contract,
    /// a conveyor only tracks physical slot occupancy and knows nothing about
    /// ProductToken/ProductInstance. This sensor instead watches its own
    /// trigger zone directly, exactly like a real photoelectric sensor would,
    /// and keeps Game.Sensors decoupled from any particular IConveyor
    /// implementation.
    ///
    /// IsWithinNormalRange: the sensor does NOT judge this itself - it only
    /// reports the raw reading via CurrentValue. The owning machine (e.g. the
    /// mixer's IProductProcessor/IMachine logic) decides what "normal" means
    /// in its context and writes the verdict back via SetWithinNormalRange.
    /// Defaults to true until a machine says otherwise.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PresenceSensor : MonoBehaviour, ISensor<bool>
    {
        [SerializeField] private string _sensorId;

        private int _overlapCount;
        private bool _currentValue;

        public string SensorId => _sensorId;
        public bool IsWithinNormalRange { get; private set; } = true;
        public float LastReadingTimestamp { get; private set; }

        public bool CurrentValue
        {
            get => _currentValue;
            private set
            {
                if (_currentValue == value) return;
                _currentValue = value;
                OnValueChanged?.Invoke(_currentValue);
            }
        }

        public event Action<bool> OnValueChanged;

        private void OnTriggerEnter(Collider other) => _overlapCount++;

        private void OnTriggerExit(Collider other) => _overlapCount = Mathf.Max(0, _overlapCount - 1);

        private void Update() => UpdateReading();

        public void UpdateReading()
        {
            CurrentValue = _overlapCount > 0;
            LastReadingTimestamp = Time.time;
        }

        /// <summary>
        /// Called by the owning machine after it has evaluated CurrentValue
        /// against its own criteria (e.g. "bowl must be present to start").
        /// </summary>
        public void SetWithinNormalRange(bool isNormal) => IsWithinNormalRange = isNormal;
    }
}