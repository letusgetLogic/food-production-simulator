using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Data-only configuration for a single conveyor segment.
    /// No hardcoded values in behaviour scripts - everything tunable lives here.
    /// </summary>
    [CreateAssetMenu(fileName = "ConveyorConfig", menuName = "Production/Conveyor Config")]
    public class SO_ConveyorConfig : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("Number of discrete transport slots on this conveyor segment.")]
        [Min(1)] public int SlotCount = 6;

        [Tooltip("World-space distance between two adjacent slots.")]
        [Min(0.01f)] public float SlotSpacing = 0.5f;

        [Header("Motion")]
        [Tooltip("Linear speed at which tokens move between slots, in units/second.")]
        [Min(0.01f)] public float TransportSpeed = 1.0f;

        [Header("Fault Simulation (optional, for later fault system integration)")]
        [Tooltip("If true, this conveyor can be forced into a jammed state externally (debug/testing).")]
        public bool AllowManualJamToggle = true;
    }
}