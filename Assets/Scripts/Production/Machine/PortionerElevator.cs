using System.Collections;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Docking/elevator mechanism at the Portionierer. Workers place a (dumb, collider +
    /// IInteractable only) dough container in the dock slot; once the PresenceSensor confirms
    /// it's docked, the elevator raises it to tipping height and dumps its dough sphere into the
    /// DoughHopper. No IMachine here — this is mechanical support for PortionerMachine, not a
    /// production station in its own right, and it is not itself operated via HMI/Hotspot.
    /// </summary>
    public class PortionerElevator : MonoBehaviour
    {
        [SerializeField] private PresenceSensor _dockSensor;
        [SerializeField] private Hopper _hopper;
        [SerializeField] private Transform _liftedContainerPivot;
        [SerializeField] private float _liftDurationSeconds = 2f;
        [SerializeField] private float _liftHeight = 1.5f;

        private Coroutine _liftRoutine;
        private GameObject _dockedContainer;

        private void Update()
        {
            if (_liftRoutine != null)
            {
                return;
            }

            if (_dockSensor.CurrentValue && _dockedContainer != null)
            {
                _liftRoutine = StartCoroutine(LiftAndTipRoutine());
            }
        }

        // The PresenceSensor at the dock only reports a raw presence value (Tag 2 convention:
        // sensors report raw values, the owning logic does the interpretation). Resolving which
        // GameObject is actually docked is done here via the elevator's own trigger zone at the
        // same physical dock slot, since the container carries no ProductToken/domain data for a
        // sensor to resolve.
        private void OnTriggerEnter(Collider other)
        {
            if (_dockedContainer != null)
            {
                return;
            }

            // TODO: filter to container-only colliders once a marker/tag/component confirms
            // "this is a dough container" rather than any collider entering the dock zone.
            _dockedContainer = other.gameObject;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject == _dockedContainer)
            {
                _dockedContainer = null;
            }
        }

        private IEnumerator LiftAndTipRoutine()
        {
            float elapsed = 0f;
            Vector3 start = _liftedContainerPivot.localPosition;
            Vector3 target = start + Vector3.up * _liftHeight;

            while (elapsed < _liftDurationSeconds)
            {
                elapsed += Time.deltaTime;
                _liftedContainerPivot.localPosition = Vector3.Lerp(start, target, elapsed / _liftDurationSeconds);
                yield return null;
            }

            TipIntoHopper();
            _liftRoutine = null;
        }

        private void TipIntoHopper()
        {
            if (_dockedContainer == null)
            {
                return;
            }

            // The dough sphere lands inside the container as a child object and carries its
            // ProductInstance via ProductToken (confirmed Dev B component, same pattern sensors
            // use to resolve ProductInstance from a physical GameObject).
            ProductToken token = _dockedContainer.GetComponentInChildren<ProductToken>();
            if (token != null && token.Product != null)
            {
                _hopper.AddDough(token.Product.MeasuredWeightGrams ?? 0f);
                Destroy(token.gameObject);
            }

            _dockedContainer = null;
        }
    }
}