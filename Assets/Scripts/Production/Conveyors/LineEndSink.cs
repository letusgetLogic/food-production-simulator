using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// End of the line (behind the last station): every product that reaches this trigger volume leaves
    /// the simulation. Raises <see cref="ProductArrived"/> once per product - the QualitySystem judges it
    /// there - and removes the physical object after a short delay so the belt end never piles up.
    ///
    /// Like a sensor it does not evaluate anything itself. Products carried by the worker (kinematic)
    /// are ignored until they are dropped.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LineEndSink : MonoBehaviour
    {
        [Tooltip("Seconds the product stays visible after arriving before it is removed.")]
        [Min(0f)] [SerializeField] private float _despawnDelaySeconds = 0.5f;

        private readonly HashSet<ProductToken> _handled = new HashSet<ProductToken>();

        /// <summary>Raised once per product that reaches the end of the line.</summary>
        public event Action<ProductInstance> ProductArrived;

        /// <summary>Number of products that left the line here since scene start.</summary>
        public int ArrivedCount { get; private set; }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            ProductToken token = ProductColliderUtility.FindToken(other);
            if (token == null || token.Product == null || _handled.Contains(token))
            {
                return;
            }

            if (token.TryGetComponent(out Rigidbody body) && body.isKinematic)
            {
                return; // held by the worker or a machine
            }

            _handled.Add(token);
            ArrivedCount++;
            ProductArrived?.Invoke(token.Product);
            Destroy(token.gameObject, _despawnDelaySeconds);
        }

        private void Update()
        {
            if (_handled.Count > 0)
            {
                _handled.RemoveWhere(token => token == null);
            }
        }

        private void OnDrawGizmos()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 0.8f, 0.45f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
