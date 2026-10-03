using System.Collections.Generic;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// "Setpoints" and "Actual values" section of the machine panel. Rows are built at runtime from
    /// the bound machine's <see cref="IMachineParameterSource"/>, so one panel serves every machine
    /// type (Portioner, Press, later Dosing/Oven ...) without per-machine UI prefabs.
    /// Stays a dumb view: it only knows MachineParameter/MachineReadout, never a concrete machine.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class MachineParameterListView : MonoBehaviour
    {
        [SerializeField] private SO_HmiTheme _theme;

        [Tooltip("Font and base size are copied from this text (e.g. the panel's content text). Falls back to the TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [Header("Layout")]
        [SerializeField] private float _rowHeight = 44f;
        [SerializeField] private float _headerHeight = 36f;
        [SerializeField] private float _spacing = 4f;
        [SerializeField] private float _fontSize = 24f;
        [SerializeField] private float _valueWidth = 150f;
        [SerializeField] private float _unitWidth = 70f;
        [SerializeField] private float _buttonWidth = 56f;

        [Header("Texts (fallback until localization keys exist)")]
        [SerializeField] private string _setpointsHeader = "SETPOINTS";
        [SerializeField] private string _actualValuesHeader = "ACTUAL VALUES";

        private sealed class ParameterRow
        {
            public MachineParameter Parameter;
            public TextMeshProUGUI LabelText;
            public TextMeshProUGUI UnitText;
            public TextMeshProUGUI ValueText;
            public Button DecreaseButton;
            public Button IncreaseButton;
        }

        private sealed class ReadoutRow
        {
            public MachineReadout Readout;
            public TextMeshProUGUI LabelText;
            public TextMeshProUGUI UnitText;
            public TextMeshProUGUI ValueText;
            public Image SeverityBar;
        }

        private readonly List<ParameterRow> _parameterRows = new List<ParameterRow>();
        private readonly List<ReadoutRow> _readoutRows = new List<ReadoutRow>();
        private bool _editable = true;
        private RectTransform _rect;
        private LayoutElement _layoutElement;

        private RectTransform Rect => _rect != null ? _rect : _rect = (RectTransform)transform;

        /// <summary>
        /// Rebuilds all rows. <paramref name="readouts"/> are the readouts that are NOT shown in a fixed
        /// tile of the panel. A null/empty source hides the section.
        /// </summary>
        public void Build(IReadOnlyList<MachineParameter> parameters, IReadOnlyList<MachineReadout> readouts)
        {
            Clear();
            EnsureLayout();

            float height = 0f;
            int rowCount = 0;

            if (parameters != null && parameters.Count > 0)
            {
                CreateHeader(LocText.Get("hmi.setpoints", _setpointsHeader));
                height += _headerHeight;
                rowCount++;

                foreach (MachineParameter parameter in parameters)
                {
                    _parameterRows.Add(CreateParameterRow(parameter));
                    height += _rowHeight;
                    rowCount++;
                }
            }

            if (readouts != null && readouts.Count > 0)
            {
                CreateHeader(LocText.Get("hmi.actual_values", _actualValuesHeader));
                height += _headerHeight;
                rowCount++;

                foreach (MachineReadout readout in readouts)
                {
                    _readoutRows.Add(CreateReadoutRow(readout));
                    height += _rowHeight;
                    rowCount++;
                }
            }

            if (rowCount > 1)
            {
                height += (rowCount - 1) * _spacing;
            }

            // Works inside a parent layout group (LayoutElement) and without one (sizeDelta).
            Rect.sizeDelta = new Vector2(Rect.sizeDelta.x, height);
            _layoutElement.minHeight = height;
            _layoutElement.preferredHeight = height;

            gameObject.SetActive(rowCount > 0);
            ApplyEditable();
            Refresh();
        }

        public void Clear()
        {
            _parameterRows.Clear();
            _readoutRows.Clear();

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        /// <summary>Terminal = editable, tablet = read-only (step buttons hidden).</summary>
        public void SetEditable(bool editable)
        {
            _editable = editable;
            ApplyEditable();
        }

        /// <summary>Pulls the current values from the machine. Called periodically by the binder.</summary>
        public void Refresh()
        {
            // Labels/units are re-read every refresh so a language switch shows up without rebuilding.
            foreach (ParameterRow row in _parameterRows)
            {
                row.LabelText.text = row.Parameter.Label;
                row.UnitText.text = row.Parameter.Unit;
                row.ValueText.text = row.Parameter.FormattedValue;

                float value = row.Parameter.Value;
                row.DecreaseButton.interactable = _editable && value > row.Parameter.Min + 0.0001f;
                row.IncreaseButton.interactable = _editable && value < row.Parameter.Max - 0.0001f;
            }

            foreach (ReadoutRow row in _readoutRows)
            {
                row.LabelText.text = row.Readout.Label;
                row.UnitText.text = row.Readout.Unit;
                row.ValueText.text = row.Readout.Text;
                row.SeverityBar.color = ResolveColor(row.Readout.Level);
            }
        }

        private void ApplyEditable()
        {
            foreach (ParameterRow row in _parameterRows)
            {
                row.DecreaseButton.gameObject.SetActive(_editable);
                row.IncreaseButton.gameObject.SetActive(_editable);
            }
        }

        // ---- Row construction ----

        private void EnsureLayout()
        {
            if (!TryGetComponent(out VerticalLayoutGroup layout))
            {
                layout = gameObject.AddComponent<VerticalLayoutGroup>();
            }

            layout.spacing = _spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            if (!TryGetComponent(out _layoutElement))
            {
                _layoutElement = gameObject.AddComponent<LayoutElement>();
            }
        }

        private void CreateHeader(string text)
        {
            RectTransform row = CreateRow("Header", _headerHeight);
            TextMeshProUGUI label = CreateText(row, "Label", text, _fontSize * 0.8f, LabelColor, TextAlignmentOptions.BottomLeft);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 4f;
            AddFlexible(label.gameObject, 1f);
        }

        private ParameterRow CreateParameterRow(MachineParameter parameter)
        {
            RectTransform row = CreateRow("Setpoint_" + parameter.Id, _rowHeight);
            AddBackground(row);

            TextMeshProUGUI label = CreateText(row, "Label", parameter.Label, _fontSize, LabelColor, TextAlignmentOptions.MidlineLeft);
            AddFlexible(label.gameObject, 1f);

            Button decrease = CreateStepButton(row, "Decrease", "−", -1, parameter);

            TextMeshProUGUI value = CreateText(row, "Value", "--", _fontSize, ValueColor, TextAlignmentOptions.MidlineRight);
            value.fontStyle = FontStyles.Bold;
            AddFixed(value.gameObject, _valueWidth);

            TextMeshProUGUI unit = CreateText(row, "Unit", parameter.Unit, _fontSize * 0.85f, LabelColor, TextAlignmentOptions.MidlineLeft);
            AddFixed(unit.gameObject, _unitWidth);

            Button increase = CreateStepButton(row, "Increase", "+", 1, parameter);

            return new ParameterRow
            {
                Parameter = parameter,
                LabelText = label,
                UnitText = unit,
                ValueText = value,
                DecreaseButton = decrease,
                IncreaseButton = increase
            };
        }

        private ReadoutRow CreateReadoutRow(MachineReadout readout)
        {
            RectTransform row = CreateRow("Readout_" + readout.Id, _rowHeight);
            AddBackground(row);

            Image bar = CreateImage(row, "SeverityBar", Inactive);
            AddFixed(bar.gameObject, 6f);

            TextMeshProUGUI label = CreateText(row, "Label", readout.Label, _fontSize, LabelColor, TextAlignmentOptions.MidlineLeft);
            AddFlexible(label.gameObject, 1f);

            TextMeshProUGUI value = CreateText(row, "Value", "--", _fontSize, ValueColor, TextAlignmentOptions.MidlineRight);
            value.fontStyle = FontStyles.Bold;
            AddFixed(value.gameObject, _valueWidth + _buttonWidth);

            TextMeshProUGUI unit = CreateText(row, "Unit", readout.Unit, _fontSize * 0.85f, LabelColor, TextAlignmentOptions.MidlineLeft);
            AddFixed(unit.gameObject, _unitWidth + _buttonWidth);

            return new ReadoutRow
            {
                Readout = readout,
                LabelText = label,
                UnitText = unit,
                ValueText = value,
                SeverityBar = bar
            };
        }

        private RectTransform CreateRow(string rowName, float height)
        {
            var go = new GameObject(rowName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);

            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var element = go.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            return rect;
        }

        private void AddBackground(RectTransform row)
        {
            var image = row.gameObject.AddComponent<Image>();
            image.color = _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f, 1f);
            image.raycastTarget = false;
        }

        private Button CreateStepButton(RectTransform row, string buttonName, string symbol, int direction, MachineParameter parameter)
        {
            Image image = CreateImage(row, buttonName, _theme != null ? _theme.PanelBorder : new Color(0.22f, 0.26f, 0.30f, 1f));
            image.raycastTarget = true;
            AddFixed(image.gameObject, _buttonWidth);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            button.colors = colors;

            TextMeshProUGUI text = CreateText((RectTransform)image.transform, "Symbol", symbol, _fontSize * 1.2f, ValueColor, TextAlignmentOptions.Center);
            var textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var repeat = image.gameObject.AddComponent<HmiRepeatButton>();
            repeat.Stepped += () =>
            {
                parameter.Nudge(direction);
                Refresh();
            };

            return button;
        }

        private TextMeshProUGUI CreateText(RectTransform parent, string objectName, string text, float size, Color color,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (_styleSource != null && _styleSource.font != null)
            {
                tmp.font = _styleSource.font;
            }
            else if (TMP_Settings.defaultFontAsset != null)
            {
                tmp.font = TMP_Settings.defaultFontAsset;
            }

            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Image CreateImage(RectTransform parent, string objectName, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void AddFixed(GameObject go, float width)
        {
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }

        private static void AddFlexible(GameObject go, float weight)
        {
            var element = go.AddComponent<LayoutElement>();
            element.minWidth = 80f;
            element.flexibleWidth = weight;
        }

        // ---- Theme ----

        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f, 1f);
        private Color ValueColor => _theme != null ? _theme.ValueText : Color.white;
        private Color Inactive => _theme != null ? _theme.Inactive : Color.grey;

        private Color ResolveColor(MachineValueLevel level)
        {
            if (_theme == null)
            {
                return Color.grey;
            }

            return level switch
            {
                MachineValueLevel.Normal => _theme.Normal,
                MachineValueLevel.Warning => _theme.Warning,
                MachineValueLevel.Alarm => _theme.Alarm,
                _ => _theme.Inactive
            };
        }
    }
}
