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
        public PlayerInputReader Input => _input;
        [SerializeField] private SO_HoldChannel _holdChannel;
        public SO_HoldChannel HoldChannel => _holdChannel;


        [Header("Raycast")]
        [SerializeField] private float _maxDistance = 3f;
        [SerializeField] private LayerMask _interactableMask = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Collide;
        [Tooltip("Solid colliders on these layers block the aim (machine housings, tunnel walls) - nothing behind " +
                 "them can be targeted. Triggers without an interactable never block.")]
        [SerializeField] private LayerMask _occluderMask = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

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
            if (_holdChannel.HoldingObject != null)
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

            int count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _interactableMask | _occluderMask, _triggerInteraction);
            Array.Sort(_hits, 0, count, HitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider.transform.IsChildOf(transform.root))
                {
                    continue; // own body: the eye sits at the top of the player's capsule
                }

                // GetComponentInParent so colliders can sit on child meshes of a button.
                IInteractable interactable = collider.GetComponentInParent<IInteractable>();
                bool onInteractableLayer = (_interactableMask.value & (1 << collider.gameObject.layer)) != 0;
                if (interactable != null && onInteractableLayer)
                {
                    return interactable;
                }

                // A solid wall/housing in front of everything else ends the search; triggers (belt drives,
                // sensors, zones) are looked through.
                if (!collider.isTrigger)
                {
                    return null;
                }
            }

            return null;
        }

        private sealed class HitDistanceComparer : System.Collections.Generic.IComparer<RaycastHit>
        {
            public static readonly HitDistanceComparer Instance = new HitDistanceComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
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