using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// The store of empty pallets as ONE interaction: a single trigger box covers all <see cref="PalletStack"/>s
    /// below it. E takes the top pallet of the non-empty stack nearest to the player; carrying an empty pallet and
    /// pressing Q at the store puts it on the nearest stack with room (see <see cref="PalletStack.TryStackNear"/>).
    /// Crosshair: "Take pallet (37)".
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class PalletStorage : InteractableBase
    {
        [Tooltip("Stacks of this store. Empty = all PalletStacks below this object.")]
        [SerializeField] private List<PalletStack> _stacks = new List<PalletStack>();
        [SerializeField] private string _defaultLayer = "Default";
        [SerializeField] private string _interactionLayer = "Interactable";

        private float _refreshTimer;
        private int _count;

        protected override string DefaultInteractionText => "Take pallet";

        /// <summary>Empty pallets in the whole store.</summary>
        public int Count => _count;

        public override string InteractionText => $"{base.InteractionText} ({_count})";

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true; // physical contact stays with the pallets themselves
            if (_stacks.Count == 0)
            {
                _stacks.AddRange(GetComponentsInChildren<PalletStack>());
            }
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f)
            {
                return;
            }

            _refreshTimer = 0.25f;
            _count = 0;
            foreach (PalletStack stack in _stacks)
            {
                if (stack != null)
                {
                    _count += stack.Count;
                }
            }

            IsInteractable = _count > 0;
            gameObject.layer = LayerMask.NameToLayer(_count > 0 ? _interactionLayer : _defaultLayer);
        }

        /// <summary>E: the player takes the top pallet of the nearest non-empty stack (carried like any held item).</summary>
        protected override void OnInteract(IInteractor interactor)
        {
            PalletStack stack = NearestNonEmpty();
            Pallet top = stack != null ? stack.Top : null;
            if (top == null || !top.TryGetComponent(out HoldInteractable hold))
            {
                return;
            }
            top.PlaceToHoldPoint();
            hold.SetInteractable(true); // the top pallet is always free
            hold.Interact(interactor);
            _refreshTimer = 0f;
        }

        private PalletStack NearestNonEmpty()
        {
            Camera view = Camera.main;
            Vector3 from = view != null ? view.transform.position : transform.position;
            PalletStack best = null;
            float bestDistance = float.MaxValue;
            foreach (PalletStack stack in _stacks)
            {
                if (stack == null || stack.Count == 0)
                {
                    continue;
                }

                Vector3 offset = stack.transform.position - from;
                float distance = new Vector2(offset.x, offset.z).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = stack;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>Fits the trigger box around all stacks (footprint and full height). Also on adding the component.</summary>
        [ContextMenu("Fit Collider To Stacks")]
        private void Reset()
        {
            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = true;
            PalletStack[] stacks = GetComponentsInChildren<PalletStack>();
            if (stacks.Length == 0)
            {
                return;
            }

            var bounds = new Bounds(stacks[0].transform.position, Vector3.zero);
            foreach (PalletStack stack in stacks)
            {
                Vector3 p = stack.transform.position;
                bounds.Encapsulate(p + new Vector3(-0.78f, 0f, -0.78f));
                bounds.Encapsulate(p + new Vector3(0.78f, stack.MaxPallets * stack.PalletHeight, 0.78f));
            }

            collider.center = transform.InverseTransformPoint(bounds.center);
            Vector3 scale = transform.lossyScale;
            collider.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
        }
    }
}
