using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Step button for setpoints: fires once on press and repeats while held (faster the longer it
    /// is held), so the operator can run through a range without clicking every step.
    /// Uses unscaled time so it also works while the game is paused behind the HMI.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class HmiRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float _initialDelaySeconds = 0.4f;
        [SerializeField] private float _repeatIntervalSeconds = 0.12f;
        [SerializeField] private float _fastRepeatIntervalSeconds = 0.04f;
        [SerializeField] private float _accelerateAfterSeconds = 1.5f;

        private Button _button;
        private bool _isHeld;
        private float _heldTime;
        private float _nextRepeatTime;

        /// <summary>Raised on press and on every repeat.</summary>
        public event Action Stepped;

        private void Awake() => _button = GetComponent<Button>();

        private void OnDisable() => _isHeld = false;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsUsable())
            {
                return;
            }

            _isHeld = true;
            _heldTime = 0f;
            _nextRepeatTime = _initialDelaySeconds;
            Stepped?.Invoke();
        }

        public void OnPointerUp(PointerEventData eventData) => _isHeld = false;

        public void OnPointerExit(PointerEventData eventData) => _isHeld = false;

        private void Update()
        {
            if (!_isHeld)
            {
                return;
            }

            if (!IsUsable())
            {
                _isHeld = false;
                return;
            }

            _heldTime += Time.unscaledDeltaTime;
            if (_heldTime < _nextRepeatTime)
            {
                return;
            }

            float interval = _heldTime > _accelerateAfterSeconds ? _fastRepeatIntervalSeconds : _repeatIntervalSeconds;
            _nextRepeatTime = _heldTime + interval;
            Stepped?.Invoke();
        }

        private bool IsUsable() => _button != null && _button.IsInteractable();
    }
}
