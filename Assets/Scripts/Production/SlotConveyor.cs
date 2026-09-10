using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Concrete, slot-based implementation of IConveyor. Tracks only physical
    /// occupancy per slot (GameObject loads) - it never inspects ProductToken or
    /// ProductInstance, per the interface contract. Identity resolution is the
    /// job of sensors/stations scanning the physical load themselves.
    ///
    /// Data flow model: this conveyor moves loads along its own slots
    /// autonomously. Getting a load OFF the exit end and onto the next
    /// conveyor/station is NOT automatic - some external wiring (a connector
    /// script, or the next station itself) is expected to call
    /// TryReleaseLoad(out load) and then hand that load to the next
    /// component's TryAcceptLoad(load). This conveyor has no reference to
    /// what comes next.
    ///
    /// IsBackedUp is therefore only meaningful if that external wiring also
    /// tells this conveyor whether the next component currently accepts loads
    /// (see SetDownstreamAvailabilityCheck) - without it, IsBackedUp stays
    /// false and a completed load simply waits at the exit slot for pickup.
    /// </summary>
    public class SlotConveyor : MonoBehaviour, IConveyor
    {
        [SerializeField] private ConveyorConfig _config;
        [SerializeField] private Transform[] _slotAnchors; // world positions per slot, index 0 = input end

        private GameObject[] _slots;
        private bool[] _occupancy;
        private float[] _slotProgress; // 0..1 progress toward the *next* slot anchor, per occupied slot
        private Func<bool> _downstreamAvailabilityCheck; // optional, wired externally

        public int SlotCount => _config.SlotCount;
        public IReadOnlyList<bool> SlotOccupancy => _occupancy;

        /// <summary>
        /// Set externally (fault system / debug tooling) since there is no real
        /// jam-detection hardware simulated yet. Never derived from movement or
        /// backpressure logic.
        /// </summary>
        public bool IsJammed { get; private set; }

        public bool IsBackedUp { get; private set; }

        public bool CanAcceptLoad => !IsJammed && _slots[0] == null;

        private void Awake()
        {
            _slots = new GameObject[_config.SlotCount];
            _occupancy = new bool[_config.SlotCount];
            _slotProgress = new float[_config.SlotCount];
        }

        /// <summary>
        /// Wires this conveyor to an external check for "can the next
        /// component accept a load right now?" so IsBackedUp reflects real
        /// downstream backpressure. Optional - without it, IsBackedUp is
        /// always false and loads simply wait at the exit slot.
        /// </summary>
        public void SetDownstreamAvailabilityCheck(Func<bool> canDownstreamAccept)
        {
            _downstreamAvailabilityCheck = canDownstreamAccept;
        }

        /// <summary>Debug/fault-system entry point until real jam hardware exists.</summary>
        public void SetJammed(bool jammed) => IsJammed = jammed;

        public bool TryAcceptLoad(GameObject load)
        {
            if (!CanAcceptLoad) return false;

            _slots[0] = load;
            _occupancy[0] = true;
            _slotProgress[0] = 0f;
            PositionLoad(load, 0, 0f);
            return true;
        }

        public bool TryReleaseLoad(out GameObject load)
        {
            int exitIndex = _slots.Length - 1;

            if (IsJammed || IsBackedUp || _slots[exitIndex] == null || _slotProgress[exitIndex] < 1f)
            {
                load = null;
                return false;
            }

            load = _slots[exitIndex];
            _slots[exitIndex] = null;
            _occupancy[exitIndex] = false;
            _slotProgress[exitIndex] = 0f;
            return true;
        }

        private void Update()
        {
            RecomputeBackedUp();

            if (IsJammed) return; // belt physically stuck - nothing moves

            float step = _config.TransportSpeed * Time.deltaTime / _config.SlotSpacing;
            int exitIndex = _slots.Length - 1;

            // Walk from the exit end backwards so a freed slot can be filled
            // by the slot behind it within the same frame.
            for (int i = exitIndex; i >= 0; i--)
            {
                var load = _slots[i];
                if (load == null) continue;

                if (i == exitIndex)
                {
                    // Already at the exit slot - just sits there (progress
                    // caps at 1) until TryReleaseLoad is called externally.
                    _slotProgress[i] = Mathf.Min(1f, _slotProgress[i] + step);
                    PositionLoad(load, i, _slotProgress[i]);
                    continue;
                }

                bool nextSlotFree = _slots[i + 1] == null;
                if (!nextSlotFree) continue; // blocked by the load ahead within this conveyor

                _slotProgress[i] += step;
                if (_slotProgress[i] < 1f)
                {
                    PositionLoad(load, i, _slotProgress[i]);
                    continue;
                }

                _slots[i + 1] = load;
                _occupancy[i + 1] = true;
                _slotProgress[i + 1] = 0f;
                _slots[i] = null;
                _occupancy[i] = false;
                _slotProgress[i] = 0f;
                PositionLoad(load, i + 1, 0f);
            }
        }

        private void RecomputeBackedUp()
        {
            int exitIndex = _slots.Length - 1;
            bool loadReadyAtExit = _slots[exitIndex] != null && _slotProgress[exitIndex] >= 1f;
            bool downstreamUnavailable = _downstreamAvailabilityCheck != null && !_downstreamAvailabilityCheck();

            IsBackedUp = loadReadyAtExit && downstreamUnavailable;
        }

        private void PositionLoad(GameObject load, int slotIndex, float progressToNext)
        {
            if (_slotAnchors == null || _slotAnchors.Length <= slotIndex) return;

            Transform from = _slotAnchors[slotIndex];
            Transform to = (slotIndex + 1 < _slotAnchors.Length) ? _slotAnchors[slotIndex + 1] : from;

            load.transform.position = Vector3.Lerp(from.position, to.position, progressToNext);
        }
    }
}