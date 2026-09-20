using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Severity of a displayed reading, decided by whoever feeds the tile –
    /// never by the tile itself (same rule as sensors: raw value in, judgement
    /// stays with the caller).
    /// </summary>
    public enum HmiValueSeverity
    {
        Inactive,
        Normal,
        Warning,
        Alarm
    }

    /// <summary>
    /// One label/value readout in the operator panel: temperature, fill level,
    /// throughput, produced units, scrap count. Placeholder-friendly – shows
    /// "--" until something calls <see cref="SetValue"/>.
    /// </summary>
    public class StatusTileView : MonoBehaviour
    {
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private TextMeshProUGUI _valueText;
        [SerializeField] private TextMeshProUGUI _unitText;
        [SerializeField] private Image _severityBar;

        [Header("Defaults")]
        [SerializeField] private string _label = "LABEL";
        [SerializeField] private string _unit = "";
        [SerializeField] private string _placeholderValue = "--";

        private void Awake()
        {
            SetLabel(_label);
            SetUnit(_unit);
            SetPlaceholder();
        }

        public void SetLabel(string label)
        {
            _label = label;
            if (_labelText != null)
            {
                _labelText.text = label;
            }
        }

        public void SetUnit(string unit)
        {
            _unit = unit;
            if (_unitText != null)
            {
                _unitText.text = unit;
            }
        }

        public void SetPlaceholder()
        {
            SetValue(_placeholderValue, HmiValueSeverity.Inactive);
        }

        public void SetValue(float value, HmiValueSeverity severity, string format = "0.0")
        {
            SetValue(value.ToString(format), severity);
        }

        public void SetValue(int value, HmiValueSeverity severity)
        {
            SetValue(value.ToString(), severity);
        }

        public void SetValue(string value, HmiValueSeverity severity)
        {
            if (_valueText != null)
            {
                _valueText.text = value;
                _valueText.color = _theme != null ? _theme.ValueText : Color.white;
            }

            if (_severityBar != null)
            {
                _severityBar.color = ResolveSeverityColor(severity);
            }
        }

        private Color ResolveSeverityColor(HmiValueSeverity severity)
        {
            if (_theme == null)
            {
                return Color.grey;
            }

            return severity switch
            {
                HmiValueSeverity.Normal => _theme.Normal,
                HmiValueSeverity.Warning => _theme.Warning,
                HmiValueSeverity.Alarm => _theme.Alarm,
                _ => _theme.Inactive
            };
        }
    }
}
