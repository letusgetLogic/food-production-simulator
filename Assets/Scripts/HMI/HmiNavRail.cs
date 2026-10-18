using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Navigation row of the operator screen (Overview, Recipe, up to two machine buttons). The active buttons
    /// share the full row width (small gap between them, no empty space at the end), labels shrink to fit long
    /// translations ("Descripción general") instead of wrapping or being cut off, and the look comes from the
    /// <see cref="SO_HmiTheme"/>: dark tabs, the page that is shown is highlighted with the "Normal" accent.
    /// Styling is applied at runtime, so the buttons in the prefab stay plain.
    /// </summary>
    [RequireComponent(typeof(HorizontalLayoutGroup))]
    public class HmiNavRail : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public Button Button;
            [Tooltip("Panel the button shows (overview, recipe, machine) - used when the screen opens a page itself.")]
            public string PanelId;
        }

        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private List<Entry> _entries = new List<Entry>();
        [Tooltip("\"X\" button that closes the screen - placed at the right end of the row, the tabs leave room for it.")]
        [SerializeField] private Button _closeButton;
        [SerializeField] private float _closeLabelSize = 24f;

        [Header("Layout")]
        [SerializeField] private float _spacing = 6f;
        [SerializeField] private int _paddingHorizontal = 8;
        [SerializeField] private int _paddingVertical = 6;
        [SerializeField] private float _labelMargin = 8f;
        [Tooltip("Smallest label size before a text is cut with an ellipsis.")]
        [SerializeField] private float _minFontSize = 12f;
        [SerializeField] private float _maxFontSize = 22f;
        [SerializeField] private float _activeBarHeight = 3f;

        private readonly Dictionary<Button, Image> _activeBars = new Dictionary<Button, Image>();
        private readonly Dictionary<Button, UnityEngine.Events.UnityAction> _clickHandlers =
            new Dictionary<Button, UnityEngine.Events.UnityAction>();
        private Button _active;
        private bool _styled;

        private Color Tab => _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f, 1f);
        private Color Border => _theme != null ? _theme.PanelBorder : new Color(0.22f, 0.26f, 0.30f, 1f);
        private Color Accent => _theme != null ? _theme.Normal : new Color(0.30f, 0.80f, 0.45f, 1f);
        private Color LabelText => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f, 1f);
        private Color ValueText => _theme != null ? _theme.ValueText : Color.white;
        private Color Inactive => _theme != null ? _theme.Inactive : Color.grey;
        private Color Alarm => _theme != null ? _theme.Alarm : new Color(0.92f, 0.28f, 0.24f, 1f);

        private void Awake()
        {
            ApplyLayout();
            foreach (Entry entry in _entries)
            {
                if (entry?.Button != null)
                {
                    Style(entry.Button);
                }
            }
            PlaceCloseButton();
            _styled = true;
            RefreshHighlight();
        }

        /// <summary>Square "X" at the right end of the row (same height as the tabs, not localized).</summary>
        private void PlaceCloseButton()
        {
            if (_closeButton == null)
            {
                return;
            }

            var railRect = (RectTransform)transform;
            float size = Mathf.Max(20f, railRect.rect.height > 0f ? railRect.rect.height - 2f * _paddingVertical : 38f);

            var rect = (RectTransform)_closeButton.transform;
            rect.SetParent(railRect.parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-_paddingHorizontal, railRect.anchoredPosition.y - _paddingVertical);
            rect.sizeDelta = new Vector2(size, size);
            rect.SetAsLastSibling();

            // The tabs end one gap before the button.
            railRect.offsetMax = new Vector2(-(size + _spacing), railRect.offsetMax.y);

            if (_closeButton.targetGraphic is Image image)
            {
                image.color = Color.white;
            }
            ColorBlock colors = _closeButton.colors;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            colors.normalColor = Tab;
            colors.selectedColor = Tab;
            colors.highlightedColor = Color.Lerp(Tab, Alarm, 0.55f);
            colors.pressedColor = Alarm;
            colors.disabledColor = Color.Lerp(Tab, Inactive, 0.3f);
            _closeButton.colors = colors;

            TextMeshProUGUI label = _closeButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = "X";
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                label.enableAutoSizing = false;
                label.fontSize = _closeLabelSize;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.color = ValueText;
            }
        }

        private void OnEnable()
        {
            foreach (Entry entry in _entries)
            {
                if (entry?.Button == null || _clickHandlers.ContainsKey(entry.Button))
                {
                    continue;
                }

                Button button = entry.Button;
                UnityEngine.Events.UnityAction handler = () => Highlight(button);
                button.onClick.AddListener(handler);
                _clickHandlers.Add(button, handler);
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<Button, UnityEngine.Events.UnityAction> pair in _clickHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.onClick.RemoveListener(pair.Value);
                }
            }
            _clickHandlers.Clear();
        }

        /// <summary>Marks the button of the shown page.</summary>
        public void Highlight(Button button)
        {
            _active = button;
            RefreshHighlight();
        }

        /// <summary>Marks the first active button that shows <paramref name="panelId"/> (page opened by the screen itself).</summary>
        public void Highlight(string panelId)
        {
            Entry match = _entries.Find(e => e?.Button != null && e.PanelId == panelId && e.Button.gameObject.activeSelf);
            Highlight(match?.Button);
        }

        private void ApplyLayout()
        {
            var layout = GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(_paddingHorizontal, _paddingHorizontal, _paddingVertical, _paddingVertical);
            layout.spacing = _spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            // Every active button gets the same share of the row: 1 to 4 buttons always fill it.
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;
        }

        private void Style(Button button)
        {
            // Equal shares: no preferred width from the button itself (the image would report its sprite size).
            LayoutElement element = button.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = button.gameObject.AddComponent<LayoutElement>();
            }
            element.minWidth = 0f;
            element.preferredWidth = 0f;
            element.flexibleWidth = 1f;

            if (button.targetGraphic is Image image)
            {
                image.color = Color.white; // the ColorBlock below carries the theme colours
            }

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                var rect = (RectTransform)label.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(_labelMargin, _activeBarHeight);
                rect.offsetMax = new Vector2(-_labelMargin, 0f);

                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.enableAutoSizing = true;
                label.fontSizeMax = Mathf.Max(_minFontSize, _maxFontSize);
                label.fontSizeMin = _minFontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.characterSpacing = 1f;
            }

            Image bar = HmiUiFactory.CreateImage(button.transform, "ActiveBar", Accent);
            bar.raycastTarget = false;
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = new Vector2(0f, _activeBarHeight);
            _activeBars[button] = bar;
        }

        private void RefreshHighlight()
        {
            if (!_styled)
            {
                return;
            }

            foreach (Entry entry in _entries)
            {
                Button button = entry?.Button;
                if (button == null)
                {
                    continue;
                }

                bool active = button == _active;
                Color baseColor = active ? Color.Lerp(Tab, Accent, 0.18f) : Tab;
                ColorBlock colors = button.colors;
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.08f;
                colors.normalColor = baseColor;
                colors.selectedColor = baseColor;
                colors.highlightedColor = Color.Lerp(baseColor, Border, 0.6f);
                colors.pressedColor = Color.Lerp(Tab, Accent, 0.32f);
                colors.disabledColor = Color.Lerp(Tab, Inactive, 0.3f);
                button.colors = colors;

                TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.color = active ? ValueText : LabelText;
                    label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                }

                if (_activeBars.TryGetValue(button, out Image bar) && bar != null)
                {
                    bar.enabled = active;
                }
            }
        }
    }
}
