using System;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// One dispenser of the <see cref="DosingMachine"/>: ingredient, process zone under the round head, reservoir.
    /// Building block of the modular dosing station - to add an ingredient, duplicate a unit (or drop the
    /// DosingUnit prefab) under the station and assign the ingredient; to replace one, assign another
    /// <see cref="SO_DosingIngredient"/>. The station doses in hierarchy order (first child unit = first
    /// dispenser on the belt), inactive units are skipped.
    ///
    /// The unit only holds data (setpoint, reservoir level, last dose); dosing logic, faults and HMI
    /// values live in the DosingMachine.
    /// </summary>
    public class DosingUnit : MonoBehaviour
    {
        [SerializeField] private SO_DosingIngredient _ingredient;

        [Tooltip("Trigger volume under the dispenser head - a product entering it is dosed.")]
        [SerializeField] private ProcessZone _zone;

        [Tooltip("Renderers that take the ingredient's DispenserMaterial (head, reservoir, refill button).")]
        [SerializeField] private Renderer[] _ingredientColoredRenderers = Array.Empty<Renderer>();

        private float _setpointGrams;
        private float _levelGrams;
        private float _lastDoseGrams = -1f;

        /// <summary>Raised when a product enters the zone under the head.</summary>
        public event Action<DosingUnit, ProductToken> ProductEntered;

        public SO_DosingIngredient Ingredient => _ingredient;
        public string IngredientId => _ingredient != null ? _ingredient.Id : name;
        public string DisplayName => _ingredient != null ? _ingredient.DisplayName : name;
        public string DisplayNameFallback => _ingredient != null ? _ingredient.DisplayNameFallback : name;
        public ProcessZone Zone => _zone;

        public float SetpointGrams => _setpointGrams;
        public float LevelGrams => _levelGrams;
        public float CapacityGrams => _ingredient != null ? _ingredient.ReservoirCapacityGrams : 1f;
        public float Level01 => _levelGrams / CapacityGrams;
        public float LastDoseGrams => _lastDoseGrams;

        /// <summary>Not enough left for the next pizza.</summary>
        public bool IsInsufficient => _levelGrams < _setpointGrams;

        public bool IsLow(float warningPercent) => Level01 * 100f < warningPercent;

        private void OnEnable()
        {
            if (_zone != null)
            {
                _zone.ProductEntered += HandleProductEntered;
            }
        }

        private void OnDisable()
        {
            if (_zone != null)
            {
                _zone.ProductEntered -= HandleProductEntered;
            }
        }

        private void OnValidate() => ApplyMaterial();

        private void HandleProductEntered(ProductToken token) => ProductEntered?.Invoke(this, token);

        /// <summary>Default setpoint and initial reservoir level (called by the station on Awake).</summary>
        public void Initialize(float initialFillPercent)
        {
            if (_ingredient == null)
            {
                Debug.LogError($"{name}: DosingUnit without ingredient.", this);
                return;
            }

            _setpointGrams = _ingredient.DefaultGrams;
            _levelGrams = _ingredient.ReservoirCapacityGrams * Mathf.Clamp01(initialFillPercent / 100f);
            ApplyMaterial();
        }

        public void SetSetpoint(float grams)
        {
            _setpointGrams = _ingredient != null ? Mathf.Clamp(grams, _ingredient.MinGrams, _ingredient.MaxGrams) : grams;
        }

        /// <summary>Takes a dose from the reservoir and remembers it as last dose.</summary>
        public void Take(float grams)
        {
            _levelGrams = Mathf.Max(0f, _levelGrams - grams);
            _lastDoseGrams = grams;
        }

        public void Refill() => _levelGrams = CapacityGrams;

        /// <summary>Save/Load.</summary>
        public void Restore(float levelGrams, float lastDoseGrams)
        {
            _levelGrams = Mathf.Clamp(levelGrams, 0f, CapacityGrams);
            _lastDoseGrams = lastDoseGrams;
        }

        private void ApplyMaterial()
        {
            if (_ingredient == null || _ingredient.DispenserMaterial == null || _ingredientColoredRenderers == null)
            {
                return;
            }

            foreach (Renderer target in _ingredientColoredRenderers)
            {
                if (target != null && target.sharedMaterial != _ingredient.DispenserMaterial)
                {
                    target.sharedMaterial = _ingredient.DispenserMaterial;
                }
            }
        }
    }
}
