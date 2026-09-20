using Game.Production;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// One selectable row for the Tablet's machine picker. Unlike the read-only
    /// MachineStateRowView used in the Overview, this one carries a direct
    /// MachineBase reference and reacts to clicks - it is only ever used on the
    /// Tablet, never in the shared Overview list.
    /// </summary>
    public class MachineSelectRowView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _machineNameText;
        [SerializeField] private Button _selectButton;

        private MachineBase _machine;

        public event Action<MachineBase> Selected;

        private void Awake()
        {
            _selectButton?.onClick.AddListener(() => Selected?.Invoke(_machine));
        }

        public void Bind(MachineBase machine)
        {
            _machine = machine;

            if (_machineNameText != null)
            {
                _machineNameText.text = machine != null ? machine.Id : string.Empty;
            }
        }
    }

    /// <summary>
    /// Tablet-only machine picker: lists every machine in the scene, tapping one
    /// tells the co-located MachineDetailBinder to bind it. Does its own
    /// FindObjectsByType discovery (same approach as MachineOverviewReporter) -
    /// deliberately independent of the MachineOverviewChannel, since this list
    /// only needs machine identity, not live state.
    /// </summary>
    public class MachineSelectListView : MonoBehaviour
    {
        [SerializeField] private MachineDetailBinder _binder;
        [SerializeField] private MachineSelectRowView _rowTemplate;
        [SerializeField] private RectTransform _rowContainer;

        private void Awake()
        {
            if (_rowTemplate == null || _rowContainer == null)
            {
                Debug.LogError($"{nameof(MachineSelectListView)}: row template or container not assigned.", this);
                return;
            }

            _rowTemplate.gameObject.SetActive(false);

            MachineBase[] machines = FindObjectsByType<MachineBase>(FindObjectsSortMode.None);

            foreach (MachineBase machine in machines)
            {
                MachineSelectRowView row = Instantiate(_rowTemplate, _rowContainer);
                row.gameObject.SetActive(true);
                row.Bind(machine);
                row.Selected += OnRowSelected;
            }
        }

        private void OnRowSelected(MachineBase machine)
        {
            if (_binder != null)
            {
                _binder.Bind(machine);
            }
        }
    }
}