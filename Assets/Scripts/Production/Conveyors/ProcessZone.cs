using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Trigger volume of a tunnel station (oven chamber, cooling tunnel, freezer). Only tracks which
    /// products are inside and for how long - no evaluation, no state changes (that is the station's
    /// job, same separation as sensors vs. machines).
    ///
    /// The zone's local Z axis must point in travel direction; <see cref="LengthMeters"/> (box size Z
    /// in world units) is what the station uses to turn a dwell time into a belt speed.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ProcessZone : MonoBehaviour
    {
        private readonly Dictionary<ProductToken, int> _colliderCounts = new Dictionary<ProductToken, int>();
        private readonly Dictionary<ProductToken, float> _entryTimes = new Dictionary<ProductToken, float>();
        private readonly List<ProductToken> _scratch = new List<ProductToken>();

        private BoxCollider _volume;

        /// <summary>Raised when a product enters the zone.</summary>
        public event Action<ProductToken> ProductEntered;

        /// <summary>Raised when a product leaves the zone: (product, secondsInside).</summary>
        public event Action<ProductToken, float> ProductExited;

        public IReadOnlyCollection<ProductToken> Products => _entryTimes.Keys;
        public int ProductCount => _entryTimes.Count;

        /// <summary>Length of the zone along its local Z axis in world units.</summary>
        public float LengthMeters
        {
            get
            {
                BoxCollider volume = _volume != null ? _volume : GetComponent<BoxCollider>();
                return volume.size.z * Mathf.Abs(volume.transform.lossyScale.z);
            }
        }

        public float GetSecondsInside(ProductToken token) =>
            token != null && _entryTimes.TryGetValue(token, out float entryTime) ? Time.time - entryTime : 0f;

        private void Awake()
        {
            _volume = GetComponent<BoxCollider>();
            _volume.isTrigger = true;
        }

        private void Update()
        {
            // Products destroyed inside the zone (e.g. removed by the worker) never get an exit event.
            _scratch.Clear();
            foreach (ProductToken token in _entryTimes.Keys)
            {
                if (token == null)
                {
                    _scratch.Add(token);
                }
            }

            foreach (ProductToken token in _scratch)
            {
                _entryTimes.Remove(token);
                _colliderCounts.Remove(token);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            ProductToken token = ProductColliderUtility.FindToken(other);
            if (token == null)
            {
                return;
            }

            _colliderCounts.TryGetValue(token, out int count);
            _colliderCounts[token] = count + 1;

            if (count == 0)
            {
                _entryTimes[token] = Time.time;
                ProductEntered?.Invoke(token);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            ProductToken token = ProductColliderUtility.FindToken(other);
            if (token == null || !_colliderCounts.TryGetValue(token, out int count))
            {
                return;
            }

            if (count > 1)
            {
                _colliderCounts[token] = count - 1;
                return;
            }

            float secondsInside = GetSecondsInside(token);
            _colliderCounts.Remove(token);
            _entryTimes.Remove(token);
            ProductExited?.Invoke(token, secondsInside);
        }

        private void OnDrawGizmosSelected()
        {
            BoxCollider volume = _volume != null ? _volume : GetComponent<BoxCollider>();
            if (volume == null)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.4f, 0f);
            Gizmos.matrix = volume.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(volume.center, volume.size);
            Gizmos.DrawLine(volume.center, volume.center + Vector3.forward * volume.size.z * 0.5f);
        }
    }
}
