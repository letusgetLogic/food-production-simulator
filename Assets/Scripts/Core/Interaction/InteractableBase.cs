using System;
using UnityEngine;
using UnityEngine.Localization;

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

        [Tooltip("Localized text shown at the crosshair while aiming at this object. " +
                 "Empty = the default text of the component (key prompt.<text in snake_case>).")]
        [SerializeField] private LocalizedString _interactKey;
        /// <summary>
        /// Crosshair text: the localized inspector text if set, otherwise the component's default text
        /// </summary>
        public virtual string InteractionText
        {
            get
            {
                string fallback = Localizer("Interact");
                return _interactKey == null || _interactKey.IsEmpty ? fallback : LocText.Now(_interactKey, fallback);
            }
        }

        /// <summary>
        /// Translates an English prompt text. Set by LocText at start-up;
        /// identity until then. Keeps Game.Core free of a table dependency.
        /// </summary>
        public static System.Func<string, string> Localizer = text => text;

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
        protected abstract void OnInteract(IInteractor interactor);

        /// <summary>Override to show a highlight; default does nothing.</summary>
        public virtual void OnHoverEnter(IInteractor interactor) { }

        /// <summary>Override to clear a highlight; default does nothing.</summary>
        public virtual void OnHoverExit(IInteractor interactor) { }
    }
}
