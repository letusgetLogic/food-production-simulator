using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content data for a conveyor segment. No hardcoded values in code,
    /// everything here is configurable via inspector/content.
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ConveyorConfig", menuName = "FoodProductionSimulator/Conveyor Config")]
    public class SO_ConveyorConfig : ScriptableObject
    {
        [Header("Movement")]
        [SerializeField] private float _beltSpeedMetersPerSecond = 0.5f;
        [SerializeField] private float _beltLengthMeters = 2f;

        [Header("Jam Detection")]
        [Tooltip("How often we check whether products on the belt are still moving.")]
        [SerializeField] private float _jamCheckIntervalSeconds = 0.5f;
        [Tooltip("Position change below this value counts as 'not moving'.")]
        [SerializeField] private float _jamPositionThresholdMeters = 0.02f;
        [Tooltip("A product must be stuck for this long despite the belt running before IsJammed is triggered.")]
        [SerializeField] private float _jamTimeToTriggerSeconds = 3f;

        [Header("Visual (Planks)")]
        [SerializeField] private float _plankLengthMeters = 0.25f;

        public float BeltSpeedMetersPerSecond => _beltSpeedMetersPerSecond;
        public float BeltLengthMeters => _beltLengthMeters;
        public float JamCheckIntervalSeconds => _jamCheckIntervalSeconds;
        public float JamPositionThresholdMeters => _jamPositionThresholdMeters;
        public float JamTimeToTriggerSeconds => _jamTimeToTriggerSeconds;
        public float PlankLengthMeters => _plankLengthMeters;
    }
}