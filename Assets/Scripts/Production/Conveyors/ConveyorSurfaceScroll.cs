using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Purely visual, no logic: scrolls the stripe texture of a flat belt-surface quad along the travel
    /// direction, so a running belt looks like it moves. The quad lies just above the belt surface collider,
    /// its texture V axis points in travel direction (set up by
    /// <c>Tools / Food Production / Setup Moving Belt Visuals</c>).
    ///
    /// Reads only <see cref="ConveyorBelt.IsMoving"/> (or one slot zone of a buffer belt) and the current belt
    /// speed, so the stripes follow start/stop ramps, speed changes, hold-back and faults. The quad carries no
    /// collider - products rest on the belt's own surface collider underneath.
    ///
    /// Replaces the plank approach of <see cref="ConveyorPlankVisual"/>, which needs separate plank objects.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ConveyorSurfaceScroll : MonoBehaviour
    {
        [SerializeField] private ConveyorBelt _belt;

        [Tooltip("Buffer belt only: slot zone this quad belongs to (0 = rear). -1 = whole belt.")]
        [SerializeField] private int _segmentIndex = -1;

        [Tooltip("Distance between two stripes on the belt in metres (one texture repeat).")]
        [Min(0.02f)] [SerializeField] private float _stripeSpacingMeters = 0.25f;

        [Tooltip("Length of this quad along the travel direction in metres (sets the texture tiling).")]
        [Min(0.01f)] [SerializeField] private float _lengthMeters = 1f;

        private Material _material;
        private float _offset;

        public ConveyorBelt Belt => _belt;
        public int SegmentIndex => _segmentIndex;

        /// <summary>Called by the editor setup.</summary>
        public void Configure(ConveyorBelt belt, int segmentIndex, float lengthMeters, float stripeSpacingMeters)
        {
            _belt = belt;
            _segmentIndex = segmentIndex;
            _lengthMeters = Mathf.Max(0.01f, lengthMeters);
            _stripeSpacingMeters = Mathf.Max(0.02f, stripeSpacingMeters);
        }

        private void Awake()
        {
            // Own material instance per quad - every belt (and every buffer slot) scrolls independently.
            _material = GetComponent<Renderer>().material;
            _material.mainTextureScale = new Vector2(1f, _lengthMeters / _stripeSpacingMeters);

            // Random start offset, so neighbouring belts do not show identical stripe positions.
            _offset = Random.value;
            _material.mainTextureOffset = new Vector2(0f, _offset);
        }

        private void Update()
        {
            if (_belt == null)
            {
                return;
            }

            bool isMoving = _segmentIndex < 0 ? _belt.IsMoving : _belt.IsSegmentMoving(_segmentIndex);
            if (!isMoving)
            {
                return;
            }

            // Texture V runs in travel direction; decreasing the offset moves the pattern forward.
            _offset = Mathf.Repeat(_offset - _belt.CurrentSpeedMetersPerSecond * Time.deltaTime / _stripeSpacingMeters, 1f);
            _material.mainTextureOffset = new Vector2(0f, _offset);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
