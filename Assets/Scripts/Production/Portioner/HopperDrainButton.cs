using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Operator button at the Portionierer: drops the whole hopper content out without creating
    /// products (holding colliders off, then on again).
    /// </summary>
    public class HopperDrainButton : InteractableBase
    {
        [SerializeField] private PortionerMachine _machine;

        protected override void OnInteract(IInteractor interactor)
        {
            if (_machine != null)
            {
                _machine.TryDrainHopper();
            }
        }
    }
}
