using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Attached to the physical product prefab that rides on the conveyor belt. Carries the
    /// domain-level <see cref="ProductInstance"/> so that a station's sensors can identify/scan
    /// which product has physically arrived (e.g. via <c>GetComponent&lt;ProductToken&gt;()</c>),
    /// without the conveyor itself ever needing to know about ProductInstance - the conveyor only
    /// ever handles the plain GameObject as a physical load.
    /// </summary>
    public class ProductToken : MonoBehaviour
    {
        /// <summary>The domain-level product data carried by this physical object.</summary>
        public ProductInstance Product;
    }

}
