using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// End of the line (behind the last station): every product that reaches this trigger volume is handed
    /// over once. Raises <see cref="ProductArrived"/> - the QualitySystem judges it there - then offers it to
    /// the optional <see cref="IProductReceiver"/> (palletizer: good cartons go onto the pallet). Products the
    /// receiver does not take (scrap) are removed after a short delay so the belt end never piles up.
    /// While the receiver is not accepting (no pallet / pallet full) products stay on the belt and are
    /// handed over later.
    ///
    /// Like a sensor it does not evaluate anything itself. Products carried by the worker (kinematic)
    /// are ignored until they are dropped.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class LineEndSink : MonoBehaviour
    {
        [Tooltip("Seconds the product stays visible after arriving before it is removed.")]
        [Min(0f)] [SerializeField] private float _despawnDelaySeconds = 0.5f;

        [Tooltip("Optional: takes the products (palletizer). Empty = products are removed after the quality check.")]
        [SerializeField] private MonoBehaviour _receiver;

        private readonly HashSet<ProductToken> _handled = new HashSet<ProductToken>();

        private IProductReceiver Receiver => _receiver as IProductReceiver;

        /// <summary>Raised once per product that reaches the end of the line.</summary>
        public event Action<ProductInstance> ProductArrived;

        /// <summary>Number of products that left the line here since scene start.</summary>
        public int ArrivedCount { get; private set; }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) => TryHandle(other);

        // Products that arrived while the receiver was not accepting are handed over as soon as it is.
        private void OnTriggerStay(Collider other) => TryHandle(other);

        private void TryHandle(Collider other)
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

            IProductReceiver receiver = Receiver;
            if (receiver != null && !receiver.IsAccepting)
            {
                return; // wait (no pallet / pallet full) - the belt holds the product
            }

            _handled.Add(token);
            ArrivedCount++;
            ProductArrived?.Invoke(token.Product);

            if (receiver == null || !receiver.TryReceive(token))
            {
                Destroy(token.gameObject, _despawnDelaySeconds);
            }
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
