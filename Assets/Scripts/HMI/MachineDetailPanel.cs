using System;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Per-machine detail view: readouts plus the operator commands. Commands are
    /// exposed as C# events; the binder decides which machine they map onto, so
    /// this panel never references a concrete machine type.
    /// </summary>
    public class MachineDetailPanel : HmiPanelBase
    {
        [Header("Scroll View")]
        [SerializeField] private RectTransform _scrollViewRect;

        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _machineNameText;
        [SerializeField] private MachineStateRowView _stateRow;

        [Header("Readouts")]
        [SerializeField] private StatusTileView _temperatureTile;
        [SerializeField] private StatusTileView _fillLevelTile;
        [SerializeField] private StatusTileView _cycleTimeTile;
        [SerializeField] private StatusTileView _setpointTile;

        [Header("Content")]
        [SerializeField] private TextMeshProUGUI _content;

        [Header("Operator commands")]
        [SerializeField] private GameObject _commandRow;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _stopButton;
        [SerializeField] private Button _acknowledgeFaultButton;
        [SerializeField] private Button _completeMaintenanceButton;
        [SerializeField] private Button _resetToIdleButton;

        private bool _controlEnabled = true;
        private MachineState _currentState;

        public event Action StartRequested;
        public event Action StopRequested;
        public event Action AcknowledgeFaultRequested;
        public event Action CompleteMaintenanceRequested;
        public event Action ResetToIdleRequested;

        protected override void Awake()
        {
            base.Awake();

            _startButton?.onClick.AddListener(() => StartRequested?.Invoke());
            _stopButton?.onClick.AddListener(() => StopRequested?.Invoke());
            _acknowledgeFaultButton?.onClick.AddListener(() => AcknowledgeFaultRequested?.Invoke());
            _completeMaintenanceButton?.onClick.AddListener(() => CompleteMaintenanceRequested?.Invoke());
            _resetToIdleButton?.onClick.AddListener(() => ResetToIdleRequested?.Invoke());
        }

        public void SetMachineName(string machineName)
        {
            if (_machineNameText != null)
            {
                _machineNameText.text = machineName;
            }

            _stateRow?.SetMachineName(machineName);
        }

        public void SetState(MachineState state)
        {
            _currentState = state;
            _stateRow?.SetState(state);
            UpdateCommandAvailability(state);
        }

        /// <summary>
        /// Toggles whether this instance may operate the machine (Terminal) or is
        /// read-only (Tablet). The command row is hidden entirely rather than just
        /// disabled, so a read-only viewer never sees buttons that look pressable.
        /// </summary>
        public void SetControlEnabled(bool controlEnabled)
        {
            _controlEnabled = controlEnabled;

            if (_commandRow != null)
            {
                _commandRow.SetActive(controlEnabled);
            }

            if (_scrollViewRect != null)
            {
                _scrollViewRect.offsetMin = new Vector2(_scrollViewRect.offsetMin.x, controlEnabled ? 90f : 0f);
            }

            UpdateCommandAvailability(_currentState);
        }

        public void SetTemperature(float celsius, HmiValueSeverity severity) =>
            _temperatureTile?.SetValue(celsius, severity);

        public void SetFillLevel(float percent, HmiValueSeverity severity) =>
            _fillLevelTile?.SetValue(percent, severity, "0");

        public void SetCycleTime(float seconds, HmiValueSeverity severity) =>
            _cycleTimeTile?.SetValue(seconds, severity);

        public void SetSetpoint(string value) =>
            _setpointTile?.SetValue(value, HmiValueSeverity.Normal);

        public void SetContent(string content) =>
            _content?.SetText(content);

        /// <summary>
        /// Mirrors the state machine rules: no self-reset out of Stopped, Fault or
        /// Maintenance – only the matching explicit operator command is offered.
        /// </summary>
        private void UpdateCommandAvailability(MachineState state)
        {
            if (!_controlEnabled)
            {
                SetInteractable(_startButton, false);
                SetInteractable(_stopButton, false);
                SetInteractable(_acknowledgeFaultButton, false);
                SetInteractable(_completeMaintenanceButton, false);
                SetInteractable(_resetToIdleButton, false);
                return;
            }

            SetInteractable(_startButton, state == MachineState.Idle);
            SetInteractable(_stopButton, state == MachineState.Running);
            SetInteractable(_acknowledgeFaultButton, state == MachineState.Fault);
            SetInteractable(_completeMaintenanceButton, state == MachineState.Maintenance);
            SetInteractable(_resetToIdleButton, state == MachineState.Stopped);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }
}