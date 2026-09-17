using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Root of the operator screen. Owns panel visibility and the UI focus
    /// handshake with the platform layer. Holds no production data itself –
    /// binders added in later days push values into the individual views.
    /// </summary>
    [DisallowMultipleComponent]
    public class HmiScreenController : MonoBehaviour
    {
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private GameObject _screenRoot;
        [SerializeField] private List<HmiPanelBase> _panels = new List<HmiPanelBase>();
        [SerializeField] private string _defaultPanelId = "overview";
        [SerializeField] private bool _openOnStart;

        public bool IsOpen { get; private set; }
        public string ActivePanelId { get; private set; }

        private void Awake()
        {
            ApplyOpenState(false);
        }

        private void Start()
        {
            if (_openOnStart)
            {
                Open(_defaultPanelId);
            }
        }

        public void Open() => Open(_defaultPanelId);

        public void Open(string panelId)
        {
            ShowPanel(panelId);

            if (IsOpen)
            {
                return;
            }

            IsOpen = true;
            ApplyOpenState(true);
            _uiFocusChannel?.PushFocus();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            IsOpen = false;
            ApplyOpenState(false);
            _uiFocusChannel?.PopFocus();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void ShowPanel(string panelId)
        {
            ActivePanelId = panelId;

            for (int i = 0; i < _panels.Count; i++)
            {
                HmiPanelBase panel = _panels[i];
                if (panel != null)
                {
                    panel.SetVisible(panel.PanelId == panelId);
                }
            }
        }

        public T GetPanel<T>() where T : HmiPanelBase
        {
            for (int i = 0; i < _panels.Count; i++)
            {
                if (_panels[i] is T typed)
                {
                    return typed;
                }
            }

            return null;
        }

        private void ApplyOpenState(bool open)
        {
            if (_screenRoot != null)
            {
                _screenRoot.SetActive(open);
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                _uiFocusChannel?.PopFocus();
            }
        }
    }
}
