using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Small helpers to build UGUI elements from code (statistics page, pause menu), so these screens
    /// need no prefab work. Same look as <see cref="MachineParameterListView"/>: theme colours, TMP font
    /// copied from a style source.
    /// </summary>
    public static class HmiUiFactory
    {
        public static RectTransform CreateRect(string objectName, Transform parent)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject go, float spacing, int padding)
        {
            if (!go.TryGetComponent(out VerticalLayoutGroup layout))
            {
                layout = go.AddComponent<VerticalLayoutGroup>();
            }
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static RectTransform CreateRow(Transform parent, string rowName, float height, float spacing = 8f)
        {
            RectTransform rect = CreateRect(rowName, parent);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(8, 8, 2, 2);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            SetHeight(rect.gameObject, height);
            return rect;
        }

        public static TextMeshProUGUI CreateText(Transform parent, string objectName, string text, float size, Color color,
            TextAlignmentOptions alignment, TMP_FontAsset font = null)
        {
            RectTransform rect = CreateRect(objectName, parent);
            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                tmp.font = font;
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

        public static Image CreateImage(Transform parent, string objectName, Color color)
        {
            RectTransform rect = CreateRect(objectName, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Button with a centred label. The label is returned so it can be localized later.</summary>
        public static Button CreateButton(Transform parent, string objectName, string label, float fontSize,
            Color background, Color textColor, TMP_FontAsset font, out TextMeshProUGUI labelText)
        {
            Image image = CreateImage(parent, objectName, background);
            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            button.colors = colors;

            labelText = CreateText(image.transform, "Label", label, fontSize, textColor, TextAlignmentOptions.Center, font);
            Stretch((RectTransform)labelText.transform);
            return button;
        }

        public static LayoutElement SetHeight(GameObject go, float height)
        {
            if (!go.TryGetComponent(out LayoutElement element))
            {
                element = go.AddComponent<LayoutElement>();
            }
            element.minHeight = height;
            element.preferredHeight = height;
            return element;
        }

        public static LayoutElement SetWidth(GameObject go, float width)
        {
            if (!go.TryGetComponent(out LayoutElement element))
            {
                element = go.AddComponent<LayoutElement>();
            }
            element.minWidth = width;
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
            return element;
        }

        public static LayoutElement SetFlexible(GameObject go, float weight = 1f, float minWidth = 60f)
        {
            if (!go.TryGetComponent(out LayoutElement element))
            {
                element = go.AddComponent<LayoutElement>();
            }
            element.minWidth = minWidth;
            element.flexibleWidth = weight;
            return element;
        }
    }
}
