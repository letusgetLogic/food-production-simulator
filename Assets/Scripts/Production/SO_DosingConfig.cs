using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content-driven configuration for <see cref="DosingMachine"/> (sauce + topping dosing station).
    /// Setpoints are set by the operator at the machine terminal, not bound to a RecipeDefinition in code.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_DosingConfig", menuName = "Production/Machines/Dosing Config")]
    public class SO_DosingConfig : ScriptableObject
    {
        [Header("Timing")]
        [Min(0f)] public float StartupDurationSeconds = 1f;
        [Min(0f)] public float ShutdownDurationSeconds = 1f;

        [Header("Sauce (operator setpoint, grams per pizza)")]
        [Min(0f)] public float DefaultSauceGrams = 80f;
        [Min(0f)] public float MinSauceGrams = 40f;
        [Min(0f)] public float MaxSauceGrams = 140f;
        [Min(0.1f)] public float SauceStepGrams = 5f;
        [Tooltip("Display tolerance for the 'last dose' readout (Margherita: 80 g +/- 10 %).")]
        [Min(0f)] public float SauceToleranceGrams = 8f;

        [Header("Topping (operator setpoint, grams per pizza)")]
        [Min(0f)] public float DefaultToppingGrams = 120f;
        [Min(0f)] public float MinToppingGrams = 60f;
        [Min(0f)] public float MaxToppingGrams = 200f;
        [Min(0.1f)] public float ToppingStepGrams = 5f;
        [Tooltip("Display tolerance for the 'last dose' readout (Margherita: 120 g +/- 10 %).")]
        [Min(0f)] public float ToppingToleranceGrams = 12f;

        [Header("Reservoirs")]
        [Tooltip("Sauce tank capacity in grams (10 kg = about 125 pizzas at 80 g).")]
        [Min(1f)] public float SauceTankCapacityGrams = 10000f;
        [Tooltip("Topping hopper capacity in grams (15 kg = about 125 pizzas at 120 g).")]
        [Min(1f)] public float ToppingHopperCapacityGrams = 15000f;
        [Tooltip("Fill level at scene start / after a fresh setup, in percent of capacity.")]
        [Range(0f, 100f)] public float InitialFillPercent = 100f;
        [Tooltip("Below this fill level (percent) the station shows a warning; at 0 it faults (SauceEmpty / ToppingEmpty).")]
        [Range(0f, 100f)] public float LowLevelWarningPercent = 20f;

        [Header("Dosing accuracy")]
        [Tooltip("Standard deviation of the dosed amount in percent of the setpoint. 0 = exact dosing.")]
        [Min(0f)] public float DosingDeviationPercent = 3f;

        private void OnValidate()
        {
            MaxSauceGrams = Mathf.Max(MaxSauceGrams, MinSauceGrams);
            MaxToppingGrams = Mathf.Max(MaxToppingGrams, MinToppingGrams);
            DefaultSauceGrams = Mathf.Clamp(DefaultSauceGrams, MinSauceGrams, MaxSauceGrams);
            DefaultToppingGrams = Mathf.Clamp(DefaultToppingGrams, MinToppingGrams, MaxToppingGrams);
        }
    }
}
