using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    public class AlarmEntryView : MonoBehaviour
    {
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private TextMeshProUGUI _sourceText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Image _severityBar;

        public void Bind(HmiAlarmEntry entry)
        {
            if (_sourceText != null)
            {
                _sourceText.text = entry.SourceName;
            }

            if (_messageText != null)
            {
                _messageText.text = entry.Message;
            }

            if (_severityBar != null && _theme != null)
            {
                _severityBar.color = entry.IsAcknowledged ? _theme.Warning : _theme.Alarm;
            }
        }
    }
}
