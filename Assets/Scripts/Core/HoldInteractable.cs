using UnityEngine;

namespace Game.Core
{
    [RequireComponent(typeof(Transform))]
    public class HoldInteractable : InteractableBase
    {
        [SerializeField] private SO_HoldPointChannel _holdPointChannel;
        [SerializeField] private SO_DropRequestChannel _dropRequestChannel;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private float _followLerpSpeed = 15f;
        [SerializeField] private float _lookYaw = 90f;

        private Transform _tf;
        private bool _isHeld;

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

            if (_holdPointChannel == null || _holdPointChannel.HoldPoint == null)
            {
                Debug.LogWarning($"{name}: no hold point available, cannot pick up.", this);
                return;
            }

            if (!_holdPointChannel.TryClaim())
            {
                return; // another item is already held, ignore this pickup attempt
            }

            Pickup();
        }

        private void Pickup()
        {
            _isHeld = true;

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

            _holdPointChannel?.ReleaseClaim();

            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = false;
            }
        }

        private void FixedUpdate()
        {
            if (!_isHeld)
            {
                return;
            }

            Transform holdPoint = _holdPointChannel != null ? _holdPointChannel.HoldPoint : null;
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
                _rigidbody.MovePosition(nextPosition);
                _rigidbody.MoveRotation(nextRotation);
            }
            else
            {
                _tf.SetPositionAndRotation(nextPosition, nextRotation);
            }
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

            _holdPointChannel?.ReleaseClaim();
        }
    }

}