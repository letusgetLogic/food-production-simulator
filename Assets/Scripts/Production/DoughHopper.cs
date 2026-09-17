
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Holds the dough level inside the Portionierer's hopper (Trichter).
    /// Implements IFillLevelSource so LevelSensor (attached to the same
    /// GameObject) can report the normalized level without knowing anything
    /// about dough, grams, or portioning.
    ///
    /// Ownership split (per the elevator/container design): the elevator
    /// itself - raising the container, detecting it's at tipping height - is
    /// machine mechanics and belongs with Dev A's Portionierer IMachine code.
    /// This component is the receiving end on the data/material-flow side:
    /// Dev A's elevator logic calls AddDough(amount) at the moment it tips
    /// the (dumb, collider-only) container. The Portionierer's own
    /// StartMachine()/running condition then checks HasDough (or the
    /// LevelSensor's CurrentValue) rather than reaching into this hopper's
    /// internals directly.
    ///
    /// Deliberately does not model the container at all - by the time
    /// AddDough() is called, the container has already done its (non-
    /// software) job of getting dough to the hopper. No ProductToken exists
    /// yet at this point either; that still only exists per formed/portioned
    /// pizza further down the line.
    /// </summary>
    public class DoughHopper : MonoBehaviour, IFillLevelSource
    {
        [SerializeField] private float _capacityGrams = 5000f;

        public float CurrentAmountGrams { get; private set; }
        public bool HasDough => CurrentAmountGrams > 0f;

        /// <summary>0..1, consumed by LevelSensor via IFillLevelSource.</summary>
        public float NormalizedLevel => _capacityGrams > 0f
            ? Mathf.Clamp01(CurrentAmountGrams / _capacityGrams)
            : 0f;

        /// <summary>
        /// Called by the elevator mechanism at the moment of tipping. Clamped
        /// to capacity - excess simply doesn't fit (no overflow modeling for
        /// now; revisit if PM wants spillage as a fault case later).
        /// </summary>
        public void AddDough(float amountGrams)
        {
            CurrentAmountGrams = Mathf.Min(_capacityGrams, CurrentAmountGrams + amountGrams);
        }

        /// <summary>
        /// Called by the Portionierer's own portioning logic as it draws
        /// dough out per cycle. Clamped at zero rather than going negative.
        /// </summary>
        public void ConsumeDough(float amountGrams)
        {
            CurrentAmountGrams = Mathf.Max(0f, CurrentAmountGrams - amountGrams);
        }
    }

}