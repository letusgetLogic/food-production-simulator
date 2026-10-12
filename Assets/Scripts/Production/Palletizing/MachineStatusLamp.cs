using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Signal lamp (Signalsäule) on a machine: green = running without warning, amber = warning
    /// (e.g. waiting for a pallet), red = fault/maintenance, grey = stopped. Visual only (MaterialPropertyBlock).
    /// Prefab "StatusLamp": on a <see cref="MachineControlPanel"/> the machine and renderer are found automatically.
    /// </summary>
    public class MachineStatusLamp : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Empty = the machine of the control panel this lamp sits on.")]
        [SerializeField] private MachineBase _machine;
        [Tooltip("Empty = the renderer on this object.")]
        [SerializeField] private Renderer _lamp;
        [SerializeField] private Color _running = new Color(0.2f, 0.85f, 0.3f);
        [SerializeField] private Color _warning = new Color(1f, 0.7f, 0.1f);
        [SerializeField] private Color _fault = new Color(0.95f, 0.15f, 0.1f);
        [SerializeField] private Color _off = new Color(0.35f, 0.35f, 0.35f);
        [Min(0f)] [SerializeField] private float _emission = 1.5f;

        private MaterialPropertyBlock _block;
        private Color? _shown;

        private void Awake()
        {
            if (_machine == null)
            {
                MachineControlPanel panel = GetComponentInParent<MachineControlPanel>();
                _machine = panel != null ? panel.Machine : GetComponentInParent<MachineBase>();
            }
            if (_lamp == null)
            {
                _lamp = GetComponent<Renderer>();
            }
        }

        private void Update()
        {
            if (_machine == null || _lamp == null)
            {
                return;
            }

            Color color = _machine.CurrentState switch
            {
                MachineState.Fault => _fault,
                MachineState.Maintenance => _fault,
                MachineState.Running when _machine.HasWarning => _warning,
                MachineState.Running => _running,
                MachineState.Starting => _warning,
                _ => _off
            };

            if (_shown == color)
            {
                return;
            }

            _block ??= new MaterialPropertyBlock();
            _lamp.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(EmissionColorId, color == _off ? Color.black : color * _emission);
            _lamp.SetPropertyBlock(_block);
            _shown = color;
        }
    }
}
