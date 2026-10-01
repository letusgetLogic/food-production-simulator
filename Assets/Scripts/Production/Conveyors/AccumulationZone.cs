using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Zoned buffer ("Puffer-Band") in front of a taktende machine such as the press. Turns the
    /// owning <see cref="ConveyorBelt"/> into a zone accumulation conveyor: the belt is split into
    /// <see cref="SO_AccumulationZoneConfig.Capacity"/> slots of equal length, and every slot is a
    /// short belt zone of its own that only runs when it may.
    ///
    ///   travel ->   | slot 0 (rear) | slot 1 | slot 2 | slot 3 (front) | transfer .. press point |
    ///               ^ _infeedEdge
    ///
    /// Per product (decided every FixedUpdate):
    ///  - The slot ahead is free (front slot: press Running and press point free) -> the product moves on.
    ///  - Otherwise it moves up to the centre of its own slot and stops there. Its zone stops with it,
    ///    so the dough never lies on a moving belt.
    ///  - In the transfer area behind the front slot it keeps moving while the press is Running, until
    ///    the press takes it (kinematic) at the press point.
    /// Because every product moves forward as soon as the slot ahead is free, the buffer fills from the
    /// front without gaps: an arriving product moves up one slot after the other.
    ///
    /// Toward upstream: <see cref="CanAccept"/> is false while the rear slot holds a waiting product.
    /// The belt under the portioner pauses then (only once a product reaches its discharge sensor) and
    /// the portioner pauses with it; both continue by themselves when a slot frees up.
    /// Full and the press takes nothing for longer than the configured delay = Fault "BufferFull" on the
    /// buffer belt (raised by <see cref="ConveyorBelt"/>).
    ///
    /// Slot occupancy is computed from product positions inside this zone - effectively one light
    /// barrier per slot. The BoxCollider only marks the volume (forced to trigger).
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class AccumulationZone : MonoBehaviour
    {
        private const int NoProduct = -1;

        [SerializeField] private SO_AccumulationZoneConfig _config;

        [Tooltip("Rear edge of slot 0, where products arrive from the upstream belt. Usually the start of the buffer belt.")]
        [SerializeField] private Transform _infeedEdge;

        [Header("Downstream (press)")]
        [Tooltip("Machine the buffer feeds (press). Overwritten by ConveyorBelt.SetDownstream / ConveyorLineController.")]
        [SerializeField] private MachineBase _downstream;

        [Tooltip("PresenceSensor at the press point. Occupied = the press is holding/pressing a product.")]
        [SerializeField] private PresenceSensor _downstreamInfeedSensor;

        private readonly HashSet<ProductToken> _moving = new HashSet<ProductToken>();
        private readonly List<ProductToken> _candidates = new List<ProductToken>();
        private readonly List<float> _distances = new List<float>();
        private readonly List<int> _order = new List<int>();

        private BoxCollider _volume;
        private int[] _slotOccupant = Array.Empty<int>();
        private bool[] _segmentMoving = Array.Empty<bool>();
        private int _productCount;
        private bool _isTransferOccupied;

        // Product that has left the front slot and is on its way into the press. Tracked until the press
        // takes it (kinematic), it is gone, or it has travelled past the press - so the next product is
        // only released after the handover, without a sensor having to cover the whole transfer path.
        private ProductToken _inTransit;

        /// <summary>Raised when the number of products in the slots changes: (count, capacity).</summary>
        public event Action<int, int> FillChanged;

        public SO_AccumulationZoneConfig Config => _config;
        public MachineBase Downstream => _downstream;
        public int ProductCount => _productCount;
        public int Capacity => _config.Capacity;
        public float FillRatio => Capacity > 0 ? (float)_productCount / Capacity : 0f;
        public bool IsFull => _productCount >= Capacity;
        public bool IsAboveWarningLevel => FillRatio >= _config.WarningFillRatio;

        /// <summary>Press is Running, its press point is free and no product is on its way into it.</summary>
        public bool IsDownstreamReady { get; private set; }

        /// <summary>False while the rear slot holds a waiting product - upstream must not deliver.</summary>
        public bool CanAccept { get; private set; } = true;

        /// <summary>Full and the press takes nothing - turns into a fault after the configured delay.</summary>
        public bool IsBlockedFull => IsFull && !IsDownstreamReady && !_isTransferOccupied;

        /// <summary>True if the given product should be carried this step.</summary>
        public bool ShouldMove(ProductToken token) => token != null && _moving.Contains(token);

        /// <summary>Whether slot zone <paramref name="slot"/> runs (0 = rear). Empty zones run, zones holding a waiting product stand.</summary>
        public bool IsSegmentMoving(int slot) => slot >= 0 && slot < _segmentMoving.Length && _segmentMoving[slot];

        /// <summary>World position of a slot centre (where a waiting product stops).</summary>
        public Vector3 GetSlotCenter(int slot) =>
            _infeedEdge.position + _infeedEdge.forward * ((slot + 0.5f) * _config.SlotLengthMeters);

        public void SetDownstream(MachineBase downstream) => _downstream = downstream;

        private void Awake()
        {
            _volume = GetComponent<BoxCollider>();
            _volume.isTrigger = true;

            if (_infeedEdge == null)
            {
                Debug.LogWarning($"{name}: no infeed edge assigned - using the zone transform.", this);
                _infeedEdge = transform;
            }

            _slotOccupant = new int[_config.Capacity];
            _segmentMoving = new bool[_config.Capacity];
        }

        /// <summary>
        /// Recomputes slot occupancy and which products move. Called by the owning belt every
        /// FixedUpdate while it is Running.
        /// </summary>
        public void Evaluate(IEnumerable<ProductToken> productsOnBelt, bool isBeltRunning)
        {
            int capacity = _config.Capacity;
            float slotLength = _config.SlotLengthMeters;
            Vector3 origin = _infeedEdge.position;
            Vector3 direction = _infeedEdge.forward;

            _candidates.Clear();
            _distances.Clear();
            for (int s = 0; s < capacity; s++)
            {
                _slotOccupant[s] = NoProduct;
            }

            bool isTransferOccupied = false;
            foreach (ProductToken token in productsOnBelt)
            {
                if (token == null || !ContainsPoint(token.transform.position))
                {
                    continue;
                }

                // Held by the press or carried by a worker - not part of the buffer any more.
                if (token.TryGetComponent(out Rigidbody body) && body.isKinematic)
                {
                    continue;
                }

                float distance = Vector3.Dot(token.transform.position - origin, direction);
                int index = _candidates.Count;
                _candidates.Add(token);
                _distances.Add(distance);

                int slot = SlotOf(distance, slotLength);
                if (slot >= capacity)
                {
                    isTransferOccupied = true;
                    _inTransit = token;
                }
                else if (slot >= 0 && (_slotOccupant[slot] == NoProduct || _distances[_slotOccupant[slot]] < distance))
                {
                    _slotOccupant[slot] = index; // the front-most product of the slot is its occupant
                }
            }

            _isTransferOccupied = isTransferOccupied;
            UpdateInTransit(origin, direction, capacity * slotLength);
            bool isDownstreamRunning = InfeedReadinessUtility.IsReady(_downstream, null);
            IsDownstreamReady = !isTransferOccupied && _inTransit == null
                && InfeedReadinessUtility.IsReady(_downstream, _downstreamInfeedSensor);

            // Decide per product.
            _moving.Clear();
            int count = 0;
            for (int i = 0; i < _candidates.Count; i++)
            {
                float distance = _distances[i];
                int slot = SlotOf(distance, slotLength);
                bool move;

                if (slot >= capacity || _candidates[i] == _inTransit)
                {
                    move = isDownstreamRunning; // released: on its way into the press
                }
                else if (slot < 0)
                {
                    move = true; // not yet over the first slot - upstream belt hands it over
                }
                else
                {
                    count++;
                    bool isFrontSlot = slot == capacity - 1;
                    bool isAheadFree = isFrontSlot ? IsDownstreamReady : _slotOccupant[slot + 1] == NoProduct;
                    float stopDistance = (slot + 0.5f) * slotLength - _config.StopToleranceMeters;
                    move = isAheadFree || distance < stopDistance;

                    if (isFrontSlot && isAheadFree && isBeltRunning && i == _slotOccupant[slot])
                    {
                        _inTransit = _candidates[i]; // released into the press
                    }
                }

                if (move && isBeltRunning)
                {
                    _moving.Add(_candidates[i]);
                }
            }

            // Spacing: nothing moves up closer than one slot length behind a product that stands
            // (covers a second product that ended up in the same slot). Front to back, so it cascades.
            _order.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                _order.Add(i);
            }
            _order.Sort((a, b) => _distances[b].CompareTo(_distances[a]));
            for (int k = 1; k < _order.Count; k++)
            {
                int self = _order[k];
                int ahead = _order[k - 1];
                if (_moving.Contains(_candidates[self]) && !_moving.Contains(_candidates[ahead])
                    && _distances[ahead] - _distances[self] < slotLength)
                {
                    _moving.Remove(_candidates[self]);
                }
            }

            // Zone motors: an empty zone runs (ready to receive), a zone with a waiting product stands.
            for (int s = 0; s < capacity; s++)
            {
                int occupant = _slotOccupant[s];
                _segmentMoving[s] = isBeltRunning && (occupant == NoProduct || _moving.Contains(_candidates[occupant]));
            }

            int rear = _slotOccupant[0];
            CanAccept = isBeltRunning && (rear == NoProduct || _moving.Contains(_candidates[rear]));

            if (count != _productCount)
            {
                _productCount = count;
                FillChanged?.Invoke(_productCount, capacity);
            }
        }

        /// <summary>Clears all bookkeeping (belt stopped, faulted or under maintenance).</summary>
        public void Halt()
        {
            _moving.Clear();
            _inTransit = null;
            for (int s = 0; s < _segmentMoving.Length; s++)
            {
                _segmentMoving[s] = false;
            }
            CanAccept = false;
        }

        private void UpdateInTransit(Vector3 origin, Vector3 direction, float bufferLength)
        {
            if (_inTransit == null)
            {
                return;
            }

            bool isTaken = _inTransit.TryGetComponent(out Rigidbody body) && body.isKinematic;
            float distance = Vector3.Dot(_inTransit.transform.position - origin, direction);
            bool hasPassed = distance > bufferLength + _config.MaxTransferDistanceMeters;

            if (isTaken || hasPassed)
            {
                _inTransit = null;
            }
        }

        private static int SlotOf(float distance, float slotLength) =>
            distance < 0f ? -1 : Mathf.FloorToInt(distance / slotLength);

        private bool ContainsPoint(Vector3 worldPoint)
        {
            Vector3 local = _volume.transform.InverseTransformPoint(worldPoint) - _volume.center;
            Vector3 half = _volume.size * 0.5f;
            // Footprint only: the product's pivot height depends on the prefab and is irrelevant here -
            // only products the belt already carries are passed in.
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        private void OnDrawGizmosSelected()
        {
            Transform edge = _infeedEdge != null ? _infeedEdge : transform;
            if (_config == null)
            {
                return;
            }

            for (int s = 0; s < _config.Capacity; s++)
            {
                Vector3 start = edge.position + edge.forward * (s * _config.SlotLengthMeters);
                Vector3 centre = edge.position + edge.forward * ((s + 0.5f) * _config.SlotLengthMeters);
                Gizmos.color = Application.isPlaying && s < _slotOccupant.Length && _slotOccupant[s] != NoProduct
                    ? new Color(1f, 0.75f, 0f) : Color.yellow;
                Gizmos.DrawLine(start - edge.right * 0.3f, start + edge.right * 0.3f);
                Gizmos.DrawWireSphere(centre, 0.05f);
            }

            Vector3 end = edge.position + edge.forward * (_config.Capacity * _config.SlotLengthMeters);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(end - edge.right * 0.3f, end + edge.right * 0.3f);
        }
    }
}
