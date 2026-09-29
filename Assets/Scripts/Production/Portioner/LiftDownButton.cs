using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>Operator button at the Portionierer: tilts the pot back and lowers the lift.</summary>
    public class LiftDownButton : InteractableBase
    {
        [SerializeField] private PortionerMachine _machine;

        protected override void OnInteract(IInteractor interactor)
        {
            if (_machine != null)
            {
                _machine.TryLowerLift();
            }
        }
    }
}
