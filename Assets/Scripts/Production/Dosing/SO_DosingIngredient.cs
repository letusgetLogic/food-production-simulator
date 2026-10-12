using Game.Core;
using System.Text;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// One ingredient the dosing station can apply (tomato sauce, mozzarella, ...). Content only: to add an
    /// ingredient, create a new asset and a <see cref="DosingUnit"/> for it; to replace one, assign another
    /// asset to the unit. Quality spec, recipe and pizza visuals refer to the ingredient by <see cref="Id"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Ingredient", menuName = "Production/Dosing Ingredient")]
    public class SO_DosingIngredient : ScriptableObject
    {
        [Tooltip("Stable id (save files, quality spec, pizza visuals), lower case, e.g. \"sauce\", \"mozzarella\". Do not change once used.")]
        public string Id = "sauce";

        [Tooltip("English name. Localization key = hmi.<name in snake_case> (e.g. hmi.mozzarella).")]
        public string DisplayNameFallback = "Sauce";

        [Header("Operator setpoint (grams per pizza)")]
        [Min(0f)] public float DefaultGrams = 80f;
        [Min(0f)] public float MinGrams = 40f;
        [Min(0f)] public float MaxGrams = 150f;
        [Tooltip("Step of the setpoint at the machine terminal.")]
        [Min(0.1f)] public float StepGrams = 5f;
        [Tooltip("Display tolerance for the 'last dose' readout (amber outside).")]
        [Min(0f)] public float ToleranceGrams = 8f;

        [Header("Reservoir")]
        [Tooltip("Reservoir capacity in grams (10 kg = about 125 pizzas at 80 g).")]
        [Min(1f)] public float ReservoirCapacityGrams = 10000f;

        [Header("Look")]
        [Tooltip("Optional: material for the unit's head, reservoir and refill button (see DosingUnit).")]
        public Material DispenserMaterial;

        /// <summary>Localization key of the name ("hmi.mozzarella").</summary>
        public string NameKey => LocText.KeyFromText("hmi", DisplayNameFallback);

        /// <summary>Translated name.</summary>
        public string DisplayName => LocText.Get(NameKey, DisplayNameFallback);

        /// <summary>Prefix of the quality defect codes, e.g. "Mozzarella" -> MozzarellaLow / MozzarellaHigh.</summary>
        public string DefectCode => CodeOf(Id);

        /// <summary>"sauce" -> "Sauce", "tomato_sauce" -> "TomatoSauce".</summary>
        public static string CodeOf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return "Ingredient";
            }

            var builder = new StringBuilder(id.Length);
            bool upper = true;
            foreach (char c in id)
            {
                if (c == '_' || c == '-' || c == ' ')
                {
                    upper = true;
                    continue;
                }

                builder.Append(upper ? char.ToUpperInvariant(c) : c);
                upper = false;
            }
            return builder.ToString();
        }

        private void OnValidate()
        {
            MaxGrams = Mathf.Max(MaxGrams, MinGrams);
            DefaultGrams = Mathf.Clamp(DefaultGrams, MinGrams, MaxGrams);
        }
    }
}
