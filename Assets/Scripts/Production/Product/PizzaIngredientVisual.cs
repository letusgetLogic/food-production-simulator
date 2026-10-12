using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Shows one visual per dosed ingredient on the pizza prefab (sauce layer, mozzarella pieces, ...): an entry
    /// is visible while the product carries a dose of that ingredient (<see cref="ProductInstance.HasDose"/>)
    /// and is not yet packaged. Works next to <see cref="PizzaStateVisual"/> (base, box) - ingredient visuals
    /// must not be listed there. New ingredient = new child visual + one entry here.
    /// </summary>
    [RequireComponent(typeof(ProductToken))]
    public class PizzaIngredientVisual : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("SO_DosingIngredient.Id, e.g. \"sauce\", \"mozzarella\".")]
            public string IngredientId;
            public GameObject Visual;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        [Tooltip("From this state on the ingredients are hidden (e.g. inside the carton).")]
        [SerializeField] private ProductState _hideFromState = ProductState.PackagedPizza;

        private ProductToken _token;

        private void Awake()
        {
            _token = GetComponent<ProductToken>();
            Apply();
        }

        private void Update() => Apply();

        private void Apply()
        {
            ProductInstance product = _token != null ? _token.Product : null;
            foreach (Entry entry in _entries)
            {
                if (entry == null || entry.Visual == null)
                {
                    continue;
                }

                bool visible = product != null && product.CurrentState < _hideFromState && product.HasDose(entry.IngredientId);
                if (entry.Visual.activeSelf != visible)
                {
                    entry.Visual.SetActive(visible);
                }
            }
        }
    }
}
