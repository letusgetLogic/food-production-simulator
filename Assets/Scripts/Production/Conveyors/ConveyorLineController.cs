using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// "Schaltschrank" of one production line section: knows all elements in material-flow order
    /// (machines, belts, tunnel stations) and
    ///  - wires every belt and the portioner to its next element (pause while the next one cannot accept),
    ///  - starts the line from the end to the beginning (downstream first, so nothing runs into a
    ///    stopped element) and stops it from the beginning to the end (upstream first, so the line
    ///    empties instead of piling up),
    ///  - sets one transport speed for all belts in the list,
    ///  - aggregates faults/warnings for HMI or tutorial logic.
    ///
    /// It does NOT control individual products and never resets a Fault itself (convention: the
    /// operator acknowledges at the HMI). Belts inside a tunnel station are owned by that station and
    /// must not be listed here - list the station instead.
    /// </summary>
    public class ConveyorLineController : MonoBehaviour
    {
        [Tooltip("All elements of the line in material-flow order (upstream first).")]
        [SerializeField] private List<MachineBase> _elements = new List<MachineBase>();

        [Tooltip("Wire every belt to the next element of the list on Awake (overrides the belt's own Downstream).")]
        [SerializeField] private bool _autoWireDownstream = true;

        [Tooltip("Delay between starting/stopping two consecutive elements.")]
        [Min(0f)] [SerializeField] private float _sequenceDelaySeconds = 0.5f;

        [Tooltip("Apply the line speed to all listed belts on Awake. Off = every belt keeps its config speed.")]
        [SerializeField] private bool _applyLineSpeedOnAwake;

        [Tooltip("Common transport speed for all listed belts (m/s).")]
        [Min(0f)] [SerializeField] private float _lineSpeedMetersPerSecond = 0.5f;

        [Tooltip("Start the line automatically when the scene starts.")]
        [SerializeField] private bool _startOnPlay;

        private readonly Dictionary<MachineBase, Action<MachineState, MachineState>> _stateHandlers =
            new Dictionary<MachineBase, Action<MachineState, MachineState>>();

        private Coroutine _sequenceRoutine;

        /// <summary>Raised when any element changes state: (element, newState).</summary>
        public event Action<MachineBase, MachineState> ElementStateChanged;

        public IReadOnlyList<MachineBase> Elements => _elements;
        public float LineSpeedMetersPerSecond => _lineSpeedMetersPerSecond;
        public bool IsSequenceRunning => _sequenceRoutine != null;

        public bool IsAnyElementFaulted => _elements.Exists(e => e != null && e.CurrentState == MachineState.Fault);
        public bool IsAnyElementWarning => _elements.Exists(e => e != null && e.HasWarning);
        public bool IsLineRunning => _elements.TrueForAll(e => e == null || e.CurrentState == MachineState.Running);

        // ---- Unity lifecycle ----

        private void Awake()
        {
            if (_autoWireDownstream)
            {
                WireDownstream();
            }

            if (_applyLineSpeedOnAwake)
            {
                SetLineSpeed(_lineSpeedMetersPerSecond);
            }
        }

        private void Start()
        {
            if (_startOnPlay)
            {
                StartLine();
            }
        }

        private void OnEnable()
        {
            foreach (MachineBase element in _elements)
            {
                if (element == null || _stateHandlers.ContainsKey(element))
                {
                    continue;
                }

                MachineBase captured = element;
                Action<MachineState, MachineState> handler = (_, next) =>
                {
                    if (next == MachineState.Running && !(captured is ConveyorBelt))
                    {
                        RestartAdjacentBelts(captured);
                    }
                    ElementStateChanged?.Invoke(captured, next);
                };
                element.StateChanged += handler;
                _stateHandlers.Add(element, handler);
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<MachineBase, Action<MachineState, MachineState>> entry in _stateHandlers)
            {
                if (entry.Key != null)
                {
                    entry.Key.StateChanged -= entry.Value;
                }
            }
            _stateHandlers.Clear();
        }

        // ---- Public API (HMI buttons, tutorial, debug) ----

        /// <summary>Starts all elements, last element first. Faulted elements are skipped (need acknowledgement).</summary>
        [ContextMenu("Start Line")]
        public void StartLine()
        {
            RestartSequence(StartSequence());
        }

        /// <summary>Stops all elements, first element first.</summary>
        [ContextMenu("Stop Line")]
        public void StopLine()
        {
            RestartSequence(StopSequence());
        }

        /// <summary>Sets the same transport speed on every listed belt (clamped per belt config).</summary>
        public void SetLineSpeed(float metersPerSecond)
        {
            _lineSpeedMetersPerSecond = Mathf.Max(0f, metersPerSecond);

            foreach (MachineBase element in _elements)
            {
                if (element is ConveyorBelt belt)
                {
                    belt.SetSpeed(_lineSpeedMetersPerSecond);
                }
            }
        }

        /// <summary>Wires every belt (and the portioner) to the next element of the list.</summary>
        public void WireDownstream()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] is IDownstreamLink link)
                {
                    MachineBase next = i + 1 < _elements.Count ? _elements[i + 1] : null;
                    link.SetDownstream(next);
                }
            }
        }

        /// <summary>Collects all elements currently in Fault (for an alarm list).</summary>
        public void GetFaultedElements(List<MachineBase> result)
        {
            result.Clear();
            foreach (MachineBase element in _elements)
            {
                if (element != null && element.CurrentState == MachineState.Fault)
                {
                    result.Add(element);
                }
            }
        }

        // ---- Restart after a fault ----

        /// <summary>
        /// A station (portioner, press, ...) was started again by the operator: the belts directly before
        /// and after it run again too (playtest 06.10.: after acknowledging and restarting portioner and
        /// press the belts in between stood still). Stopped/Ready belts are started; a buffer belt in Fault
        /// "BufferFull" is cleared, because that fault only follows from the stopped station - the restart is
        /// the operator action. Real belt faults (Jam) still need their own acknowledgement.
        /// Not during the start/stop sequence, which keeps its own order.
        /// </summary>
        private void RestartAdjacentBelts(MachineBase station)
        {
            if (IsSequenceRunning)
            {
                return;
            }

            int index = _elements.IndexOf(station);
            if (index < 0)
            {
                return;
            }

            // Downstream belts first, so nothing runs into a standing belt.
            for (int i = index + 1; i < _elements.Count && _elements[i] is ConveyorBelt belt; i++)
            {
                RestartBelt(belt);
            }

            for (int i = index - 1; i >= 0 && _elements[i] is ConveyorBelt belt; i--)
            {
                RestartBelt(belt);
            }
        }

        private static void RestartBelt(ConveyorBelt belt)
        {
            if (belt.CurrentState == MachineState.Fault && belt.FaultReason == ConveyorBelt.FaultReasonBufferFull)
            {
                belt.AcknowledgeFault();
                belt.CompleteMaintenance();
            }

            belt.RequestRun();
        }

        // ---- Sequences ----

        private IEnumerator StartSequence()
        {
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                MachineBase element = _elements[i];
                if (element == null || element.CurrentState == MachineState.Running
                    || element.CurrentState == MachineState.Starting)
                {
                    continue;
                }

                element.RequestRun();
                yield return Delay();
            }

            _sequenceRoutine = null;
        }

        private IEnumerator StopSequence()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                MachineBase element = _elements[i];
                if (element == null || element.CurrentState != MachineState.Running)
                {
                    continue;
                }

                element.StopRun();
                yield return Delay();
            }

            _sequenceRoutine = null;
        }

        private object Delay() => _sequenceDelaySeconds > 0f ? new WaitForSeconds(_sequenceDelaySeconds) : null;

        private void RestartSequence(IEnumerator sequence)
        {
            if (_sequenceRoutine != null)
            {
                StopCoroutine(_sequenceRoutine);
            }

            _sequenceRoutine = StartCoroutine(sequence);
        }
    }
}
