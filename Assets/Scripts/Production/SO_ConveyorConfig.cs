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
        [Tooltip("Default speed at scene start. Can be changed at runtime via ConveyorBelt.SetSpeed (line controller / tunnel station).")]
        [SerializeField] private float _beltSpeedMetersPerSecond = 0.5f;
        [SerializeField] private float _minBeltSpeedMetersPerSecond = 0.05f;
        [SerializeField] private float _maxBeltSpeedMetersPerSecond = 2f;
        [SerializeField] private float _beltLengthMeters = 2f;

        [Header("Timing")]
        [Tooltip("Ready -> Starting -> Running delay (motor ramp-up).")]
        [SerializeField] private float _startupDurationSeconds = 0.3f;
        [Tooltip("Running -> Stopping -> Stopped delay (motor ramp-down). Products stop immediately.")]
        [SerializeField] private float _shutdownDurationSeconds = 0.3f;

        [Header("Jam Detection")]
        [Tooltip("How often we check whether products on the belt are still moving.")]
        [SerializeField] private float _jamCheckIntervalSeconds = 0.5f;
        [Tooltip("Position change below this value counts as 'not moving'.")]
        [SerializeField] private float _jamPositionThresholdMeters = 0.02f;
        [Tooltip("A product must be stuck for this long despite the belt running before a Jam fault is triggered. " +
                 "Products held on purpose by an AccumulationZone are never counted.")]
        [SerializeField] private float _jamTimeToTriggerSeconds = 3f;

        [Header("Side Guides")]
        [Tooltip("Pull products back to the belt centre line while they move (like side guide rails). " +
                 "Keeps dough in one lane so the press can take it centred.")]
        [SerializeField] private bool _centerProducts = true;
        [Tooltip("How fast the lateral offset is corrected (1/s). 3 = about a third of the offset per 0.1 s.")]
        [Min(0f)] [SerializeField] private float _centeringRate = 3f;

        [Header("Visual (Planks)")]
        [SerializeField] private float _plankLengthMeters = 0.25f;

        public float BeltSpeedMetersPerSecond => _beltSpeedMetersPerSecond;
        public float MinBeltSpeedMetersPerSecond => _minBeltSpeedMetersPerSecond;
        public float MaxBeltSpeedMetersPerSecond => _maxBeltSpeedMetersPerSecond;
        public float BeltLengthMeters => _beltLengthMeters;
        public float StartupDurationSeconds => _startupDurationSeconds;
        public float ShutdownDurationSeconds => _shutdownDurationSeconds;
        public float JamCheckIntervalSeconds => _jamCheckIntervalSeconds;
        public float JamPositionThresholdMeters => _jamPositionThresholdMeters;
        public float JamTimeToTriggerSeconds => _jamTimeToTriggerSeconds;
        public float PlankLengthMeters => _plankLengthMeters;
        public bool CenterProducts => _centerProducts;
        public float CenteringRate => _centeringRate;
    }
}
