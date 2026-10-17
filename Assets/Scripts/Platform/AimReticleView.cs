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
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private AimInteractor _interactor;
        [SerializeField] private Graphic _reticle;
        [SerializeField] private TextMeshProUGUI _inputKeyLabel;
        [SerializeField] private TextMeshProUGUI _promptLabel;

        [Header("Appearance")]
        [SerializeField] private Color _idleColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Color _disabledColor = new Color(1f, 1f, 1f, 0.25f);
        [SerializeField] private Color _activeColor = new Color(1f, 0.78f, 0.1f, 1f);
        [SerializeField] private float _idleScale = 1f;
        [SerializeField] private float _activeScale = 1.35f;

       

        private IInteractable _boundTarget;

        private void OnEnable()
        {
            _uiFocusChannel.FocusChanged += SetReticle;
            _interactor.HoldChannel.HoldChanged += OnHoldChanged;
            _interactor.TargetChanged += OnTargetChanged;
            BindByChangingTarget(_interactor.CurrentTarget);
        }

        private void OnDisable()
        {
            _uiFocusChannel.FocusChanged -= SetReticle;
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

            if (_interactor.HoldChannel.HoldingObject)
                return;

            _inputKeyLabel.text = !canInteract ? string.Empty : $"({_interactor.Input.InputKeyInteract})";

            // Per-target action text ("("Open terminal"); the generic label only if a target has none.
            string text = hasTarget ? _boundTarget.InteractionText : null;
            _promptLabel.text = !canInteract ? string.Empty
                : string.IsNullOrEmpty(text) ? _interactor.Input.PromptInteract : $"{text}";
        }

        private void OnHoldChanged(bool isHeld)
        {
            string customPrompt = _interactor.HoldChannel.HoldingObject is IHoldingCustomHint prompt ? prompt.HintText : null;
            _promptLabel.text = !isHeld ? string.Empty : string.IsNullOrEmpty(customPrompt) ? _interactor.Input.PromptRelease : customPrompt;
            _inputKeyLabel.text = !isHeld ? string.Empty : string.IsNullOrEmpty(customPrompt) ? $"({_interactor.Input.InputKeyRelease})" : string.Empty;
        }

        private void LateUpdate()
        {
            // A custom hint can change while the item is held (pallet jack: riding, forks raised).
            if (_interactor.HoldChannel.HoldingObject is IHoldingCustomHint prompt && _promptLabel.text != prompt.HintText)
            {
                OnHoldChanged(true);
            }
        }

        private void SetReticle(bool isFocusingUI)
        {
            _reticle.enabled = !isFocusingUI;
        }
    }
}