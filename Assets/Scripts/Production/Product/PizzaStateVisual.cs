using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Shows the visual that belongs to the product's current <see cref="ProductState"/> on the
    /// pizza prefab, and switches state when its ContactCollider touches a
    /// <see cref="ProductStateTrigger"/> (e.g. the piston top of the press).
    ///
    /// Source of truth is <see cref="ProductInstance.CurrentState"/> on the ProductToken. If a
    /// machine changes the state directly, the visual follows automatically (checked every frame).
    /// While no ProductInstance is assigned yet (prefab placed by hand), a local state is used.
    ///
    /// Visual lookup: the entry with the highest state that is not above the current state wins.
    /// So CooledPizza / FrozenPizza / PackagedPizza keep showing BakedPizza until they get their own entry.
    ///
    /// Colliders are separated from the visuals: the visuals carry no trigger colliders. One child
    /// "ContactCollider" (convex trigger, shape of the formed pizza) is always active and receives the
    /// contacts for every state switch. The Rigidbody sits on this root, so OnTriggerEnter arrives
    /// here even while the press holds the pizza kinematic and the piston top is moved by animation only.
    /// </summary>
    [RequireComponent(typeof(ProductToken))]
    public class PizzaStateVisual : MonoBehaviour
    {
        [Serializable]
        public class StateVisual
        {
            public ProductState State;

            [Tooltip("GameObjects shown in this state. Everything listed in other entries is hidden.")]
            public GameObject[] Visuals;
        }

        [SerializeField] private List<StateVisual> _stateVisuals = new List<StateVisual>();

        [Tooltip("Used while no ProductInstance is assigned (e.g. pizza placed manually in the scene).")]
        [SerializeField] private ProductState _initialState = ProductState.PortionedDough;

        private ProductToken _token;
        private ProductState _localState;
        private ProductState? _shownState;
        private readonly HashSet<GameObject> _activeBuffer = new HashSet<GameObject>();

        /// <summary>Raised after a switch: (previous, next).</summary>
        public event Action<ProductState, ProductState> StateChanged;

        public ProductState CurrentState => _token != null && _token.Product != null
            ? _token.Product.CurrentState
            : _localState;

        private void Awake()
        {
            _token = GetComponent<ProductToken>();
            _localState = _initialState;
            ApplyVisual(CurrentState);
        }

        private void Update()
        {
            // Follows state changes made elsewhere (machines, save/load).
            if (_shownState != CurrentState)
            {
                ApplyVisual(CurrentState);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            ProductStateTrigger trigger = other.GetComponentInParent<ProductStateTrigger>();
            if (trigger != null)
            {
                TrySwitch(trigger.FromState, trigger.ToState);
            }
        }

        /// <summary>Switches to <paramref name="to"/> only if the product is currently in <paramref name="from"/>.</summary>
        public bool TrySwitch(ProductState from, ProductState to)
        {
            if (CurrentState != from)
            {
                return false;
            }

            SetState(to);
            return true;
        }

        /// <summary>Sets the state unconditionally (writes it to the ProductInstance, if any).</summary>
        public void SetState(ProductState next)
        {
            ProductState previous = CurrentState;

            _localState = next;
            if (_token != null && _token.Product != null)
            {
                _token.Product.CurrentState = next;
            }

            ApplyVisual(next);

            if (previous != next)
            {
                StateChanged?.Invoke(previous, next);
            }
        }

        private void ApplyVisual(ProductState state)
        {
            StateVisual match = null;
            foreach (StateVisual entry in _stateVisuals)
            {
                if (entry.State <= state && (match == null || entry.State > match.State))
                {
                    match = entry;
                }
            }

            _activeBuffer.Clear();
            if (match != null && match.Visuals != null)
            {
                foreach (GameObject visual in match.Visuals)
                {
                    if (visual != null)
                    {
                        _activeBuffer.Add(visual);
                    }
                }
            }

            foreach (StateVisual entry in _stateVisuals)
            {
                if (entry.Visuals == null)
                {
                    continue;
                }

                foreach (GameObject visual in entry.Visuals)
                {
                    if (visual != null && !_activeBuffer.Contains(visual))
                    {
                        visual.SetActive(false);
                    }
                }
            }

            foreach (GameObject visual in _activeBuffer)
            {
                visual.SetActive(true);
            }

            _shownState = state;
        }
    }
}
