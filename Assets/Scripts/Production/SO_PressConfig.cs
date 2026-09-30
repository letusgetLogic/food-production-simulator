using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content-driven configuration for <see cref="PressMachine"/>.
    /// Values are read/set by the operator via the HMI, not bound to a RecipeDefinition in code.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_PressConfig", menuName = "Production/Machines/Press Config")]
    public class SO_PressConfig : ScriptableObject
    {
        [Header("Timing")]
        public float StartupDurationSeconds = 1f;
        public float ShutdownDurationSeconds = 1f;

        [Header("Press Speed (piston animation)")]
        [Tooltip("Playback speed of the press clip. 1 = original clip speed (the 'toggle' clip is 0.5 s per stroke). " +
                 "The whole forming cycle takes clipLength / speed, so this value also sets the cycle time.")]
        [Min(0.01f)] public float DefaultPressSpeed = 0.25f;
        [Min(0.01f)] public float MinPressSpeed = 0.05f;
        [Min(0.01f)] public float MaxPressSpeed = 2f;

        [Tooltip("Normalized clip time (0..1) at which the piston touches the dough.")]
        [Range(0f, 1f)] public float ContactStartNormalized = 0.2f;
        [Tooltip("Normalized clip time (0..1) at which the piston is fully down - dough is completely pressed.")]
        [Range(0f, 1f)] public float ContactEndNormalized = 0.45f;

        [Header("Forming Parameters (operator, from the recipe terminal)")]
        public float DefaultTargetDiameterCm = 28f;
        public float DefaultTargetThicknessMm = 3f;
        public float MinDiameterCm = 20f;
        public float MaxDiameterCm = 34f;
        public float MinThicknessMm = 2f;
        public float MaxThicknessMm = 8f;

        [Header("Visual Deformation")]
        [Tooltip("Scale multiplier applied to the dough at the default diameter/thickness. " +
                 "X/Z = width, Y = height. Other operator values scale this proportionally.")]
        public Vector3 FormedScaleMultiplier = new Vector3(1.3f, 0.4f, 1.3f);

        [Header("Detection")]
        [Tooltip("Layers the products live on (DoughSphere is on layer 6).")]
        public LayerMask ProductLayer = ~0;
        [Tooltip("Half extents of the detection box around the press point.")]
        public Vector3 PressZoneHalfExtents = new Vector3(0.25f, 0.25f, 0.25f);
    }
}
