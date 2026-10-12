using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// One stack place of empty pallets in the pallet store. Not interactable itself: the player interacts with the
    /// whole store (<see cref="PalletStorage"/>), which takes from / puts onto the stacks.
    ///
    /// The stack does not own its pallets: members are the free empty pallets standing on its footprint, so a stack
    /// lifted away with the pallet jack simply leaves an empty stack place behind.
    /// </summary>
    public class PalletStack : MonoBehaviour
    {
        /// <summary>All active stacks.</summary>
        public static readonly List<PalletStack> All = new List<PalletStack>();

        [Tooltip("Max. number of pallets on this stack.")]
        [Min(1)] [SerializeField] private int _maxPallets = 20;
        [Tooltip("Height of one pallet (m).")]
        [Min(0.01f)] [SerializeField] private float _palletHeight = 0.1445f;
        [Tooltip("A pallet belongs to the stack while its centre is this close (horizontal, m) to the stack centre.")]
        [Min(0.05f)] [SerializeField] private float _footprintRadius = 0.4f;
        [Tooltip("Releasing a carried pallet (Q) this close (horizontal, m) to the stack puts it on top.")]
        [Min(0.5f)] [SerializeField] private float _dropRadius = 1.7f;

        private readonly List<Pallet> _members = new List<Pallet>();

        public int MaxPallets => _maxPallets;
        public float PalletHeight => _palletHeight;

        /// <summary>Pallets currently on the stack.</summary>
        public int Count
        {
            get
            {
                Refresh();
                return _members.Count;
            }
        }

        public bool IsFull => Count >= _maxPallets;

        /// <summary>The top pallet, or null.</summary>
        public Pallet Top
        {
            get
            {
                Refresh();
                return _members.Count > 0 ? _members[_members.Count - 1] : null;
            }
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Refresh()
        {
            _members.Clear();
            Vector3 center = transform.position;
            foreach (Pallet pallet in Pallet.All)
            {
                if (pallet.IsCarried || pallet.IsHeldByHand || pallet.IsOnStation || !pallet.IsEmpty)
                {
                    continue;
                }

                Vector3 offset = pallet.transform.position - center;
                if (new Vector2(offset.x, offset.z).magnitude <= _footprintRadius
                    && offset.y > -0.05f && offset.y < _maxPallets * _palletHeight + 0.1f)
                {
                    _members.Add(pallet);
                }
            }
            _members.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        }

        /// <summary>True if <paramref name="position"/> (pallet centre) lies on this stack's footprint.</summary>
        public bool Covers(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            return new Vector2(offset.x, offset.z).magnitude <= _footprintRadius
                   && offset.y > -0.05f && offset.y < _maxPallets * _palletHeight + 0.1f;
        }

        /// <summary>True if a pallet at <paramref name="position"/> stands on any stack of the store.</summary>
        public static bool IsOnAnyStack(Vector3 position)
        {
            foreach (PalletStack stack in All)
            {
                if (stack.Covers(position))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Puts an empty pallet on top of the stack (snapped, parked). False if the stack is full.</summary>
        public bool TryAdd(Pallet pallet)
        {
            if (pallet == null || !pallet.IsEmpty || Count >= _maxPallets)
            {
                return false;
            }

            Vector3 position = transform.position + Vector3.up * (_members.Count * _palletHeight);
            pallet.PlaceParked(position, transform.rotation);
            return true;
        }

        /// <summary>Called when the player releases a carried pallet: put it on the nearest stack within reach.</summary>
        public static bool TryStackNear(Pallet pallet)
        {
            PalletStack best = null;
            float bestDistance = float.MaxValue;
            foreach (PalletStack stack in All)
            {
                Vector3 offset = pallet.transform.position - stack.transform.position;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                if (distance <= stack._dropRadius && distance < bestDistance && !stack.IsFull)
                {
                    best = stack;
                    bestDistance = distance;
                }
            }

            return best != null && best.TryAdd(pallet);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * (_maxPallets * _palletHeight * 0.5f),
                new Vector3(_footprintRadius * 2f, _maxPallets * _palletHeight, _footprintRadius * 2f));
        }
    }
}
