using System.Collections.Generic;
using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    // Line overview: throughput figures, one state row per machine, active
    // faults. Machine rows are driven by MachineOverviewChannel - this panel
    // subscribes itself, so any number of OverviewPanel instances (terminal,
    // tablet, ...) can exist without anything needing to reference them.
    public class OverviewPanel : HmiPanelBase
    {
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
                GetOrCreateRow(entry.Key, displayName).SetState(entry.Value);
            }

            _channel.MachineStateChanged += HandleMachineStateChanged;
        }

        private void OnDisable() => _channel.MachineStateChanged -= HandleMachineStateChanged;

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

        private void HandleMachineStateChanged(string machineId, string displayName, MachineState state) =>
            GetOrCreateRow(machineId, displayName).SetState(state);

        private MachineStateRowView GetOrCreateRow(string machineId, string displayName)
        {
            if (_rowsById.TryGetValue(machineId, out MachineStateRowView existingRow))
            {
                return existingRow;
            }

            MachineStateRowView row = Instantiate(_rowTemplate, _rowContainer);
            row.gameObject.SetActive(true);
            row.SetMachineName(displayName);
            _rowsById.Add(machineId, row);
            return row;
        }
    }
}
