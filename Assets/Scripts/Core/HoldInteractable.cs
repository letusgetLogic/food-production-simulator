using System;
using UnityEngine;

namespace Game.Core
{
    [RequireComponent(typeof(Transform))]
    public class HoldInteractable : InteractableBase
    {
        [SerializeField] private SO_HoldChannel _holdChannel;
        [SerializeField] private SO_HoldPoint _holdPoint;
        [SerializeField] private SO_DropRequestChannel _dropRequestChannel;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private float _followLerpSpeed = 15f;
        [SerializeField] private float _lookYaw = 90f;

        [Tooltip("The item drops when it gets stuck this far (m) from the hold point, e.g. behind a wall.")]
        [SerializeField] private float _maxHoldDistance = 1.5f;

        private Transform _tf;
        private Collider[] _ownColliders = System.Array.Empty<Collider>();
        private readonly Collider[] _overlaps = new Collider[16];
        private bool _isHeld;
        public event Action<HoldInteractable> OnReleased; 

        private void Start()
        {
            _tf = GetComponent<Transform>();
        }

        protected override void OnInteract(IInteractor interactor)
        {
            if (_isHeld)
            {
                return; // being interacted with while held shouldn't happen via aim,
                        // since holding it keeps it out of interact range/targeting
            }

            if (_holdChannel == null)
            {
                Debug.LogWarning($"{name}: no hold channel available, cannot pick up.", this);
                return;
            }

            if (_holdPoint == null)
            {
                Debug.LogWarning($"{name}: no hold point available, cannot pick up.", this);
                return;
            }

            if (!_holdChannel.TryClaim(this))
            {
                return; // another item is already held, ignore this pickup attempt
            }

            Pickup();
        }

        private void Pickup()
        {
            _isHeld = true;

            transform.SetParent(null);
            // Visuals may switch colliders on and off (pizza states) - read them at pickup.
            _ownColliders = System.Array.FindAll(GetComponentsInChildren<Collider>(), c => !c.isTrigger);

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
            }

            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested += Release;
            }
        }

        private void Release()
        {
            if (!_isHeld)
            {
                return;
            }

            _isHeld = false;

            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested -= Release;
            }

            _holdChannel?.ReleaseClaim();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }

            OnReleased?.Invoke(this);
        }

        private void FixedUpdate()
        {
            if (!_isHeld)
            {
                return;
            }

            Transform holdPoint = _holdPoint != null ? _holdPoint.HoldPoint : null;
            if (holdPoint == null)
            {
                return;
            }
            var lookDirection = Quaternion.Euler(0, _lookYaw, 0) * holdPoint.rotation;

            // Frame-rate independent exponential smoothing instead of a fixed Lerp factor,
            // so _followLerpSpeed behaves consistently regardless of the physics timestep.
            float t = 1f - Mathf.Exp(-_followLerpSpeed * Time.fixedDeltaTime);
            Vector3 nextPosition = Vector3.Lerp(_tf.position, holdPoint.position, t);
            Quaternion nextRotation = Quaternion.Slerp(_tf.rotation, lookDirection, t);

            if (_rigidbody != null)
            {
                // Held items stay kinematic (belts, buffer and line end read that as "taken"), and a kinematic
                // body passes through static geometry - so walls, hopper and machines are checked here.
                nextPosition = ConstrainToWorld(_tf.position, nextPosition, holdPoint.root);
                if ((holdPoint.position - nextPosition).sqrMagnitude > _maxHoldDistance * _maxHoldDistance)
                {
                    Release(); // stuck behind something while the player walked on: it drops
                    return;
                }

                _rigidbody.MovePosition(nextPosition);
                _rigidbody.MoveRotation(nextRotation);
            }
            else
            {
                _tf.SetPositionAndRotation(nextPosition, nextRotation);
            }
        }

        // ---- Collision while held ----

        private const float SkinWidth = 0.01f;

        /// <summary>
        /// Limits the move from <paramref name="from"/> to <paramref name="to"/> so the held item does not enter
        /// static or kinematic colliders: sweep (stop at the first hit, slide along it), then push out of any
        /// remaining overlap. Dynamic bodies are ignored - the kinematic item pushes them (dough in the pot).
        /// The carrier (player) is ignored.
        /// </summary>
        private Vector3 ConstrainToWorld(Vector3 from, Vector3 to, Transform carrier)
        {
            Vector3 move = to - from;
            float distance = move.magnitude;
            if (distance > 1e-5f)
            {
                Vector3 direction = move / distance;
                if (TryFindBlockingHit(direction, distance + SkinWidth, carrier, out RaycastHit hit))
                {
                    float allowed = Mathf.Max(0f, hit.distance - SkinWidth);
                    Vector3 slide = Vector3.ProjectOnPlane(move - direction * allowed, hit.normal);
                    to = from + direction * allowed + slide;
                }
            }

            return to + Depenetration(to - from, carrier);
        }

        private bool TryFindBlockingHit(Vector3 direction, float distance, Transform carrier, out RaycastHit closest)
        {
            closest = default;
            bool found = false;
            foreach (RaycastHit hit in _rigidbody.SweepTestAll(direction, distance, QueryTriggerInteraction.Ignore))
            {
                if (IsBlocking(hit.collider, carrier) && (!found || hit.distance < closest.distance))
                {
                    closest = hit;
                    found = true;
                }
            }
            return found;
        }

        private Vector3 Depenetration(Vector3 offset, Transform carrier)
        {
            Vector3 correction = Vector3.zero;
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled)
                {
                    continue;
                }

                Bounds bounds = own.bounds;
                bounds.center += offset + correction;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, _overlaps, Quaternion.identity,
                    ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlaps[i];
                    if (!IsBlocking(other, carrier))
                    {
                        continue;
                    }

                    if (Physics.ComputePenetration(own, own.transform.position + offset + correction, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 pushDirection, out float depth))
                    {
                        correction += pushDirection * depth;
                    }
                }
            }
            return correction;
        }

        private bool IsBlocking(Collider other, Transform carrier)
        {
            if (other == null || other.isTrigger || other.attachedRigidbody == _rigidbody)
            {
                return false;
            }

            if (carrier != null && other.transform.IsChildOf(carrier))
            {
                return false;
            }

            Rigidbody body = other.attachedRigidbody;
            return body == null || body.isKinematic;
        }

        private void OnDisable()
        {
            // Safety net: avoid a leaked subscription and a permanently "claimed"
            // hold point if the item is destroyed/disabled while held.
            if (!_isHeld)
            {
                return;
            }

            if (_dropRequestChannel != null)
            {
                _dropRequestChannel.DropRequested -= Release;
            }

            _holdChannel?.ReleaseClaim();
        }
    }

}