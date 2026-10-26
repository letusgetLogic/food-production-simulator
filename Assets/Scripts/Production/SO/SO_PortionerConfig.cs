using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content-driven configuration for <see cref="PortionerMachine"/>.
    /// Values are read/set by the operator via the HMI, not bound to a RecipeDefinition in code.
    /// </summary>
    [CreateAssetMenu(fileName = "PortionerConfig", menuName = "Production/Machines/Portioner Config")]
    public class SO_PortionerConfig : ScriptableObject
    {
        [Header("Timing")]
        public float StartupDurationSeconds = 1f;
        public float ShutdownDurationSeconds = 1f;

        [Header("Default Portioning Parameters")]
        [Tooltip("Default portioning duration in seconds; overridable at runtime via SetPortioningDuration().")]
        public float DefaultPortioningDurationSeconds = 5f;

        [Tooltip("Default target portion weight in grams; overridable at runtime via SetTargetWeight().")]
        public float DefaultTargetWeightGrams = 250f;

        [Tooltip("Default acceptable deviation from the target weight in grams.")]
        public float DefaultToleranceGrams = 15f;

        [Header("HMI Setpoint Limits (operator input at the machine terminal)")]
        [Min(1f)] public float MinTargetWeightGrams = 100f;
        [Min(1f)] public float MaxTargetWeightGrams = 500f;
        [Min(0.1f)] public float TargetWeightStepGrams = 5f;
        [Min(0f)] public float MaxToleranceGrams = 50f;
        [Min(0.1f)] public float ToleranceStepGrams = 1f;
        [Min(0.1f)] public float MinPortioningDurationSeconds = 1f;
        [Min(0.1f)] public float MaxPortioningDurationSeconds = 20f;
        [Min(0.1f)] public float PortioningDurationStepSeconds = 0.5f;
    }
}
