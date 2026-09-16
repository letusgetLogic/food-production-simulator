using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Convenience base class for MonoBehaviour-based interactables. Handles the
    /// IsInteractable/InteractableChanged bookkeeping and a default (empty) hover
    /// implementation, so concrete interactables (navigation arrows on Day 2, HMI
    /// buttons on Day 3) only need to override <see cref="OnInteract"/> and optionally
    /// the hover hooks. Not mandatory to use IInteractable through this base class,
    /// but recommended to avoid repeating the same boilerplate everywhere.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class InteractableBase : MonoBehaviour, IInteractable
    {
        [SerializeField] private bool _isInteractable = true;

        public bool IsInteractable
        {
            get => _isInteractable;
            protected set
            {
                if (_isInteractable == value)
                {
                    return;
                }

                _isInteractable = value;
                InteractableChanged?.Invoke(_isInteractable);
            }
        }

        public event Action<bool> InteractableChanged;

        public void Interact(IInteractor interactor)
        {
            if (!IsInteractable)
            {
                return;
            }

            OnInteract(interactor);
        }

        /// <summary>Override to implement the actual interaction behaviour.</summary>
        public abstract void OnInteract(IInteractor interactor);

        /// <summary>Override to show a highlight; default does nothing.</summary>
        public virtual void OnHoverEnter(IInteractor interactor) { }

        /// <summary>Override to clear a highlight; default does nothing.</summary>
        public virtual void OnHoverExit(IInteractor interactor) { }
    }
}
