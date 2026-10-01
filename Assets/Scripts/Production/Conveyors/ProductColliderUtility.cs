using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Resolves the <see cref="ProductToken"/> behind a collider. Products may consist of several
    /// colliders (e.g. pizza body + ContactCollider child), so the lookup goes through the attached
    /// Rigidbody first and falls back to the parent hierarchy.
    /// </summary>
    public static class ProductColliderUtility
    {
        public static ProductToken FindToken(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }

            Rigidbody body = collider.attachedRigidbody;
            if (body != null && body.TryGetComponent(out ProductToken tokenOnBody))
            {
                return tokenOnBody;
            }

            return collider.GetComponentInParent<ProductToken>();
        }
    }
}
