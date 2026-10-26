using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content-driven configuration for <see cref="DosingMachine"/> (station-wide values). Ingredient values
    /// (setpoint range, reservoir size) live in each <see cref="SO_DosingIngredient"/>; setpoints are set by
    /// the operator at the machine terminal, not bound to a RecipeDefinition in code.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_DosingConfig", menuName = "Production/Machines/Dosing Config")]
    public class SO_DosingConfig : ScriptableObject
    {
        [Header("Timing")]
        [Min(0f)] public float StartupDurationSeconds = 1f;
        [Min(0f)] public float ShutdownDurationSeconds = 1f;

        [Header("Reservoirs")]
        [Tooltip("Fill level at scene start, in percent of each reservoir's capacity.")]
        [Range(0f, 100f)] public float InitialFillPercent = 100f;
        [Tooltip("Below this fill level (percent) the station shows a warning; not enough for the next pizza -> Fault IngredientEmpty:<id>.")]
        [Range(0f, 100f)] public float LowLevelWarningPercent = 20f;

        [Header("Dosing accuracy")]
        [Tooltip("Standard deviation of the dosed amount in percent of the setpoint. 0 = exact dosing.")]
        [Min(0f)] public float DosingDeviationPercent = 3f;
    }
}
