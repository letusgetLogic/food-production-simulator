using System;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Platform
{
    /// <summary>
    /// Desktop mouse/pointer implementation of <see cref="IInteractor"/>. Uses a physics
    /// raycast from the main camera through the current pointer position (read via the
    /// new Input System) to find the closest <see cref="IInteractable"/> under the cursor.
    ///
    /// This is intentionally the only platform-specific interactor today. From Week 5, an
    /// XrInteractor can sit alongside this one without any change to IInteractable/IInteractor,
    /// since both only ever talk to the platform-agnostic Game.Core interfaces.
    ///
    /// Requires the "Input System Package (New)" project package (com.unity.inputsystem) and
    /// Project Settings → Player → Active Input Handling set to "Input System Package (New)"
    /// or "Both".
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseInteractor : MonoBehaviour, IInteractor
    {
        [Header("Raycast Setup")]
        [SerializeField] private Camera _raycastCamera;
        [SerializeField] private LayerMask _interactableLayerMask = ~0;
        [SerializeField] private float _maxRayDistance = 100f;

        [Header("Behaviour")]
        [Tooltip("If true, re-evaluates the hovered target every frame. Disable and call " +
                 "RefreshTarget() manually if you want tighter control over when raycasts happen.")]
        [SerializeField] private bool _updateEveryFrame = true;

        private IInteractable _currentTarget;

        public IInteractable CurrentTarget => _currentTarget;

        public event Action<IInteractable, IInteractable> TargetChanged;

        private void Awake()
        {
            if (_raycastCamera == null)
            {
                _raycastCamera = Camera.main;
            }
        }

        private void Update()
        {
            // No mouse present (e.g. touch-only device) - nothing to do this frame.
            if (Mouse.current == null)
            {
                return;
            }

            if (_updateEveryFrame)
            {
                RefreshTarget();
            }

            // Standard desktop trigger: left mouse button click.
            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                TryInteract();
            }
        }

        /// <summary>
        /// Re-runs the raycast from the current pointer position and updates
        /// <see cref="CurrentTarget"/>, raising <see cref="TargetChanged"/> if it changed.
        /// Exposed publicly so callers can drive this manually instead of relying on Update().
        /// </summary>
        public void RefreshTarget()
        {
            IInteractable newTarget = null;

            if (_raycastCamera != null && Mouse.current != null)
            {
                Vector2 pointerPosition = Mouse.current.position.ReadValue();
                Ray ray = _raycastCamera.ScreenPointToRay(pointerPosition);
                if (Physics.Raycast(ray, out RaycastHit hit, _maxRayDistance, _interactableLayerMask))
                {
                    hit.collider.TryGetComponent(out IInteractable hitInteractable);
                    newTarget = hitInteractable;
                }
            }

            SetTarget(newTarget);
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

        private void SetTarget(IInteractable newTarget)
        {
            if (ReferenceEquals(newTarget, _currentTarget))
            {
                return;
            }

            IInteractable previous = _currentTarget;
            previous?.OnHoverExit(this);

            _currentTarget = newTarget;
            _currentTarget?.OnHoverEnter(this);

            TargetChanged?.Invoke(previous, _currentTarget);
        }
    }
}
