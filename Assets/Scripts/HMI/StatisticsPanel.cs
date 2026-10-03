using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Production;
using Game.Quality;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// HMI page "line statistics" (Woche 2/3, HMI-Statistik): shift figures from QualitySystem and
    /// FaultSystem. Builds its content from code under <see cref="_contentRoot"/> (no prefab work) and
    /// refreshes while visible.
    ///
    ///  - KPI tiles: run time, good, scrap (rate), throughput, line availability
    ///  - scrap by cause, faults by type, downtime by machine - each as bars, largest first
    ///
    /// Open it with panel id "statistics" (e.g. an <see cref="HmiNavButton"/> on the overview).
    /// </summary>
    public class StatisticsPanel : HmiPanelBase
    {
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private SO_HmiInteractChannel _interactChannel;
        [SerializeField] private QualityInspector _qualityInspector;
        [SerializeField] private FaultMonitor _faultMonitor;

        [Tooltip("Parent for the generated content. Empty = this panel.")]
        [SerializeField] private RectTransform _contentRoot;

        [Tooltip("Font is copied from this text. Empty = TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [SerializeField] private string _backPanelId = "overview";
        [SerializeField] private float _fontSize = 22f;
        [SerializeField] private int _maxRowsPerSection = 6;
        [Min(0.1f)] [SerializeField] private float _refreshIntervalSeconds = 0.5f;

        private sealed class KpiTile
        {
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public Image Bar;
        }

        private sealed class BarRow
        {
            public GameObject Root;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Value;
            public LayoutElement BarLayout;
            public LayoutElement Spacer;
            public Image Bar;
        }

        private sealed class Section
        {
            public TextMeshProUGUI Header;
            public TextMeshProUGUI Empty;
            public readonly List<BarRow> Rows = new List<BarRow>();
        }

        private readonly Dictionary<string, KpiTile> _tiles = new Dictionary<string, KpiTile>();
        private Section _scrapSection;
        private Section _faultSection;
        private Section _downtimeSection;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _backLabel;
        private bool _isBuilt;
        private float _timer;

        private const float BarMaxWidth = 260f;

        private TMP_FontAsset Font => _styleSource != null ? _styleSource.font : null;
        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f);
        private Color ValueColor => _theme != null ? _theme.ValueText : Color.white;
        private Color RowColor => _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f);

        protected override void Awake()
        {
            base.Awake();
            if (_qualityInspector == null)
            {
                _qualityInspector = FindFirstObjectByType<QualityInspector>();
            }

            if (_faultMonitor == null)
            {
                _faultMonitor = FindFirstObjectByType<FaultMonitor>();
            }
        }

        private void OnEnable()
        {
            // The HMI screen root may be activated with this page already marked visible
            // (SetVisible before Awake) - then OnVisibilityChanged never fires.
            if (IsVisible)
            {
                Build();
                Refresh();
            }
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible)
            {
                Build();
                Refresh();
            }
        }

        private void Update()
        {
            if (!IsVisible || !_isBuilt)
            {
                return;
            }

            _timer += Time.unscaledDeltaTime;
            if (_timer >= _refreshIntervalSeconds)
            {
                _timer = 0f;
                Refresh();
            }
        }

        // ---- Build ----

        private void Build()
        {
            if (_isBuilt)
            {
                return;
            }

            _isBuilt = true;
            RectTransform root = HmiUiFactory.CreateRect("StatisticsContent", _contentRoot != null ? _contentRoot : transform);
            HmiUiFactory.Stretch(root, 16f);
            HmiUiFactory.AddVerticalLayout(root.gameObject, 8f, 4);

            // Title row with back button
            RectTransform titleRow = HmiUiFactory.CreateRow(root, "Title", 48f);
            _title = HmiUiFactory.CreateText(titleRow, "Title", "LINE STATISTICS", _fontSize * 1.2f, ValueColor,
                TextAlignmentOptions.MidlineLeft, Font);
            _title.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetFlexible(_title.gameObject);
            Button back = HmiUiFactory.CreateButton(titleRow, "Back", "Back", _fontSize, RowColor, ValueColor, Font, out _backLabel);
            HmiUiFactory.SetWidth(back.gameObject, 140f);
            back.onClick.AddListener(() =>
            {
                if (_interactChannel != null)
                {
                    _interactChannel.Request(_backPanelId, null);
                }
            });

            // KPI tiles
            RectTransform kpiRow = HmiUiFactory.CreateRow(root, "Kpis", 96f, 8f);
            foreach (string id in new[] { "runTime", "good", "scrap", "throughput", "availability" })
            {
                _tiles[id] = CreateTile(kpiRow, id);
            }

            _scrapSection = CreateSection(root, "Scrap");
            _faultSection = CreateSection(root, "Faults");
            _downtimeSection = CreateSection(root, "Downtime");
        }

        private KpiTile CreateTile(Transform parent, string id)
        {
            Image background = HmiUiFactory.CreateImage(parent, "Tile_" + id, RowColor);
            HmiUiFactory.SetFlexible(background.gameObject, 1f, 120f);
            HmiUiFactory.AddVerticalLayout(background.gameObject, 2f, 8);

            var tile = new KpiTile
            {
                Label = HmiUiFactory.CreateText(background.transform, "Label", id, _fontSize * 0.75f, LabelColor,
                    TextAlignmentOptions.TopLeft, Font),
                Value = HmiUiFactory.CreateText(background.transform, "Value", "--", _fontSize * 1.4f, ValueColor,
                    TextAlignmentOptions.MidlineLeft, Font),
                Bar = HmiUiFactory.CreateImage(background.transform, "SeverityBar", Severity(HmiValueSeverity.Inactive))
            };
            tile.Value.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(tile.Label.gameObject, _fontSize);
            HmiUiFactory.SetHeight(tile.Value.gameObject, _fontSize * 1.6f);
            HmiUiFactory.SetHeight(tile.Bar.gameObject, 4f);
            return tile;
        }

        private Section CreateSection(Transform parent, string objectName)
        {
            var section = new Section
            {
                Header = HmiUiFactory.CreateText(parent, objectName + "_Header", objectName, _fontSize * 0.8f, LabelColor,
                    TextAlignmentOptions.BottomLeft, Font)
            };
            section.Header.fontStyle = FontStyles.Bold;
            section.Header.characterSpacing = 4f;
            HmiUiFactory.SetHeight(section.Header.gameObject, _fontSize * 1.6f);

            section.Empty = HmiUiFactory.CreateText(parent, objectName + "_Empty", "--", _fontSize * 0.85f, LabelColor,
                TextAlignmentOptions.MidlineLeft, Font);
            HmiUiFactory.SetHeight(section.Empty.gameObject, _fontSize * 1.4f);

            for (int i = 0; i < _maxRowsPerSection; i++)
            {
                RectTransform row = HmiUiFactory.CreateRow(parent, objectName + "_Row" + i, _fontSize * 1.6f);
                row.gameObject.AddComponent<Image>().color = RowColor;

                var barRow = new BarRow
                {
                    Root = row.gameObject,
                    Label = HmiUiFactory.CreateText(row, "Label", string.Empty, _fontSize, LabelColor,
                        TextAlignmentOptions.MidlineLeft, Font)
                };
                HmiUiFactory.SetFlexible(barRow.Label.gameObject, 1f, 160f);

                // Label | bar (width = share of the largest entry) | spacer (rest) | value
                barRow.Bar = HmiUiFactory.CreateImage(row, "Bar", Severity(HmiValueSeverity.Warning));
                barRow.BarLayout = HmiUiFactory.SetWidth(barRow.Bar.gameObject, 0f);

                RectTransform spacer = HmiUiFactory.CreateRect("Spacer", row);
                barRow.Spacer = HmiUiFactory.SetWidth(spacer.gameObject, BarMaxWidth);

                barRow.Value = HmiUiFactory.CreateText(row, "Value", string.Empty, _fontSize, ValueColor,
                    TextAlignmentOptions.MidlineRight, Font);
                barRow.Value.fontStyle = FontStyles.Bold;
                HmiUiFactory.SetWidth(barRow.Value.gameObject, 120f);

                section.Rows.Add(barRow);
                row.gameObject.SetActive(false);
            }

            return section;
        }

        // ---- Refresh ----

        public void Refresh()
        {
            if (!_isBuilt)
            {
                return;
            }

            _title.text = LocText.Get("hmi.line_statistics", "LINE STATISTICS");
            _backLabel.text = LocText.Get("hmi.back", "Back");
            _scrapSection.Header.text = LocText.Get("hmi.scrap_by_cause", "SCRAP BY CAUSE");
            _faultSection.Header.text = LocText.Get("hmi.faults_by_type", "FAULTS BY TYPE");
            _downtimeSection.Header.text = LocText.Get("hmi.downtime_by_machine", "DOWNTIME BY MACHINE");

            RefreshKpis();

            IEnumerable<KeyValuePair<string, float>> scrap = _qualityInspector != null
                ? _qualityInspector.DefectCounts.Select(e => new KeyValuePair<string, float>(DefectLabel(e.Key), e.Value))
                : Enumerable.Empty<KeyValuePair<string, float>>();
            FillSection(_scrapSection, scrap, v => v.ToString("0"));

            IEnumerable<KeyValuePair<string, float>> faults = _faultMonitor != null
                ? _faultMonitor.CountsByCode.Select(e => new KeyValuePair<string, float>(FaultLabel(e.Key), e.Value))
                : Enumerable.Empty<KeyValuePair<string, float>>();
            FillSection(_faultSection, faults, v => v.ToString("0"));

            IEnumerable<KeyValuePair<string, float>> downtime = _faultMonitor != null
                ? CurrentDowntimeByMachine()
                : Enumerable.Empty<KeyValuePair<string, float>>();
            FillSection(_downtimeSection, downtime, FormatDuration);
        }

        private void RefreshKpis()
        {
            float runTime = _qualityInspector != null ? _qualityInspector.RunTimeSeconds : Time.timeSinceLevelLoad;
            SetTile("runTime", LocText.Get("hmi.run_time", "Run time"), FormatDuration(runTime), HmiValueSeverity.Normal);

            if (_qualityInspector != null)
            {
                int total = _qualityInspector.TotalCount;
                SetTile("good", LocText.Get("hmi.good", "Good"), _qualityInspector.GoodCount.ToString(),
                    total > 0 ? HmiValueSeverity.Normal : HmiValueSeverity.Inactive);

                float rate = _qualityInspector.ScrapRate01;
                HmiValueSeverity scrapSeverity = total == 0 ? HmiValueSeverity.Inactive
                    : rate >= 0.25f ? HmiValueSeverity.Alarm
                    : rate >= 0.1f ? HmiValueSeverity.Warning
                    : HmiValueSeverity.Normal;
                SetTile("scrap", LocText.Get("hmi.scrap", "Scrap"),
                    total > 0 ? $"{_qualityInspector.ScrapCount} ({rate * 100f:0}%)" : "0", scrapSeverity);

                SetTile("throughput", LocText.Get("hmi.throughput_per_minute", "Throughput / min"),
                    _qualityInspector.ThroughputPerMinute.ToString("0.0"),
                    total > 0 ? HmiValueSeverity.Normal : HmiValueSeverity.Inactive);
            }

            if (_faultMonitor != null && runTime > 0f)
            {
                float availability = Mathf.Clamp01(1f - _faultMonitor.LineDownSeconds / runTime);
                HmiValueSeverity severity = availability < 0.75f ? HmiValueSeverity.Alarm
                    : availability < 0.9f ? HmiValueSeverity.Warning
                    : HmiValueSeverity.Normal;
                SetTile("availability", LocText.Get("hmi.availability", "Availability"), $"{availability * 100f:0}%", severity);
            }
        }

        private IEnumerable<KeyValuePair<string, float>> CurrentDowntimeByMachine()
        {
            var result = new Dictionary<string, float>(_faultMonitor.DowntimeByMachine.ToDictionary(e => e.Key, e => e.Value));
            foreach (FaultMonitor.ActiveFault fault in _faultMonitor.ActiveFaults)
            {
                result.TryGetValue(fault.MachineName, out float seconds);
                result[fault.MachineName] = seconds + fault.DurationSeconds;
            }
            return result;
        }

        private void SetTile(string id, string label, string value, HmiValueSeverity severity)
        {
            if (!_tiles.TryGetValue(id, out KpiTile tile))
            {
                return;
            }

            tile.Label.text = label;
            tile.Value.text = value;
            tile.Bar.color = Severity(severity);
        }

        private void FillSection(Section section, IEnumerable<KeyValuePair<string, float>> entries, System.Func<float, string> format)
        {
            List<KeyValuePair<string, float>> sorted = entries.OrderByDescending(e => e.Value).ToList();
            float max = sorted.Count > 0 ? Mathf.Max(0.0001f, sorted[0].Value) : 1f;

            section.Empty.gameObject.SetActive(sorted.Count == 0);
            section.Empty.text = LocText.Get("hmi.none", "none");

            for (int i = 0; i < section.Rows.Count; i++)
            {
                BarRow row = section.Rows[i];
                bool active = i < sorted.Count;
                if (row.Root.activeSelf != active)
                {
                    row.Root.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                float width = BarMaxWidth * sorted[i].Value / max;
                row.Label.text = sorted[i].Key;
                row.Value.text = format(sorted[i].Value);
                row.BarLayout.preferredWidth = width;
                row.BarLayout.minWidth = width;
                row.Spacer.preferredWidth = BarMaxWidth - width;
                row.Spacer.minWidth = BarMaxWidth - width;
            }
        }

        // ---- Labels ----

        private static string DefectLabel(string code)
        {
            // "WeightMissing" -> "Missing measurement: Weight"
            const string missing = QualityInspector.MissingSuffix;
            if (code.EndsWith(missing) && code.Length > missing.Length)
            {
                string measurement = code.Substring(0, code.Length - missing.Length);
                return LocText.Get("defect.missing", "Missing measurement") + ": " +
                       LocText.Get("defect." + LocText.Snake(measurement), measurement);
            }

            return LocText.Get("defect." + LocText.Snake(code), code);
        }

        private string FaultLabel(string code)
        {
            FaultDefinition definition = _faultMonitor != null && _faultMonitor.Catalog != null
                ? _faultMonitor.Catalog.Find(code)
                : null;
            return definition != null ? definition.Message : code;
        }

        private static string FormatDuration(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return total >= 3600
                ? $"{total / 3600}:{total / 60 % 60:00}:{total % 60:00}"
                : $"{total / 60}:{total % 60:00}";
        }

        private Color Severity(HmiValueSeverity severity)
        {
            if (_theme == null)
            {
                return Color.grey;
            }

            return severity switch
            {
                HmiValueSeverity.Normal => _theme.Normal,
                HmiValueSeverity.Warning => _theme.Warning,
                HmiValueSeverity.Alarm => _theme.Alarm,
                _ => _theme.Inactive
            };
        }
    }
}
