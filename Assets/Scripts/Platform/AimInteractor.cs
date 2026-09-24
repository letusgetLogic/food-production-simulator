using System;
using Game.Core;
using UnityEngine;

namespace Game.Platform
{
    /// <summary>
    /// Crosshair-based interactor: raycasts from the screen centre, drives
    /// OnHoverEnter/OnHoverExit on whatever IInteractable it targets, and
    /// triggers Interact() on request. Sits next to MouseInteractor as a second
    /// IInteractor implementation – nothing on the IInteractable side changes.
    /// </summary>
    [DisallowMultipleComponent]
    public class AimInteractor : MonoBehaviour, IInteractor
    {
        [Header("References")]
        [SerializeField] private Camera _aimCamera;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private SO_HoldPointChannel _holdChannel;
        public SO_HoldPointChannel HoldChannel => _holdChannel;


        [Header("Raycast")]
        [SerializeField] private float _maxDistance = 3f;
        [SerializeField] private LayerMask _interactableMask = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;

        private IInteractable _currentTarget;

        public IInteractable CurrentTarget => _currentTarget;

        /// <summary>
        /// Update only by change.
        /// </summary>
        public event Action<IInteractable, IInteractable> TargetChanged;

        private void Awake()
        {
            if (_aimCamera == null)
            {
                _aimCamera = GetComponentInChildren<Camera>();
            }

            if (_input == null)
            {
                _input = GetComponentInParent<PlayerInputReader>();
            }
        }

        private void OnEnable() => _input.InteractPerformed += OnInteractPerformed;

        private void OnDisable()
        {
            _input.InteractPerformed -= OnInteractPerformed;
            SetTarget(null);
        }

        private void Update()
        {
            if (_holdChannel.IsHeldItem)
            {
                SetTarget(null);
                return;
            }

            SetTarget(FindTargetUnderAim());
        }

        public bool TryInteract()
        {
            if (_currentTarget == null || !_currentTarget.IsInteractable)
            {
                return false;
            }

            _currentTarget.Interact(this);
            return true;
        }

        private IInteractable FindTargetUnderAim()
        {
            if (_aimCamera == null)
            {
                return null;
            }

            Ray ray = _aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _interactableMask, _triggerInteraction))
            {
                return null;
            }

            // GetComponentInParent so colliders can sit on child meshes of a button.
            return hit.collider.GetComponentInParent<IInteractable>();
        }

        /// <summary>
        /// Called in Update.
        /// </summary>
        /// <param name="next"></param>
        private void SetTarget(IInteractable next)
        {
            if (ReferenceEquals(next, _currentTarget))
            {
                return;
            }

            IInteractable previous = _currentTarget;

            // IInteractable's own doc: "Called when an interactor begins/stops
            // hovering/targeting this interactable" – the interactor drives this.
            previous?.OnHoverExit(this);
            next?.OnHoverEnter(this);

            _currentTarget = next;
            TargetChanged?.Invoke(previous, next);
        }

        private void OnInteractPerformed() => TryInteract();
    }
}