using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using Game.Production;

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
        [SerializeField] private SO_HmiInteractChannel _interactChannel;
        [SerializeField] private MachineOverviewReporter _machineOverviewReporter;
        [SerializeField] private MachineDetailBinder _machineDetailBinder;
        [SerializeField] private GameObject _screenRoot;
        [SerializeField] private List<HmiPanelBase> _panels = new List<HmiPanelBase>();
        [SerializeField] private string _defaultPanelId = "overview";
        [SerializeField] private bool _openOnStart;

        public bool IsOpen { get; private set; }
        public string ActivePanelId { get; private set; }
        private HmiPanelBase _machinePanel;

        private void Awake()
        {
            ApplyOpenState(false);
        }

        private void Start()
        {
            _machinePanel = _panels.Find(x => x.PanelId == "machine");

            if (_openOnStart)
            {
                Open(_defaultPanelId);
            }
        }

        private void OnEnable()
        {
            if (_uiFocusChannel)
                _uiFocusChannel.CloseRequested += Close;

            if (_interactChannel)
            {
                _interactChannel.DetailRequested += Open;
                _interactChannel.DetailRequestedFromMachineRow += ShowMachinePanel;
            }
        }

        private void OnDisable()
        {
            if (IsOpen)
            {
                IsOpen = false;
                _uiFocusChannel?.PopFocus();
            }
            if (_uiFocusChannel)
                _uiFocusChannel.CloseRequested -= Close;

            if (_interactChannel)
            {
                _interactChannel.DetailRequested -= Open;
                _interactChannel.DetailRequestedFromMachineRow -= ShowMachinePanel;
            }
        }

        public void Open() => Open(_defaultPanelId);

        public void Open(string panelId, MachineBase machine = null)
        {
            ShowPanel(panelId, machine);

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

        public void ShowPanel(string panelId, MachineBase machine = null)
        {
            ActivePanelId = panelId;

            for (int i = 0; i < _panels.Count; i++)
            {
                HmiPanelBase panel = _panels[i];
                if (panel != null)
                {
                    panel.SetVisible(panel.PanelId == panelId);

                    // Hide navButton of machine
                    if (panelId != "machine")
                    {
                        if (_machinePanel)
                            _machinePanel.gameObject.SetActive(false);
                    }
                    else
                    {
                        if (_machinePanel)
                            _machinePanel.gameObject.SetActive(true);
                        if (_machineDetailBinder)
                            _machineDetailBinder.Bind(machine);

                    }
                }
            }
        }

        public void ShowMachinePanel(string machineId)
        {
            ActivePanelId = "machine";

            for (int i = 0; i < _panels.Count; i++)
            {
                HmiPanelBase panel = _panels[i];
                if (panel != null)
                {
                    panel.SetVisible(panel.PanelId == machineId);

                    if (_machinePanel)
                        _machinePanel.gameObject.SetActive(true);

                    MachineBase machine = _machineOverviewReporter.GetMachineById(machineId);
                    if (_machineDetailBinder)
                        _machineDetailBinder.Bind(machine);
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

    }
}
