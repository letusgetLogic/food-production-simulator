using System;

namespace Game.Production
{
    /// <summary>
    /// Represents a single, individual product instance as it travels through the production line.
    /// Tracks its current <see cref="ProductState"/>, the recipe it was created from, and measured
    /// values gathered along the way (e.g. actual weight) that later feed into quality checks.
    /// </summary>
    /// <remarks>
    /// Implemented as a class (reference type) rather than a struct. Reasoning:
    /// <list type="bullet">
    /// <item>
    /// Identity matters: a single physical pizza on the line needs a stable identity as it is
    /// handed from conveyor to conveyor and from station to station. A struct would be copied by
    /// value on every assignment/parameter pass, making it easy to accidentally mutate a copy
    /// (e.g. update <see cref="CurrentState"/> on a local copy) while the "real" product on the
    /// conveyor remains unchanged. A class guarantees every reference points to the same instance.
    /// </item>
    /// <item>
    /// It already holds a reference to a <see cref="RecipeDefinition"/> (a UnityEngine.Object /
    /// ScriptableObject asset), so it is not a small, self-contained value type to begin with -
    /// there is no perf upside to a struct here, and structs containing object references still
    /// get boxed/copied on every pass, which is wasted work for a moderately sized payload that
    /// will grow (more measured values over time).
    /// </item>
    /// <item>
    /// For JSON save/load: a class serializes just as easily as a struct with most JSON libraries
    /// (Unity JsonUtility, Newtonsoft.Json). On load we deserialize into a fresh instance and then
    /// re-resolve <see cref="Recipe"/> from its <see cref="RecipeId"/> (ScriptableObject references
    /// are not meaningfully serializable across sessions/asset GUID changes anyway), which works
    /// the same whether ProductInstance is a class or a struct - so this criterion is neutral, but
    /// it does not argue for a struct either.
    /// </item>
    /// </list>
    /// </remarks>
    [Serializable]
    public class ProductInstance

    {
        /// <summary>Unique identifier for this specific product instance (e.g. for debugging, logging, save/load).</summary>
        public string InstanceId;

        /// <summary>The current position of this product within the production sequence.</summary>
        public ProductState CurrentState;

        /// <summary>
        /// The recipe this product instance was created from. May be null right after deserialization
        /// until re-resolved via <see cref="RecipeId"/> against the loaded recipe catalog.
        /// </summary>
        public RecipeDefinition Recipe;

        /// <summary>
        /// Stable string identifier of <see cref="Recipe"/>, stored alongside the direct reference so the
        /// correct RecipeDefinition asset can be re-resolved after a JSON save/load round-trip.
        /// </summary>
        public string RecipeId;

        /// <summary>Actual measured weight of the product (in grams), set once a weight sensor/station has processed it.</summary>
        public float? MeasuredWeightGrams;

        /// <summary>Actual measured core/surface temperature of the product (in °C), set by relevant stations.</summary>
        public float? MeasuredTemperatureCelsius;

        /// <summary>Actual bake duration the product went through (in seconds), set by the baking station.</summary>
        public float? ActualBakeTimeSeconds;

        /// <summary>Creates a new product instance in its initial state.</summary>
        public ProductInstance(string instanceId, RecipeDefinition recipe, ProductState initialState = ProductState.RawDough)
        {
            InstanceId = instanceId;
            Recipe = recipe;
            RecipeId = recipe != null ? recipe.RecipeId : null;
            CurrentState = initialState;
        }

        /// <summary>Parameterless constructor for JSON deserialization.</summary>
        public ProductInstance()
        {
        }
    }
}
