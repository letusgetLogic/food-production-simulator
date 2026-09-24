using Game.Core;
using UnityEngine;

namespace Game.Production
{
    public class TiltUpwardButton : InteractableBase
    {
        [SerializeField] private MachineBase _machine;
        protected override void OnInteract(IInteractor interactor)
        {
            if (_machine is MixerMachine mixer)
            {
                mixer.TryResetDrum();
            }
        }
    }
}
