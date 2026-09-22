using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.HMI
{
    /// <summary>
    /// Base for every panel inside the operator screen. Panels are dumb: they
    /// expose setters and know nothing about where their data comes from, so the
    /// later switch from plain C# events to ScriptableObject event channels only
    /// touches the binder layer, not the views.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class HmiPanelBase : MonoBehaviour
    {
        [SerializeField] private string _panelId = "panel";

        private CanvasGroup _canvasGroup;

        public string PanelId => _panelId;
        public bool IsVisible { get; private set; } = true;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            ApplyVisibility(IsVisible);
        }

        public void SetVisible(bool visible)
        {
            if (IsVisible == visible)
            {
                return;
            }

            IsVisible = visible;
            ApplyVisibility(visible);
            OnVisibilityChanged(visible);
        }

        protected virtual void OnVisibilityChanged(bool visible) { }

        private void ApplyVisibility(bool visible)
        {
            if (_canvasGroup == null)
            {
                return;
            }

            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

    }
}
