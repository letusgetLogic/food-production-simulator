using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Physically simulated conveyor segment. Fully replaces the old SlotConveyor
    /// (slot array, SlotCount/SlotOccupancy) as well as IConveyor and ILoadReceiver
    /// (Day 5 decision).
    ///
    /// Principle:
    ///  - A single trigger collider covers the entire belt surface.
    ///  - Products on it are carried by real physics: collisions/stacking run
    ///    through the Rigidbody, but the forward motion itself is set explicitly
    ///    in FixedUpdate via velocity (more robust than relying on friction alone).
    ///  - Jam detection is a simple heuristic (position barely changes despite the
    ///    belt running) instead of a real speed-sensor comparison.
    ///  - Backpressure/domino propagation runs via a direct subscription to the
    ///    downstream segment's/machine's MachineBase.StateChanged - no central
    ///    coordinator, no more ILoadReceiver handshake.
    ///
    /// ASSUMPTION about Game.Core.MachineBase (Dev A owns the real implementation):
    ///  - abstract class MachineBase : IMachine
    ///  - MachineState CurrentState { get; }
    ///  - event Action&lt;MachineState&gt; StateChanged;
    ///  - protected void SetState(MachineState newState) sets CurrentState and
    ///    raises StateChanged
    ///  - StartMachine() / StopMachine() are virtual entry points
    ///  - States: Idle, Starting, Running, Stopping, Stopped, Fault, Maintenance
    ///  - No self-reset out of Fault/Maintenance - requires explicit operator/HMI action
    /// Adjust here if Dev A's actual signatures differ.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ConveyorBelt : MachineBase
    {
        [SerializeField] private SO_ConveyorConfig _config;
        [Tooltip("Transform whose forward axis defines the belt's travel direction.")]
        [SerializeField] private Transform _beltDirectionReference;

        // Replaces the old ILoadReceiver handshake: the belt simply listens to
        // whatever MachineBase sits next in the line (another ConveyorBelt segment
        // or a receiving machine that also derives from MachineBase).
        private MachineBase _downstream;

        // Products currently inside the trigger zone, with their last-checked
        // position (basis for the jam heuristic). Replaces SlotOccupancy as the
        // diagnosis source - Count is what the HMI overview panel can query.
        private readonly Dictionary<ProductToken, Vector3> _trackedProducts = new Dictionary<ProductToken, Vector3>();

        private float _jamCheckTimer;
        private float _stuckTimer;
        private bool _isJammed;

        public bool IsJammed => _isJammed;
        public int ProductCountOnBelt => _trackedProducts.Count;

        /// <summary>
        /// Wires the domino chain. Called once per segment when the line is set up
        /// (replaces ConveyorLineController.LinkSegmentChain).
        /// </summary>
        public void SetDownstream(MachineBase downstream)
        {
            if (_downstream != null)
            {
                _downstream.StateChanged -= OnDownstreamStateChanged;
            }

            _downstream = downstream;

            if (_downstream != null)
            {
                _downstream.StateChanged += OnDownstreamStateChanged;
            }
        }

        private void OnDestroy()
        {
            if (_downstream != null)
            {
                _downstream.StateChanged -= OnDownstreamStateChanged;
            }
        }

        private void OnDownstreamStateChanged(MachineState _, MachineState downstreamState)
        {
            bool downstreamBlocked = downstreamState == MachineState.Stopped
                || downstreamState == MachineState.Stopping
                || downstreamState == MachineState.Fault
                || downstreamState == MachineState.Maintenance;

            if (downstreamBlocked && CurrentState == MachineState.Running)
            {
                // Normal backpressure - not a fault of our own.
                StopMachine();
            }
            else if (!downstreamBlocked && downstreamState == MachineState.Running
                     && CurrentState == MachineState.Stopped)
            {
                // Only auto-resume if we stopped purely due to backpressure - never
                // out of Fault/Maintenance (convention: no self-reset, that stays an
                // explicit operator/HMI action).
                StartMachine();
            }
        }

        /// <summary>
        /// Operator/HMI action to recover from a jam fault. No self-reset.
        /// </summary>
        public void ClearJam()
        {
            if (CurrentState != MachineState.Fault || !_isJammed)
            {
                return;
            }

            _isJammed = false;
            _stuckTimer = 0f;
            SetState(MachineState.Stopped);
        }

        private void FixedUpdate()
        {
            if (CurrentState != MachineState.Running)
            {
                return;
            }

            CarryProducts();
            CheckForJam();
        }

        private void CarryProducts()
        {
            Vector3 travelDirection = _beltDirectionReference.forward;
            Vector3 targetHorizontalVelocity = travelDirection * _config.BeltSpeedMetersPerSecond;

            foreach (var token in _trackedProducts.Keys)
            {
                if (token == null) continue;
                if (!token.TryGetComponent<Rigidbody>(out var rb)) continue;

                // Vertical component (gravity, impact rebound) stays real physics,
                // only the horizontal forward motion is set explicitly.
                Vector3 vertical = Vector3.Project(rb.linearVelocity, Vector3.up);
                rb.linearVelocity = targetHorizontalVelocity + vertical;
            }
        }

        private void CheckForJam()
        {
            _jamCheckTimer += Time.fixedDeltaTime;
            if (_jamCheckTimer < _config.JamCheckIntervalSeconds)
            {
                return;
            }
            _jamCheckTimer = 0f;

            bool anyProductStuck = false;
            var keys = new List<ProductToken>(_trackedProducts.Keys);

            foreach (var token in keys)
            {
                if (token == null)
                {
                    _trackedProducts.Remove(token);
                    continue;
                }

                Vector3 lastPosition = _trackedProducts[token];
                Vector3 currentPosition = token.transform.position;
                float moved = Vector3.Distance(lastPosition, currentPosition);

                if (moved < _config.JamPositionThresholdMeters)
                {
                    anyProductStuck = true;
                }

                _trackedProducts[token] = currentPosition;
            }

            if (anyProductStuck)
            {
                _stuckTimer += _config.JamCheckIntervalSeconds;
                if (_stuckTimer >= _config.JamTimeToTriggerSeconds && !_isJammed)
                {
                    _isJammed = true;
                    SetState(MachineState.Fault);
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<ProductToken>(out var token))
            {
                _trackedProducts[token] = token.transform.position;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent<ProductToken>(out var token))
            {
                _trackedProducts.Remove(token);
            }
        }
    }
}