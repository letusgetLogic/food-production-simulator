using Game.Core;
using Game.Production;
using System;
using System.Collections.Generic;
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
        [SerializeField] private SO_TerminalInteractChannel _interactChannel;
        [SerializeField] private MachineOverviewReporter _machineOverviewReporter;
        [SerializeField] private MachineDetailBinder _machineDetailBinder;
        [SerializeField] private GameObject _screenRoot;
        [SerializeField] private NavMachineButton _navFirstMachineButton;
        [SerializeField] private NavMachineButton _navSecondaryMachineButton;
        [SerializeField] private List<HmiPanelBase> _panels = new List<HmiPanelBase>();
        [SerializeField] private string _defaultPanelId = "overview";
        [SerializeField] private bool _openOnStart;

        private const string MachinePanelId = "machine";

        public bool IsOpen { get; private set; }
        public string TerminalInteractId { get; private set; }
        private HmiPanelBase _machinePanel;

        private void Awake()
        {
            ApplyOpenState(false);
        }

        private void Start()
        {
            _machinePanel = _panels.Find(x => x.PanelId == MachinePanelId);

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
            }
        }

        public void Open() => Open(_defaultPanelId);
       
        public void Open(string panelId, MachineBase machine = null)
        {
            TerminalInteractId = panelId;

            if (machine != null)
            {
                _navFirstMachineButton.Button.onClick.AddListener(() =>
                {
                    ShowPanel(MachinePanelId, machine);
                    _navSecondaryMachineButton.gameObject.SetActive(false);
                });
            }
        
            _navFirstMachineButton.SetLabel(machine != null ? machine.DisplayName : "Machine");
            _navFirstMachineButton.gameObject.SetActive(panelId == MachinePanelId);

            _navSecondaryMachineButton.gameObject.SetActive(false);

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
     
        /// <summary>Row click in the overview: shows the machine panel bound to that machine.</summary>
        public void ShowMachinePanel(string machineId)
        {
            MachineBase machine = _machineOverviewReporter != null ? _machineOverviewReporter.GetMachineById(machineId) : null;
   
            if (TerminalInteractId == MachinePanelId)
            {
                _navSecondaryMachineButton.SetLabel(machine.DisplayName);
                _navSecondaryMachineButton.gameObject.SetActive(true);
            }
            else
            {
                _navFirstMachineButton.SetLabel(machine.DisplayName);
                _navFirstMachineButton.gameObject.SetActive(true);
            }
            ShowPanel(MachinePanelId, machine);
        }

        /// <summary>
        /// Nav-rail buttons (UnityEvent with a string argument - needs exactly this one-parameter signature,
        /// a method with an optional second parameter is not found by the persistent call).
        /// </summary>
        public void ShowPanel(string panelId) => ShowPanel(panelId, null);

        public void ShowPanel(string panelId, MachineBase machine)
        {
            if (TerminalInteractId != MachinePanelId)
            {
                _navFirstMachineButton.gameObject.SetActive(false);
                _navSecondaryMachineButton.gameObject.SetActive(false);
            }
            else
            {
                if (panelId != MachinePanelId)
                {
                    _navSecondaryMachineButton.gameObject.SetActive(false);
                }
            }
            _machineDetailBinder.Bind(machine);

            for (int i = 0; i < _panels.Count; i++)
            {
                HmiPanelBase panel = _panels[i];
                if (panel != null)
                {
                    bool visible = panel.PanelId == panelId;
                    panel.gameObject.SetActive(true);
                    panel.SetVisible(visible);
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
