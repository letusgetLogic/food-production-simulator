using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>Handle (Deichsel) of the <see cref="PalletJack"/>: E takes the jack.</summary>
    public class PalletJackHandle : InteractableBase
    {
        [SerializeField] private PalletJack _jack;

        protected override void OnInteract(IInteractor interactor)
        {
            PalletJack jack = _jack != null ? _jack : GetComponentInParent<PalletJack>();
            if (jack != null)
            {
                jack.Grab();
            }
        }
    }
}
