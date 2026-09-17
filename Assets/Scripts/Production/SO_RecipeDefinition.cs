using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Defines the ingredients, target process parameters, and quality tolerances for a single
    /// pizza recipe. Content-only data (no logic) so that designers/PM can author and tune recipes
    /// entirely in the Unity editor without touching code.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDefinition", menuName = "Production/Recipe Definition")]
    public class SO_RecipeDefinition : ScriptableObject
    {
        [Header("Identity")]

        /// <summary>Stable unique identifier for this recipe, used for save/load and cross-references (independent of the asset's display name).</summary>
        public string RecipeId;

        /// <summary>Human-readable display name (e.g. "Margherita").</summary>
        public string DisplayName;

        [Header("Dough Ingredients")]

        /// <summary>Amount of flour required per dough batch, in grams.</summary>
        public float FlourGrams;

        /// <summary>Amount of water required per dough batch, in milliliters.</summary>
        public float WaterMilliliters;

        /// <summary>Amount of yeast required per dough batch, in grams.</summary>
        public float YeastGrams;

        /// <summary>Amount of salt required per dough batch, in grams.</summary>
        public float SaltGrams;

        /// <summary>Amount of olive oil required per dough batch, in milliliters.</summary>
        public float OliveOilMilliliters;

        [Header("Portioning")]

        /// <summary>Target dough weight for a single portioned pizza base, in grams.</summary>
        public float TargetPortionWeightGrams;

        /// <summary>Acceptable deviation from <see cref="TargetPortionWeightGrams"/> before a quality fault is raised, in grams.</summary>
        public float PortionWeightToleranceGrams;

        [Header("Baking")]

        /// <summary>Target time the pizza should spend in the oven, in seconds.</summary>
        public float TargetBakeTimeSeconds;

        /// <summary>Acceptable deviation from <see cref="TargetBakeTimeSeconds"/> before a quality fault is raised, in seconds.</summary>
        public float BakeTimeToleranceSeconds;

        /// <summary>Target oven temperature, in degrees Celsius.</summary>
        public float TargetBakeTemperatureCelsius;

        /// <summary>Acceptable deviation from <see cref="TargetBakeTemperatureCelsius"/> before a quality fault is raised, in degrees Celsius.</summary>
        public float BakeTemperatureToleranceCelsius;

        [Header("Final Product Quality")]

        /// <summary>Target total weight of the finished, packaged pizza, in grams.</summary>
        public float TargetFinalWeightGrams;

        /// <summary>Acceptable deviation from <see cref="TargetFinalWeightGrams"/> before a quality fault is raised, in grams.</summary>
        public float FinalWeightToleranceGrams;
    }
}
