using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Operator button at the Dosierstation: refills the sauce tank or the topping hopper completely.
    /// Same pattern as HopperDrainButton at the Portionierer.
    /// </summary>
    public class DosingRefillButton : InteractableBase
    {
        [SerializeField] private DosingMachine _machine;
        [SerializeField] private DosingMedium _medium = DosingMedium.Sauce;

        protected override void OnInteract(IInteractor interactor)
        {
            if (_machine != null)
            {
                _machine.TryRefill(_medium);
            }
        }
    }
}
