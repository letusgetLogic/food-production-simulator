using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// One row in the machine overview: name plus state lamp. Takes a plain
    /// MachineState – no subscription to StateChanged here, so the event-channel
    /// decision stays open.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MachineStateRowView : MonoBehaviour
    {
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private TextMeshProUGUI _machineNameText;
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private Image _stateLamp;

        [Header("Defaults")]
        [SerializeField] private string _machineName = "MACHINE";

        public string MachineName => _machineName;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.interactable = false;

            // Long state texts ("Running – TemperatureDeviation") shrink instead of wrapping into two lines.
            if (_stateText != null)
            {
                _stateText.textWrappingMode = TextWrappingModes.NoWrap;
                _stateText.enableAutoSizing = true;
                _stateText.fontSizeMax = _stateText.fontSize;
                _stateText.fontSizeMin = Mathf.Max(10f, _stateText.fontSize * 0.5f);
            }
            SetMachineName(_machineName);
            SetUnknownState();
        }

        public void SetMachineName(string machineName)
        {
            _machineName = machineName;
            if (_machineNameText != null)
            {
                _machineNameText.text = machineName;
            }
        }

        public void SetButton(UnityAction action)
        {
            _button.onClick.AddListener(action);
        }

        public void SetState(MachineState state)
        {
            _lastState = state;
            bool showWarning = _hasWarning && state != MachineState.Fault && state != MachineState.Maintenance;

            if (_stateText != null)
            {
                string stateName = _theme != null
                    ? _theme.GetMachineStateDisplayName(state)
                    : state.ToString();
                // Warning reason is a code ("TemperatureDeviation") -> sheet key "warning.temperature_deviation".
                string reasonText = string.IsNullOrEmpty(_warningReason) ? string.Empty
                    : LocText.Get("warning." + LocText.Snake(_warningReason), _warningReason);
                _stateText.text = showWarning && reasonText.Length > 0
                    ? $"{stateName} – {reasonText}"
                    : stateName;
            }

            if (_stateLamp != null)
            {
                Color warningColor = _theme != null ? _theme.Warning : new Color(0.98f, 0.72f, 0.16f);
                _stateLamp.color = showWarning ? warningColor
                    : _theme != null ? _theme.GetMachineStateColor(state) : Color.grey;
            }
        }

        public void SetButton() => _button.interactable = true;

        private MachineState? _lastState;
        private bool _hasWarning;
        private string _warningReason = string.Empty;

        /// <summary>
        /// Non-blocking warning (MachineBase.SetWarning): lamp turns amber and the reason is shown next to
        /// the state, unless the machine is in Fault (red wins).
        /// </summary>
        public void SetWarning(bool hasWarning, string reason)
        {
            _hasWarning = hasWarning;
            _warningReason = hasWarning ? reason ?? string.Empty : string.Empty;
            if (_lastState.HasValue)
            {
                SetState(_lastState.Value);
            }
        }

        /// <summary>Placeholder state used until a machine is bound to this row.</summary>
        public void SetUnknownState()
        {
            if (_stateText != null)
            {
                _stateText.text = "--";
            }

            if (_stateLamp != null)
            {
                _stateLamp.color = _theme != null ? _theme.Inactive : Color.grey;
            }
        }
    }
}
