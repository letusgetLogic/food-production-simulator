using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Frozen look: from <see cref="ProductState.FrozenPizza"/> on (until packaged) every visible part of the pizza
    /// (base, sauce, mozzarella, any future ingredient) gets a paler, cooler base color - blended towards a frost
    /// tint via MaterialPropertyBlock, the shared materials stay untouched.
    /// </summary>
    [RequireComponent(typeof(ProductToken))]
    public class PizzaFrostVisual : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Cool, pale tint the colors are blended towards.")]
        [SerializeField] private Color _frostColor = new Color(0.86f, 0.93f, 1f, 1f);

        [Tooltip("0 = original colors, 1 = frost color only.")]
        [Range(0f, 1f)] [SerializeField] private float _strength = 0.4f;

        [SerializeField] private ProductState _fromState = ProductState.FrozenPizza;
        [SerializeField] private ProductState _untilState = ProductState.PackagedPizza;

        private ProductToken _token;
        private Renderer[] _renderers;
        private MaterialPropertyBlock _block;
        private bool _applied;

        private void Awake()
        {
            _token = GetComponent<ProductToken>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            ProductInstance product = _token != null ? _token.Product : null;
            bool frozen = product != null && product.CurrentState >= _fromState && product.CurrentState < _untilState;
            if (frozen != _applied)
            {
                Apply(frozen);
            }
        }

        private void Apply(bool frozen)
        {
            foreach (Renderer target in _renderers)
            {
                if (target == null || target.sharedMaterial == null)
                {
                    continue;
                }

                if (!frozen)
                {
                    target.SetPropertyBlock(null);
                    continue;
                }

                Material material = target.sharedMaterial;
                Color original = material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                Color pale = Color.Lerp(original, _frostColor, _strength);
                pale.a = original.a;
                target.GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, pale);
                target.SetPropertyBlock(_block);
            }
            _applied = frozen;
        }
    }
}
