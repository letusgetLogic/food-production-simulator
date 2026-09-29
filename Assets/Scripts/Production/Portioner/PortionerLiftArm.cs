using System;
using System.Collections;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Lift arm of the Portionierer. Pure mechanics, operated via two buttons:
    ///   Raise: rest position -> (rest.x, _tipHeight, rest.z), then rotate local Z to _tipAngleZ
    ///   Lower: rotate back to the rest rotation, then move down to the rest position
    ///
    /// The arm knows nothing about pots or products - whatever is parented to it (e.g. the pot
    /// snapped by PortionerMachine) simply rides along.
    ///
    /// No IMachine: mechanical sub-assembly of PortionerMachine, same as the Hopper.
    /// </summary>
    public class PortionerLiftArm : MonoBehaviour
    {
        public enum LiftPosition
        {
            Down,
            Raising,
            Up,
            Lowering
        }

        [Tooltip("The Arm transform to animate. Defaults to this transform.")]
        [SerializeField] private Transform _arm;

        [Header("Rest Pose (local)")]
        [SerializeField] private Vector3 _restLocalPosition = new Vector3(0.1f, 0.1f, 0f);
        [SerializeField] private Vector3 _restLocalEuler = Vector3.zero;

        [Header("Tip Pose (local)")]
        [SerializeField] private float _tipHeight = 3.2f;
        [SerializeField] private float _tipAngleZ = 110f;

        [Header("Timing (seconds)")]
        [SerializeField] private float _moveDurationSeconds = 3f;
        [SerializeField] private float _tiltDurationSeconds = 1.5f;

        private Coroutine _moveRoutine;

        /// <summary>Raised whenever <see cref="Position"/> changes (Down/Raising/Up/Lowering).</summary>
        public event Action<LiftPosition> PositionChanged;

        public LiftPosition Position { get; private set; } = LiftPosition.Down;

        public bool IsDown => Position == LiftPosition.Down;

        public bool IsUp => Position == LiftPosition.Up;

        public bool IsMoving => Position == LiftPosition.Raising || Position == LiftPosition.Lowering;

        private Transform Arm => _arm != null ? _arm : transform;

        private Vector3 TipLocalPosition => new Vector3(_restLocalPosition.x, _tipHeight, _restLocalPosition.z);

        private Quaternion RestLocalRotation => Quaternion.Euler(_restLocalEuler);

        private Quaternion TipLocalRotation => Quaternion.Euler(_restLocalEuler.x, _restLocalEuler.y, _restLocalEuler.z + _tipAngleZ);

        private void Awake()
        {
            Arm.SetLocalPositionAndRotation(_restLocalPosition, RestLocalRotation);
        }

        /// <summary>Moves up and tips. Only possible from <see cref="LiftPosition.Down"/>.</summary>
        public bool TryRaise()
        {
            if (!IsDown)
            {
                return false;
            }

            _moveRoutine = StartCoroutine(RaiseRoutine());
            return true;
        }

        /// <summary>Tilts back and moves down. Only possible from <see cref="LiftPosition.Up"/>.</summary>
        public bool TryLower()
        {
            if (!IsUp)
            {
                return false;
            }

            _moveRoutine = StartCoroutine(LowerRoutine());
            return true;
        }

        /// <summary>Stops any movement and snaps back to the rest pose (fault/maintenance reset).</summary>
        public void ResetToRest()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            Arm.SetLocalPositionAndRotation(_restLocalPosition, RestLocalRotation);
            SetPosition(LiftPosition.Down);
        }

        private IEnumerator RaiseRoutine()
        {
            SetPosition(LiftPosition.Raising);
            yield return MoveRoutine(_restLocalPosition, TipLocalPosition, RestLocalRotation, RestLocalRotation, _moveDurationSeconds);
            yield return MoveRoutine(TipLocalPosition, TipLocalPosition, RestLocalRotation, TipLocalRotation, _tiltDurationSeconds);
            _moveRoutine = null;
            SetPosition(LiftPosition.Up);
        }

        private IEnumerator LowerRoutine()
        {
            SetPosition(LiftPosition.Lowering);
            yield return MoveRoutine(TipLocalPosition, TipLocalPosition, TipLocalRotation, RestLocalRotation, _tiltDurationSeconds);
            yield return MoveRoutine(TipLocalPosition, _restLocalPosition, RestLocalRotation, RestLocalRotation, _moveDurationSeconds);
            _moveRoutine = null;
            SetPosition(LiftPosition.Down);
        }

        private IEnumerator MoveRoutine(Vector3 fromPos, Vector3 toPos, Quaternion fromRot, Quaternion toRot, float durationSeconds)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / durationSeconds));
                Arm.SetLocalPositionAndRotation(Vector3.Lerp(fromPos, toPos, t), Quaternion.Slerp(fromRot, toRot, t));
                yield return null;
            }

            Arm.SetLocalPositionAndRotation(toPos, toRot);
        }

        private void SetPosition(LiftPosition position)
        {
            if (Position == position)
            {
                return;
            }

            Position = position;
            PositionChanged?.Invoke(position);
        }
    }
}
