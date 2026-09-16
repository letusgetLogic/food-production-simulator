using System.Collections.Generic;
using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Line overview: throughput figures, one state row per machine, active
    /// faults. All setters are placeholder-safe – nothing here assumes a data
    /// source exists yet.
    /// </summary>
    public class OverviewPanel : HmiPanelBase
    {
        [Header("Line figures")]
        [SerializeField] private StatusTileView _throughputTile;
        [SerializeField] private StatusTileView _producedUnitsTile;
        [SerializeField] private StatusTileView _scrapUnitsTile;
        [SerializeField] private StatusTileView _activeFaultsTile;

        [Header("Machines")]
        [SerializeField] private List<MachineStateRowView> _machineRows = new List<MachineStateRowView>();

        [Header("Alarms")]
        [SerializeField] private AlarmListView _alarmList;

        public void SetThroughput(float unitsPerMinute, HmiValueSeverity severity) =>
            _throughputTile?.SetValue(unitsPerMinute, severity);

        public void SetProducedUnits(int units) =>
            _producedUnitsTile?.SetValue(units, HmiValueSeverity.Normal);

        public void SetScrapUnits(int units, HmiValueSeverity severity) =>
            _scrapUnitsTile?.SetValue(units, severity);

        public void SetMachineState(string machineName, MachineState state)
        {
            MachineStateRowView row = FindRow(machineName);
            row?.SetState(state);
        }

        public void SetAlarms(IReadOnlyList<HmiAlarmEntry> alarms)
        {
            _alarmList?.SetAlarms(alarms);

            int count = alarms?.Count ?? 0;
            _activeFaultsTile?.SetValue(
                count, count > 0 ? HmiValueSeverity.Alarm : HmiValueSeverity.Normal);
        }

        private MachineStateRowView FindRow(string machineName)
        {
            for (int i = 0; i < _machineRows.Count; i++)
            {
                if (_machineRows[i] != null && _machineRows[i].MachineName == machineName)
                {
                    return _machineRows[i];
                }
            }

            return null;
        }
    }
}
