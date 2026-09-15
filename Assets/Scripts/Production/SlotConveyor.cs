
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
    /// This conveyor has no opinion about *when* it should run - running state
    /// is commanded entirely from outside via StartRunning()/StopRunning(),
    /// typically by a ConveyorLineController reacting to the receiving
    /// machine's readiness. Backpressure/queueing while stopped is a normal,
    /// unmanaged side effect - not a state this conveyor tracks or reports.
    ///
    /// IsJammed represents a genuine physical fault and, per the Tag-2
    /// decision, is still set externally (fault system / debug tooling) since
    /// there is no real jam-detection hardware simulated yet - it is never
    /// derived from movement logic here.
    ///
    /// Hand-off to whatever comes next is not part of the public IConveyor
    /// contract (see ILoadReceiver). This conveyor implements ILoadReceiver
    /// itself (to accept loads pushed onto its entry slot) and, once wired via
    /// LinkNext, actively pushes a completed exit-slot load onto whatever
    /// ILoadReceiver comes after it - another SlotConveyor, or a receiving
    /// machine's intake.
    /// </summary>
    public class SlotConveyor : MonoBehaviour, IConveyor, ILoadReceiver
    {
        [SerializeField] private SO_ConveyorConfig _config;
        [SerializeField] private Transform[] _slotAnchors; // world positions per slot, index 0 = input end

        private GameObject[] _slots;
        private bool[] _occupancy;
        private float[] _slotProgress; // 0..1 progress toward the *next* slot anchor, per occupied slot
        private bool _isRunning;
        private ILoadReceiver _next; // wired by ConveyorLineController, not part of IConveyor

        public int SlotCount => _config.SlotCount;

        /// <summary>
        /// Diagnostic/HMI use only. Not part of IConveyor - the abstract
        /// contract doesn't know or care that this implementation happens to
        /// be slot-based.
        /// </summary>
        public IReadOnlyList<bool> SlotOccupancy => _occupancy;

        public bool IsRunning => _isRunning;

        /// <summary>
        /// Set externally (fault system / debug tooling) since there is no real
        /// jam-detection hardware simulated yet. Never derived from movement
        /// logic here.
        /// </summary>
        public bool IsJammed { get; private set; }

        private void Awake()
        {
            _slots = new GameObject[_config.SlotCount];
            _occupancy = new bool[_config.SlotCount];
            _slotProgress = new float[_config.SlotCount];
        }

        public void StartRunning() => _isRunning = true;

        public void StopRunning() => _isRunning = false;

        /// <summary>Debug/fault-system entry point until real jam hardware exists.</summary>
        public void SetJammed(bool jammed) => IsJammed = jammed;

        /// <summary>
        /// Wired once by ConveyorLineController at line-build time. Not
        /// exposed on IConveyor - this is internal chain plumbing, not a
        /// public control surface.
        /// </summary>
        public void LinkNext(ILoadReceiver next) => _next = next;

        /// <summary>
        /// ILoadReceiver implementation: lets a previous segment (or a
        /// producing machine, for the first segment in a line) place a load
        /// onto this conveyor's entry slot. Independent of IsRunning - a load
        /// can be set down on a stopped belt, it simply won't advance until
        /// StartRunning() is called.
        /// </summary>
        public bool TryReceiveLoad(GameObject load)
        {
            if (IsJammed || _slots[0] != null)
            {
                return false;
            }

            _slots[0] = load;
            _occupancy[0] = true;
            _slotProgress[0] = 0f;
            PositionLoad(load, 0, 0f);
            return true;
        }

        private void Update()
        {
            if (!_isRunning || IsJammed) return; // stopped on purpose, or physically stuck - nothing moves

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
                    AdvanceExitSlot(load, i, step);
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

        private void AdvanceExitSlot(GameObject load, int exitIndex, float step)
        {
            _slotProgress[exitIndex] = Mathf.Min(1f, _slotProgress[exitIndex] + step);

            bool readyToHandOff = _slotProgress[exitIndex] >= 1f;
            bool handedOff = readyToHandOff && _next != null && _next.TryReceiveLoad(load);

            if (handedOff)
            {
                _slots[exitIndex] = null;
                _occupancy[exitIndex] = false;
                _slotProgress[exitIndex] = 0f;
                return;
            }

            // No next receiver linked, or it has no room right now - the load
            // simply waits at the exit anchor. Normal backpressure, not tracked.
            PositionLoad(load, exitIndex, _slotProgress[exitIndex]);
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
