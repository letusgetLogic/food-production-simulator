using UnityEngine;

namespace Game.Production
    {
        /// <summary>
        /// Content-driven configuration for <see cref="FormerMachine"/>.
        /// Values are read/set by the operator via the HMI, not bound to a RecipeDefinition in code.
        /// </summary>
        [CreateAssetMenu(fileName = "FormerConfig", menuName = "Production/Machines/Former Config")]
        public class SO_FormerConfig : ScriptableObject
        {
            [Header("Timing")]
            public float StartupDurationSeconds = 1f;
            public float ShutdownDurationSeconds = 1f;

            [Header("Default Forming Parameters")]
            [Tooltip("Default forming duration in seconds; overridable at runtime via SetFormingDuration().")]
            public float DefaultFormingDurationSeconds = 4f;

            // No dimension sensor exists yet (only PresenceSensor/WeightSensor are confirmed, Tag 2).
            // Target diameter/thickness are content values for HMI display / future QualitySystem use
            // (Woche 2), not evaluated by this machine yet.
            [Header("Reference Values (display only, not yet checked)")]
            public float DefaultTargetDiameterCm = 28f;
            public float DefaultTargetThicknessMm = 3f;
        }
    }
