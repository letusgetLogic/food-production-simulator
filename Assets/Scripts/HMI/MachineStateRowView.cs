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
    public class MachineStateRowView : MonoBehaviour
    {
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private TextMeshProUGUI _machineNameText;
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private Image _stateLamp;

        [Header("Defaults")]
        [SerializeField] private string _machineName = "MACHINE";

        public string MachineName => _machineName;

        private void Awake()
        {
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
