using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Physical control panel (Bedienpult) at a machine - replaces the command buttons on the HMI screen.
    /// Listens to its elements (EN 60204-1 / ISO 13850):
    ///  - Start (I, momentary <see cref="PushButton"/>): one signal per press, runs the machine from Ready/Stopped.
    ///    Ignored while the emergency stop is engaged.
    ///  - Stop (O, momentary <see cref="PushButton"/>): one signal per press, normal operational stop.
    ///  - Emergency stop (latching <see cref="EmergencyStopButton"/>): signal "engaged" faults the machine and its
    ///    dependents ("EmergencyStop"), signal "released" lifts the block. Releasing never restarts anything:
    ///    the fault is acknowledged at the HMI screen and the machine started again with Start (I).
    /// Machine-specific buttons on the panel (drum, lift, refill) act on their machine themselves.
    /// </summary>
    public class MachineControlPanel : MonoBehaviour
    {
        [Tooltip("The machine this panel belongs to.")]
        [SerializeField] private MachineBase _machine;

        [Tooltip("Also emergency-stopped with the machine, e.g. the station's own belt.")]
        [SerializeField] private List<MachineBase> _dependents = new List<MachineBase>();

        [SerializeField] private PushButton _startButton;
        [SerializeField] private PushButton _stopButton;
        [SerializeField] private EmergencyStopButton _emergencyStopButton;

        private readonly List<MachineBase> _targets = new List<MachineBase>();

        public MachineBase Machine => _machine;

        /// <summary>True while this panel's emergency stop is engaged (latched).</summary>
        public bool IsEmergencyStopEngaged => _emergencyStopButton != null && _emergencyStopButton.IsEngaged;

        private void Awake()
        {
            if (_startButton != null)
            {
                _startButton.FallbackText = "Start";
            }
            if (_stopButton != null)
            {
                _stopButton.FallbackText = "Stop";
            }
        }

        private void OnEnable()
        {
            if (_startButton != null) _startButton.Pressed += HandleStart;
            if (_stopButton != null) _stopButton.Pressed += HandleStop;
            if (_emergencyStopButton != null)
            {
                _emergencyStopButton.Engaged += HandleEmergencyStopEngaged;
                _emergencyStopButton.Released += HandleEmergencyStopReleased;
            }
        }

        private void OnDisable()
        {
            if (_startButton != null) _startButton.Pressed -= HandleStart;
            if (_stopButton != null) _stopButton.Pressed -= HandleStop;
            if (_emergencyStopButton != null)
            {
                _emergencyStopButton.Engaged -= HandleEmergencyStopEngaged;
                _emergencyStopButton.Released -= HandleEmergencyStopReleased;
            }
        }

        // ---- Entry points without the physical buttons (automation, tests) ----

        public void PressStart() => HandleStart(null);
        public void PressStop() => HandleStop(null);

        /// <summary>Engages/releases through the button if there is one (keeps the mushroom in sync).</summary>
        public void SetEmergencyStop(bool engaged)
        {
            if (_emergencyStopButton != null)
            {
                _emergencyStopButton.SetEngaged(engaged);
            }
            else if (engaged)
            {
                HandleEmergencyStopEngaged(null);
            }
            else
            {
                HandleEmergencyStopReleased(null);
            }
        }

        // ---- Signals ----

        private void HandleStart(PushButton _)
        {
            if (IsEmergencyStopEngaged || _machine == null)
            {
                return;
            }
            _machine.RequestRun();
        }

        private void HandleStop(PushButton _)
        {
            if (_machine != null)
            {
                _machine.StopRun();
            }
        }

        private void HandleEmergencyStopEngaged(EmergencyStopButton _)
        {
            foreach (MachineBase target in Targets())
            {
                target.EngageEmergencyStop(this);
            }
        }

        private void HandleEmergencyStopReleased(EmergencyStopButton _)
        {
            foreach (MachineBase target in Targets())
            {
                target.ReleaseEmergencyStop(this);
            }
        }

        // Machine first: it faults before its dependents (no second "belt fault" alarm on the station).
        private List<MachineBase> Targets()
        {
            _targets.Clear();
            if (_machine != null)
            {
                _targets.Add(_machine);
            }
            foreach (MachineBase dependent in _dependents)
            {
                if (dependent != null && !_targets.Contains(dependent))
                {
                    _targets.Add(dependent);
                }
            }
            return _targets;
        }
    }
}
