using System.Collections.Generic;
using System.Globalization;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>Single read-only "label: value" line, e.g. "Mehl: 500 g".</summary>
    public readonly struct RecipeEntryData
    {
        public readonly string Label;
        public readonly string Value;

        public RecipeEntryData(string label, string value)
        {
            Label = label;
            Value = value;
        }
    }

    public class RecipeEntryRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _labelText;
        [SerializeField] private TextMeshProUGUI _valueText;

        public void Bind(RecipeEntryData entry)
        {
            if (_labelText != null)
            {
                _labelText.text = entry.Label;
            }

            if (_valueText != null)
            {
                _valueText.text = entry.Value;
            }
        }
    }

    /// <summary>
    /// HMI page "recipe" (panel id "recipe"): the active recipe's setpoints, grouped by station in line order
    /// (mixer → packaging). The operator reads them here and sets them at each machine's terminal - the recipe
    /// is never bound to the machines in code (architecture convention).
    ///
    /// Content is built from code under the page's scroll view content (like <see cref="StatisticsPanel"/>),
    /// texts come from the string table (station names "text.*", labels "hmi.*"/"recipe.*", English fallback)
    /// and are rebuilt after a language change. Values that are 0 in the recipe are left out.
    /// </summary>
    public class RecipeTerminalPanel : HmiPanelBase
    {
        [SerializeField] private TextMeshProUGUI _recipeNameText;

        [Tooltip("Recipe shown on this page (MVP: one active recipe).")]
        [SerializeField] private SO_RecipeDefinition _recipe;

        [SerializeField] private SO_HmiTheme _theme;

        [Tooltip("Parent for the generated rows. Empty = content of the page's scroll view.")]
        [SerializeField] private RectTransform _contentRoot;

        [Tooltip("Font is copied from this text. Empty = font of the recipe name text.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [SerializeField] private float _fontSize = 26f;

        private RectTransform _generatedRoot;

        private TMP_FontAsset Font => _styleSource != null ? _styleSource.font
            : _recipeNameText != null ? _recipeNameText.font : null;
        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f);
        private Color ValueColor => _theme != null ? _theme.ValueText : Color.white;
        private Color SectionColor => _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f);

        private void OnEnable()
        {
            LocText.TableChanged += Rebuild;
            // The HMI root may be activated with this page already visible - OnVisibilityChanged does not fire then.
            if (IsVisible)
            {
                Rebuild();
            }
        }

        private void OnDisable() => LocText.TableChanged -= Rebuild;

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                Rebuild();
            }
        }

        public void SetRecipe(SO_RecipeDefinition recipe)
        {
            _recipe = recipe;
            Rebuild();
        }

        public void SetRecipeName(string recipeName)
        {
            if (_recipeNameText != null)
            {
                _recipeNameText.text = recipeName;
            }
        }

        // ---- Content ----

        private void Rebuild()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            RectTransform root = GetGeneratedRoot();
            for (int i = root.childCount - 1; i >= 1; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }

            string title = LocText.Get("text.recipe", "Recipe");
            SetRecipeName(_recipe != null ? $"{title}: {_recipe.DisplayName}" : title);

            if (_recipe == null)
            {
                AddHint(root, LocText.Get("text.recipe_empty", "No recipe loaded"));
                return;
            }

            AddHint(root, LocText.Get("recipe.hint", "Set these values at each machine's terminal."));

            SO_RecipeDefinition r = _recipe;
            AddSection(root, "text.mixer", "Dough mixer", new[]
            {
                Entry("recipe.flour", "Flour", r.FlourGrams, "g"),
                Entry("recipe.water", "Water", r.WaterMilliliters, "ml"),
                Entry("recipe.yeast", "Yeast", r.YeastGrams, "g"),
                Entry("recipe.salt", "Salt", r.SaltGrams, "g"),
                Entry("recipe.olive_oil", "Olive oil", r.OliveOilMilliliters, "ml"),
                Entry("hmi.mixing_time", "Mixing time", r.MixingDurationSeconds, "s"),
            });
            AddSection(root, "text.portioner", "Portioner", new[]
            {
                Entry("hmi.target_weight", "Target weight", r.TargetPortionWeightGrams, "g", r.PortionWeightToleranceGrams),
            });
            AddSection(root, "text.dough_press", "Dough press", new[]
            {
                Entry("hmi.diameter", "Diameter", r.BaseDiameterCentimeters, "cm"),
                Entry("hmi.thickness", "Thickness", r.BaseThicknessMillimeters, "mm"),
            });
            AddSection(root, "text.dosing_station", "Dosing station", new[]
            {
                Entry("hmi.sauce", "Sauce", r.SauceGrams, "g"),
                Entry("hmi.topping", "Topping", r.ToppingGrams, "g"),
            });
            AddSection(root, "text.oven", "Oven", new[]
            {
                Entry("hmi.bake_time", "Bake time", r.TargetBakeTimeSeconds, "s", r.BakeTimeToleranceSeconds),
                Entry("hmi.temperature", "Temperature", r.TargetBakeTemperatureCelsius, "°C", r.BakeTemperatureToleranceCelsius),
            });
            AddSection(root, "text.cooling", "Cooling tunnel", new[]
            {
                Entry("hmi.temperature", "Temperature", r.CoolingTemperatureCelsius, "°C"),
                Entry("hmi.dwell_time", "Dwell time", r.CoolingDurationSeconds, "s"),
            });
            AddSection(root, "text.freezer", "Shock freezer", new[]
            {
                Entry("hmi.temperature", "Temperature", r.FreezingTemperatureCelsius, "°C"),
                Entry("hmi.dwell_time", "Dwell time", r.FreezingDurationSeconds, "s"),
            });
            AddSection(root, "text.packaging", "Packaging", new[]
            {
                Entry("hmi.dwell_time", "Dwell time", r.PackagingDurationSeconds, "s"),
            });
        }

        private RecipeEntryData? Entry(string key, string fallback, float value, string unit, float tolerance = 0f)
        {
            if (Mathf.Approximately(value, 0f))
            {
                return null;
            }

            CultureInfo culture = Culture();
            string text = $"{value.ToString("0.##", culture)} {unit}";
            if (tolerance > 0f)
            {
                text += $"  ± {tolerance.ToString("0.##", culture)} {unit}";
            }
            return new RecipeEntryData(LocText.Get(key, fallback), text);
        }

        private void AddSection(RectTransform root, string titleKey, string titleFallback, RecipeEntryData?[] entries)
        {
            var visible = new List<RecipeEntryData>();
            foreach (RecipeEntryData? entry in entries)
            {
                if (entry.HasValue)
                {
                    visible.Add(entry.Value);
                }
            }

            if (visible.Count == 0)
            {
                return;
            }

            Image header = HmiUiFactory.CreateImage(root, "Section", SectionColor);
            HmiUiFactory.SetHeight(header.gameObject, _fontSize * 1.8f);
            TextMeshProUGUI title = HmiUiFactory.CreateText(header.transform, "Title", LocText.Get(titleKey, titleFallback),
                _fontSize * 1.05f, ValueColor, TextAlignmentOptions.MidlineLeft, Font);
            title.fontStyle = FontStyles.Bold;
            var titleRect = (RectTransform)title.transform;
            HmiUiFactory.Stretch(titleRect);
            titleRect.offsetMin = new Vector2(12f, 0f); // horizontal inset only - a lower box than the font hides the text
            titleRect.offsetMax = new Vector2(-12f, 0f);

            foreach (RecipeEntryData entry in visible)
            {
                RectTransform row = HmiUiFactory.CreateRow(root, "Entry", _fontSize * 1.5f, 16f);
                row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(28, 12, 2, 2);
                TextMeshProUGUI label = HmiUiFactory.CreateText(row, "Label", entry.Label, _fontSize, LabelColor,
                    TextAlignmentOptions.MidlineLeft, Font);
                HmiUiFactory.SetFlexible(label.gameObject);
                TextMeshProUGUI value = HmiUiFactory.CreateText(row, "Value", entry.Value, _fontSize, ValueColor,
                    TextAlignmentOptions.MidlineRight, Font);
                HmiUiFactory.SetWidth(value.gameObject, _fontSize * 10f);
            }
        }

        private void AddHint(RectTransform root, string text)
        {
            TextMeshProUGUI hint = HmiUiFactory.CreateText(root, "Hint", text, _fontSize * 0.85f, LabelColor,
                TextAlignmentOptions.MidlineLeft, Font);
            hint.fontStyle = FontStyles.Italic;
            HmiUiFactory.SetHeight(hint.gameObject, _fontSize * 1.6f);
        }

        /// <summary>
        /// Container for the generated rows inside the scroll view content. Placeholder children of the
        /// content (from the prefab) are hidden; the one holding the recipe name stays.
        /// </summary>
        private RectTransform GetGeneratedRoot()
        {
            if (_generatedRoot != null)
            {
                return _generatedRoot;
            }

            RectTransform content = _contentRoot;
            if (content == null)
            {
                ScrollRect scroll = GetComponentInChildren<ScrollRect>(true);
                content = scroll != null && scroll.content != null ? scroll.content : (RectTransform)transform;
            }

            foreach (Transform child in content)
            {
                bool holdsName = _recipeNameText != null && _recipeNameText.transform.IsChildOf(child);
                if (!holdsName)
                {
                    child.gameObject.SetActive(false);
                }
            }

            _generatedRoot = content;
            return _generatedRoot;
        }

        private static CultureInfo Culture()
        {
            CultureInfo culture = LocalizationSettings.HasSettings && LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.CultureInfo
                : null;
            return culture ?? CultureInfo.InvariantCulture;
        }
    }
}
