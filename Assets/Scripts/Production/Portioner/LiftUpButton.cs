using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>Operator button at the Portionierer: raises the lift and tips the pot.</summary>
    public class LiftUpButton : InteractableBase
    {
        [SerializeField] private PortionerMachine _machine;

        protected override void OnInteract(IInteractor interactor)
        {
            if (_machine != null)
            {
                _machine.TryRaiseLift();
            }
        }
    }
}
