using Game.Core;
using UnityEngine;

namespace Game.Platform
{
    public class HoldItem : MonoBehaviour 
    {
        [SerializeField] private SO_DropRequestChannel _dropRequestChannel;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _holdItemPoint;
        [SerializeField] private SO_HoldPoint _holdPointChannel;
        [Tooltip("Relays the Use input (R) to the held tool, e.g. pallet jack forks.")]
        [SerializeField] private SO_UseRequestChannel _useRequestChannel;

        private void Awake()
        {
            _holdPointChannel.SetHoldPoint(_holdItemPoint);
        }

        private void OnEnable()
        {
            _input.ReleasePerformed += OnDropPerformed;
            _input.UsePerformed += OnUsePerformed;
            _input.ModePerformed += OnModePerformed;
        }

        private void OnDisable()
        {
            _input.ReleasePerformed -= OnDropPerformed;
            _input.UsePerformed -= OnUsePerformed;
            _input.ModePerformed -= OnModePerformed;
        }

        private void OnUsePerformed()
        {
            if (_useRequestChannel != null)
            {
                _useRequestChannel.RequestUse();
            }
        }

        private void OnModePerformed()
        {
            if (_useRequestChannel != null)
            {
                _useRequestChannel.RequestModeToggle();
            }
        }

        private void OnDropPerformed()
        {
            _dropRequestChannel.RequestDrop();
        }
    }
}
