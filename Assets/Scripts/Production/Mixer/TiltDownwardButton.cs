using Game.Core;
using UnityEngine;

namespace Game.Production
{
    public class TiltDownwardButton : InteractableBase
    {
        [SerializeField] private MachineBase _machine;
        public override void OnInteract(IInteractor interactor)
        {
            if (_machine is MixerMachine mixer)
            {
                mixer.TryTiltDrum(out _);
            }
        }
    }
}
