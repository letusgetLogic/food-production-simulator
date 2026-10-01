using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Purely visual, no logic: moves the belt surface planks along the travel
    /// direction via Transform.Translate and resets each plank to its start
    /// position once it has passed the belt end - the classic loop trick for an
    /// endless-looking belt without mesh/texture-scroll setup.
    ///
    /// Only reads <see cref="ConveyorBelt.IsMoving"/> (or one slot zone of a buffer belt) and the belt's current speed -
    /// has no effect whatsoever on products or conveyor logic. The planks must NOT
    /// carry colliders that products rest on (products are moved by ConveyorBelt via
    /// velocity; a static belt-surface collider underneath is enough).
    /// </summary>
    public class ConveyorPlankVisual : MonoBehaviour
    {
        [SerializeField] private ConveyorBelt _belt;
        [SerializeField] private SO_ConveyorConfig _config;
        [Tooltip("All plank transforms along the travel direction, as children of the belt.")]
        [SerializeField] private Transform[] _planks;
        [Tooltip("Transform whose forward axis defines the travel direction (usually the same as on ConveyorBelt).")]
        [SerializeField] private Transform _travelDirectionReference;
        [Tooltip("Buffer belt only: slot zone these planks belong to (0 = rear). -1 = whole belt. " +
                 "Give every slot of a buffer belt its own plank set, so waiting dough lies on standing planks.")]
        [SerializeField] private int _segmentIndex = -1;

        [Tooltip("Loop length of this plank set. 0 = belt length from the config (use the slot length for a buffer slot).")]
        [Min(0f)] [SerializeField] private float _loopLengthOverride;

        private float[] _plankStartLocalZ;

        private float LoopLength => _loopLengthOverride > 0f ? _loopLengthOverride : _config.BeltLengthMeters;

        private void Awake()
        {
            _plankStartLocalZ = new float[_planks.Length];
            for (int i = 0; i < _planks.Length; i++)
            {
                _plankStartLocalZ[i] = _planks[i].localPosition.z;
            }
        }

        private void Update()
        {
            bool isMoving = _segmentIndex < 0 ? _belt.IsMoving : _belt.IsSegmentMoving(_segmentIndex);
            if (!isMoving)
            {
                return;
            }

            float distanceThisFrame = _belt.CurrentSpeedMetersPerSecond * Time.deltaTime;
            Vector3 travelDirection = _travelDirectionReference.forward;

            for (int i = 0; i < _planks.Length; i++)
            {
                Transform plank = _planks[i];
                plank.Translate(travelDirection * distanceThisFrame, Space.World);

                float traveledLocalZ = plank.localPosition.z - _plankStartLocalZ[i];
                if (traveledLocalZ >= LoopLength)
                {
                    Vector3 local = plank.localPosition;
                    local.z = _plankStartLocalZ[i];
                    plank.localPosition = local;
                }
            }
        }
    }
}
