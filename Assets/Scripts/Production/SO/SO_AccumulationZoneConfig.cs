using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content data for an <see cref="AccumulationZone"/>: the zoned buffer belt in front of a
    /// taktende machine (press). Each slot is its own short belt zone - a waiting product always lies
    /// on a zone that stands still, nothing ever slides over a moving belt.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_AccumulationZoneConfig", menuName = "FoodProductionSimulator/Accumulation Zone Config")]
    public class SO_AccumulationZoneConfig : ScriptableObject
    {
        [Header("Slots")]
        [Tooltip("Number of slots (zones) on the buffer belt.")]
        [Min(1)] [SerializeField] private int _capacity = 4;

        [Tooltip("Length of one slot/zone along the travel direction. Must be larger than the product " +
                 "(28 cm pizza base after pressing, ~18 cm dough ball before) -> 0.32 m.")]
        [Min(0.05f)] [SerializeField] private float _slotLengthMeters = 0.32f;

        [Tooltip("A waiting product stops this close to the centre of its slot.")]
        [Min(0f)] [SerializeField] private float _stopToleranceMeters = 0.01f;

        [Header("Handover to the press")]
        [Tooltip("Distance behind the buffer end within which a released product is still 'on its way into the press'. " +
                 "The next product is only released once the press has taken it or it has gone further than this.")]
        [Min(0.1f)] [SerializeField] private float _maxTransferDistanceMeters = 1.5f;

        [Header("Warning / Fault")]
        [Tooltip("Fill ratio (products / capacity) from which the buffer reports a warning (HMI amber). 0.75 with 4 slots = from 3 products.")]
        [Range(0f, 1f)] [SerializeField] private float _warningFillRatio = 0.75f;

        [Tooltip("If the buffer is full and the press takes nothing for this long, the buffer belt goes into Fault (HMI red).")]
        [Min(0f)] [SerializeField] private float _fullFaultDelaySeconds = 20f;

        public int Capacity => _capacity;
        public float SlotLengthMeters => _slotLengthMeters;
        public float StopToleranceMeters => _stopToleranceMeters;
        public float MaxTransferDistanceMeters => _maxTransferDistanceMeters;
        public float WarningFillRatio => _warningFillRatio;
        public float FullFaultDelaySeconds => _fullFaultDelaySeconds;
    }
}
