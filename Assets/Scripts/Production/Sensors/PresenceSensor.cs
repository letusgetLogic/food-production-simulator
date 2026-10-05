using System;
using System.Collections.Generic;
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
    /// mixer's IMachine logic) decides what "normal" means
    /// in its context and writes the verdict back via SetWithinNormalRange.
    /// Defaults to true until a machine says otherwise.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PresenceSensor : MonoBehaviour, ISensor<bool>
    {
        [SerializeField] private string _sensorId;

        [Tooltip("Only react to products (colliders that belong to a ProductToken). Off = any collider with a Rigidbody " +
                 "(e.g. a pot). Prevents machine parts such as an animated piston from blocking the sensor.")]
        [SerializeField] private bool _productsOnly = true;

        // Colliders currently inside the zone. A set instead of a counter: Unity sends no OnTriggerExit when a
        // collider is disabled or destroyed inside the zone (e.g. the pizza switching its visual from
        // PortionedDough to FormedPizza), which would leave a counter stuck at "occupied" forever.
        private readonly HashSet<Collider> _overlaps = new HashSet<Collider>();
        private readonly List<Collider> _stale = new List<Collider>();
        private bool _currentValue;

        /// <summary>Number of colliders currently detected (debug / inspector).</summary>
        public int OverlapCount => _overlaps.Count;

        public string SensorId => _sensorId;
        public bool IsWithinNormalRange { get; private set; } = true;
        public float LastReadingTimestamp { get; private set; }

        public bool CurrentValue
        {
            get => _currentValue;
            private set
            {
                if (_currentValue == value) return;
                OnValueChanged?.Invoke(_currentValue, value);
                _currentValue = value;
            }
        }

        public event Action<bool, bool> OnValueChanged;

        private void OnTriggerEnter(Collider other)
        {
            if (!_productsOnly || ProductColliderUtility.FindToken(other) != null)
            {
                _overlaps.Add(other);
            }
        }

        private void OnTriggerExit(Collider other) => _overlaps.Remove(other);

        private void OnDisable() => _overlaps.Clear();

        private void Update() => UpdateReading();

        public void UpdateReading()
        {
            PruneStaleColliders();
            CurrentValue = _overlaps.Count > 0;
            LastReadingTimestamp = Time.time;
        }

        private void PruneStaleColliders()
        {
            _stale.Clear();
            foreach (Collider c in _overlaps)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy)
                {
                    _stale.Add(c);
                }
            }

            foreach (Collider c in _stale)
            {
                _overlaps.Remove(c);
            }
        }

        /// <summary>
        /// Called by the owning machine after it has evaluated CurrentValue
        /// against its own criteria (e.g. "bowl must be present to start").
        /// </summary>
        public void SetWithinNormalRange(bool isNormal) => IsWithinNormalRange = isNormal;
    }
}