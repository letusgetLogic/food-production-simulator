using System.Collections.Generic;
using Game.Production;
using Game.Quality;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Feeds the line figures of every <see cref="OverviewPanel"/> in the scene: throughput, produced and
    /// scrap units from the QualitySystem, the alarm list and active-fault tile from the FaultSystem.
    /// Polls (0.5 s) instead of pushing per event so several overview panels and a closed HMI stay cheap.
    /// </summary>
    public class LineStatusBinder : MonoBehaviour
    {
        [SerializeField] private FaultMonitor _faultMonitor;
        [SerializeField] private QualityInspector _qualityInspector;

        [Tooltip("Panels to feed. Empty = all OverviewPanels in the scene (found at start, including inactive).")]
        [SerializeField] private List<OverviewPanel> _panels = new List<OverviewPanel>();

        [Min(0.1f)] [SerializeField] private float _refreshIntervalSeconds = 0.5f;

        [Tooltip("Scrap rate above which the scrap tile turns amber.")]
        [Range(0f, 1f)] [SerializeField] private float _scrapWarningRate = 0.1f;

        [Tooltip("Scrap rate above which the scrap tile turns red.")]
        [Range(0f, 1f)] [SerializeField] private float _scrapAlarmRate = 0.25f;

        private readonly List<HmiAlarmEntry> _alarms = new List<HmiAlarmEntry>();
        private float _timer;

        private void Start()
        {
            if (_panels.Count == 0)
            {
                _panels.AddRange(FindObjectsByType<OverviewPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            }

            if (_faultMonitor == null)
            {
                _faultMonitor = FindFirstObjectByType<FaultMonitor>();
            }

            if (_qualityInspector == null)
            {
                _qualityInspector = FindFirstObjectByType<QualityInspector>();
            }

            if (_faultMonitor != null)
            {
                _faultMonitor.FaultsChanged += Refresh;
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (_faultMonitor != null)
            {
                _faultMonitor.FaultsChanged -= Refresh;
            }
        }

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer >= _refreshIntervalSeconds)
            {
                _timer = 0f;
                Refresh();
            }
        }

        public void Refresh()
        {
            BuildAlarms();

            foreach (OverviewPanel panel in _panels)
            {
                if (panel == null)
                {
                    continue;
                }

                panel.SetFigureLabels(
                    LocText.Get("hmi.throughput_per_minute", "Throughput / min"),
                    LocText.Get("hmi.produced", "Produced"),
                    LocText.Get("hmi.scrap", "Scrap"),
                    LocText.Get("hmi.active_faults", "Active faults"));
                panel.SetAlarms(_alarms);

                if (_qualityInspector != null)
                {
                    panel.SetThroughput(_qualityInspector.ThroughputPerMinute,
                        _qualityInspector.TotalCount > 0 ? HmiValueSeverity.Normal : HmiValueSeverity.Inactive);
                    panel.SetProducedUnits(_qualityInspector.GoodCount);
                    panel.SetScrapUnits(_qualityInspector.ScrapCount, ScrapSeverity());
                }
            }
        }

        private void BuildAlarms()
        {
            _alarms.Clear();
            if (_faultMonitor == null)
            {
                return;
            }

            foreach (FaultMonitor.ActiveFault fault in _faultMonitor.ActiveFaults)
            {
                string message = string.IsNullOrEmpty(fault.Remedy)
                    ? fault.Message
                    : $"{fault.Message}\n<size=80%>{fault.Remedy}</size>";
                _alarms.Add(new HmiAlarmEntry(fault.MachineName, message, fault.IsInMaintenance));
            }
        }

        private HmiValueSeverity ScrapSeverity()
        {
            if (_qualityInspector.TotalCount == 0)
            {
                return HmiValueSeverity.Inactive;
            }

            float rate = _qualityInspector.ScrapRate01;
            return rate >= _scrapAlarmRate ? HmiValueSeverity.Alarm
                : rate >= _scrapWarningRate ? HmiValueSeverity.Warning
                : HmiValueSeverity.Normal;
        }
    }
}
