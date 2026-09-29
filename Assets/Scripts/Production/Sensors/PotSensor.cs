using Game.Core;
using System;
using UnityEngine;

namespace Game.Production
{
    [RequireComponent(typeof(Collider))]
    public class PotSensor : MonoBehaviour, ISensor<HoldInteractable>
    {
        [SerializeField] private string _sensorId;

        private int _overlapCount;
        private HoldInteractable _currentValue;

        public string SensorId => _sensorId;
        public bool IsWithinNormalRange { get; private set; } = true;
        public float LastReadingTimestamp { get; private set; }

        public HoldInteractable CurrentValue
        {
            get => _currentValue;
            private set
            {
                if (_currentValue == value) return;
                OnValueChanged?.Invoke(_currentValue, value); // value changed must be before set value because set pot parent to null
                _currentValue = value;
            }
        }
        public event Action<HoldInteractable, HoldInteractable> OnValueChanged;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("pot"))
            {
                if (_currentValue == null)
                {
                    CurrentValue = other.GetComponent<HoldInteractable>();
                }
                _overlapCount++;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("pot"))
            {
                CurrentValue = null;
                _overlapCount = Mathf.Max(0, _overlapCount - 1);
            }
        }

        private void Update() => UpdateReading();

        public void UpdateReading()
        {
            LastReadingTimestamp = Time.time;
        }

        /// <summary>
        /// Called by the owning machine after it has evaluated CurrentValue
        /// against its own criteria (e.g. "bowl must be present to start").
        /// </summary>
        public void SetWithinNormalRange(bool isNormal) => IsWithinNormalRange = isNormal;
    }
}