using Game.Core;
using UnityEngine;

namespace Game.Production
{
    public class Pot : InteractableBase
    {
        bool _isInteracting;
        public override void OnInteract(IInteractor interactor)
        {
            if (!_isInteracting)
                transform.parent.SetParent(Camera.main.transform);
            else
                transform.parent.SetParent(null);

            _isInteracting = !_isInteracting;
        }
    }
}