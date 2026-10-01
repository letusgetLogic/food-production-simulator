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
                Action<MachineState, MachineState> handler = (_, next) => ElementStateChanged?.Invoke(captured, next);
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
