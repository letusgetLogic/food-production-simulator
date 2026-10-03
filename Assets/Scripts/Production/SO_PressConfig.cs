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

        [Header("Forming Parameters (operator, from the recipe terminal)")]
        public float DefaultTargetDiameterCm = 28f;
        public float DefaultTargetThicknessMm = 3f;
        public float MinDiameterCm = 20f;
        public float MaxDiameterCm = 34f;
        public float MinThicknessMm = 2f;
        public float MaxThicknessMm = 8f;

        [Header("HMI Setpoint Steps (operator input at the machine terminal)")]
        [Min(0.01f)] public float CycleTimeStepSeconds = 0.1f;
        [Min(0.01f)] public float DiameterStepCm = 0.5f;
        [Min(0.01f)] public float ThicknessStepMm = 0.5f;

        [Header("Detection")]
        [Tooltip("Layers the products live on (DoughSphere is on layer 6).")]
        public LayerMask ProductLayer = ~0;
        [Tooltip("Half extents of the detection box around the press point.")]
        public Vector3 PressZoneHalfExtents = new Vector3(0.25f, 0.25f, 0.25f);
        [Tooltip("The dough is taken as soon as its centre is this close (horizontally) to the press point - " +
                 "i.e. when the belt has brought it to the centre under the piston.")]
        [Min(0.005f)] public float CenterToleranceMeters = 0.03f;
    }
}
