using Game.Production;
using TMPro;
using UnityEngine;
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
        [SerializeField] private SO_HmiInteractChannel _interactChannel;
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

        public void SetMachine(string machineId)
        {
            _button.onClick.AddListener(() => _interactChannel.Request(machineId));
        }

        public void SetState(MachineState state)
        {
            if (_stateText != null)
            {
                _stateText.text = _theme != null
                    ? _theme.GetMachineStateDisplayName(state)
                    : state.ToString();
            }

            if (_stateLamp != null)
            {
                _stateLamp.color = _theme != null ? _theme.GetMachineStateColor(state) : Color.grey;
            }
        }

        public void SetButton() => _button.interactable = true;

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
