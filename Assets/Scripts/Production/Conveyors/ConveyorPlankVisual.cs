using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Purely visual, no logic: moves the belt surface planks along the travel
    /// direction via Transform.Translate and resets each plank to its start
    /// position once it has passed the belt end - the classic loop trick for an
    /// endless-looking belt without mesh/texture-scroll setup.
    ///
    /// Only reads the state from ConveyorBelt to know whether to animate - has no
    /// effect whatsoever on products or conveyor logic.
    /// </summary>
    public class ConveyorPlankVisual : MonoBehaviour
    {
        [SerializeField] private ConveyorBelt _belt;
        [SerializeField] private SO_ConveyorConfig _config;
        [Tooltip("All plank transforms along the travel direction, as children of the belt.")]
        [SerializeField] private Transform[] _planks;
        [Tooltip("Transform whose forward axis defines the travel direction (usually the same as on ConveyorBelt).")]
        [SerializeField] private Transform _travelDirectionReference;

        private float[] _plankStartLocalZ;

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
            if (_belt.CurrentState != MachineState.Running)
            {
                return;
            }

            float distanceThisFrame = _config.BeltSpeedMetersPerSecond * Time.deltaTime;
            Vector3 travelDirection = _travelDirectionReference.forward;

            for (int i = 0; i < _planks.Length; i++)
            {
                Transform plank = _planks[i];
                plank.Translate(travelDirection * distanceThisFrame, Space.World);

                float traveledLocalZ = plank.localPosition.z - _plankStartLocalZ[i];
                if (traveledLocalZ >= _config.BeltLengthMeters)
                {
                    Vector3 local = plank.localPosition;
                    local.z = _plankStartLocalZ[i];
                    plank.localPosition = local;
                }
            }
        }
    }
}