using Game.Core;
using UnityEngine;

namespace Game.Platform
{
    public class HoldItem : MonoBehaviour 
    {
        [SerializeField] private SO_DropRequestChannel _dropRequestChannel;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Transform _holdPoint;
        [SerializeField] private SO_HoldPointChannel _holdPointChannel;

        private void Awake()
        {
            _holdPointChannel.SetHoldPoint(_holdPoint);
        }

        private void OnEnable()
        {
            _input.ReleasePerformed += OnDropPerformed;
        }

        private void OnDisable()
        {
            _input.ReleasePerformed -= OnDropPerformed;
        }

        private void OnDropPerformed()
        {
            _dropRequestChannel.RequestDrop();
        }
    }
}
