using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Platform
{
    /// <summary>
    /// Minimal UGUI crosshair on its own screen-space overlay canvas. Stays out of
    /// the HMI canvas so the operator panel can be built and themed independently.
    /// Distinguishes three states: nothing aimed at, aimed at something currently
    /// not interactable (e.g. a disabled HMI button), and aimed at something
    /// interactable right now.
    /// </summary>
    [DisallowMultipleComponent]
    public class AimReticleView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AimInteractor _interactor;
        [SerializeField] private Graphic _reticle;
        [SerializeField] private TextMeshProUGUI _promptLabel;

        [Header("Appearance")]
        [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color _disabledColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private float _idleScale = 1f;
        [SerializeField] private float _activeScale = 1.35f;

        [Header("Text")]
        [SerializeField] private string _defaultPrompt = "(E) Interact";
        [SerializeField] private string _holdPrompt = "(Q) Release";

        private IInteractable _boundTarget;

        private void OnEnable()
        {
            _interactor.HoldChannel.HoldChanged += OnHoldChanged;
            _interactor.TargetChanged += OnTargetChanged;
            BindByChangingTarget(_interactor.CurrentTarget);
        }

        private void OnDisable()
        {
            _interactor.HoldChannel.HoldChanged -= OnHoldChanged;
            _interactor.TargetChanged -= OnTargetChanged;
            BindByChangingTarget(null);
        }

        /// <summary>
        /// Called in Update.
        /// </summary>
        /// <param name="previous"></param>
        /// <param name="current"></param>
        private void OnTargetChanged(IInteractable previous, IInteractable current) => BindByChangingTarget(current);

        private void BindByChangingTarget(IInteractable target)
        {
            if (_boundTarget != null)
            {
                _boundTarget.InteractableChanged -= OnTargetInteractableChanged;
            }

            _boundTarget = target;

            if (_boundTarget != null)
            {
                _boundTarget.InteractableChanged += OnTargetInteractableChanged;
            }

            Refresh();
        }

        private void OnTargetInteractableChanged(bool _) => Refresh();

        private void Refresh()
        {
            bool hasTarget = _boundTarget != null;
            bool canInteract = hasTarget && _boundTarget.IsInteractable;

            if (_reticle != null)
            {
                _reticle.color = canInteract ? _activeColor : hasTarget ? _disabledColor : _idleColor;
                _reticle.rectTransform.localScale =
                    Vector3.one * (canInteract ? _activeScale : _idleScale);
            }

            if (_promptLabel != null)
            {
                // IInteractable carries no display text of its own, so this is a
                // generic label. Swap in a per-target string here once the team
                // adds one (e.g. a small optional interface on top of IInteractable).
                _promptLabel.text = canInteract ? _defaultPrompt : string.Empty;
            }
        }

        private void OnHoldChanged(bool isHeld)
        {
             _promptLabel.text = isHeld ? _holdPrompt : string.Empty;
        }
    }
}