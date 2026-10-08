using System.Collections.Generic;
using Game.Production;
using UnityEngine;
using UnityEngine.Events;

namespace Game.HMI
{
    // Line overview: throughput figures, one state row per machine, active
    // faults. Machine rows are driven by MachineOverviewChannel - this panel
    // subscribes itself, so any number of OverviewPanel instances (terminal,
    // tablet, ...) can exist without anything needing to reference them.
    public class OverviewPanel : HmiPanelBase
    {
        [SerializeField] private HmiScreenController _screenController;

        [Header("Line figures")]
        [SerializeField] private StatusTileView _throughputTile;
        [SerializeField] private StatusTileView _producedUnitsTile;
        [SerializeField] private StatusTileView _scrapUnitsTile;
        [SerializeField] private StatusTileView _activeFaultsTile;

        [Header("Machines")]
        [SerializeField] private SO_MachineOverviewChannel _channel;
        [SerializeField] private MachineStateRowView _rowTemplate;
        [SerializeField] private Transform _rowContainer;

        [Header("Alarms")]
        [SerializeField] private AlarmListView _alarmList;

        private readonly Dictionary<string, MachineStateRowView> _rowsById = new Dictionary<string, MachineStateRowView>();

        private void OnEnable()
        {
            foreach (KeyValuePair<string, MachineState> entry in _channel.CurrentStates)
            {
                string displayName = _channel.DisplayNames.TryGetValue(entry.Key, out var name) ? name : entry.Key;
                MachineStateRowView row = GetOrCreateRow(entry.Key, displayName);
                row.SetWarning(_channel.CurrentWarnings.TryGetValue(entry.Key, out string reason), reason);
                ApplyState(entry.Key, row, entry.Value);
                row.SetButton();
            }

            _channel.MachineStateChanged += HandleMachineStateChanged;
            _channel.MachineWarningChanged += HandleMachineWarningChanged;
        }

        private void OnDisable()
        {
            _channel.MachineStateChanged -= HandleMachineStateChanged;
            _channel.MachineWarningChanged -= HandleMachineWarningChanged;
        }

        /// <summary>Labels of the four figure tiles (localized by the caller).</summary>
        public void SetFigureLabels(string throughput, string produced, string scrap, string activeFaults)
        {
            _throughputTile?.SetLabel(throughput);
            _producedUnitsTile?.SetLabel(produced);
            _scrapUnitsTile?.SetLabel(scrap);
            _activeFaultsTile?.SetLabel(activeFaults);
        }

        public void SetThroughput(float unitsPerMinute, HmiValueSeverity severity) =>
            _throughputTile?.SetValue(unitsPerMinute, severity);

        public void SetProducedUnits(int units) =>
            _producedUnitsTile?.SetValue(units, HmiValueSeverity.Normal);

        public void SetScrapUnits(int units, HmiValueSeverity severity) =>
            _scrapUnitsTile?.SetValue(units, severity);

        public void SetAlarms(IReadOnlyList<HmiAlarmEntry> alarms)
        {
            _alarmList?.SetAlarms(alarms);

            int count = alarms?.Count ?? 0;
            _activeFaultsTile?.SetValue(
                count, count > 0 ? HmiValueSeverity.Alarm : HmiValueSeverity.Normal);
        }

        private void HandleMachineStateChanged(string machineId, string displayName, MachineState state)
        {
            MachineStateRowView row = GetOrCreateRow(machineId, displayName);
            row.SetMachineName(displayName);
            ApplyState(machineId, row, state);
        }

        private void HandleMachineWarningChanged(string machineId, bool hasWarning, string reason)
        {
            if (_rowsById.TryGetValue(machineId, out MachineStateRowView row))
            {
                row.SetWarning(hasWarning, reason);
            }
        }

        /// <summary>Belts (hidden unless fault) only get a visible row while they are in Fault.</summary>
        private void ApplyState(string machineId, MachineStateRowView row, MachineState state)
        {
            row.SetState(state);
            bool visible = !_channel.IsHiddenUnlessFault(machineId) || state == MachineState.Fault;
            if (row.gameObject.activeSelf != visible)
            {
                row.gameObject.SetActive(visible);
            }
        }

        private MachineStateRowView GetOrCreateRow(string machineId, string displayName)
        {
            if (_rowsById.TryGetValue(machineId, out MachineStateRowView existingRow))
            {
                return existingRow;
            }

            MachineStateRowView row = Instantiate(_rowTemplate, _rowContainer);
            row.gameObject.SetActive(true);
            row.SetMachineName(displayName);

            UnityAction action = () => _screenController.ShowMachinePanel(machineId);
            row.SetButton(action);
            _rowsById.Add(machineId, row);
            return row;
        }
    }
}
