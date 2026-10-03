using System;
using System.Collections.Generic;
using Game.Production;
using UnityEngine;

namespace Game.Quality
{
    /// <summary>Verdict for one finished product.</summary>
    public sealed class QualityResult
    {
        public ProductInstance Product;
        public float Time;
        public readonly List<string> Defects = new List<string>();

        public bool IsGood => Defects.Count == 0;

        /// <summary>Defects as one line for logs / HMI ("Underweight, BakeTimeLong").</summary>
        public string Summary => IsGood ? "OK" : string.Join(", ", Defects);
    }

    /// <summary>
    /// QualitySystem (Woche 2): judges every product that leaves the line at a <see cref="LineEndSink"/>
    /// against an <see cref="SO_QualitySpec"/>. Uses only the values stations already wrote to the
    /// <see cref="ProductInstance"/> (weight, press setting, dosed grams, bake time, zone temperatures) plus
    /// a scrap mark set by a station (<see cref="ProductInstance.RejectReason"/>). Machines never judge quality.
    ///
    /// Counts good / scrap units, defects per code and throughput (good units per minute) for the HMI.
    /// </summary>
    [DisallowMultipleComponent]
    public class QualityInspector : MonoBehaviour
    {
        public const string DefectNotFinished = "NotFinished";
        public const string DefectRejected = "Rejected";
        public const string DefectUnderweight = "Underweight";
        public const string DefectOverweight = "Overweight";
        public const string DefectDiameter = "Diameter";
        public const string DefectThickness = "Thickness";
        public const string DefectSauceLow = "SauceLow";
        public const string DefectSauceHigh = "SauceHigh";
        public const string DefectToppingLow = "ToppingLow";
        public const string DefectToppingHigh = "ToppingHigh";
        public const string DefectUnderbaked = "Underbaked";
        public const string DefectOverbaked = "Overbaked";
        public const string DefectBakeTemperature = "BakeTemperature";
        public const string DefectCoolingTemperature = "CoolingTemperature";
        public const string DefectColdChain = "ColdChain";
        public const string MissingSuffix = "Missing";

        [SerializeField] private SO_QualitySpec _spec;
        [SerializeField] private LineEndSink _lineEnd;

        [Tooltip("Number of recent results kept for the HMI.")]
        [Min(1)] [SerializeField] private int _historySize = 20;

        [SerializeField] private bool _logResults = true;

        private readonly Queue<float> _goodTimestamps = new Queue<float>();
        private readonly Dictionary<string, int> _defectCounts = new Dictionary<string, int>();
        private readonly List<QualityResult> _history = new List<QualityResult>();

        /// <summary>Raised after every evaluated product.</summary>
        public event Action<QualityResult> ProductEvaluated;

        public int GoodCount { get; private set; }
        public int ScrapCount { get; private set; }
        public int TotalCount => GoodCount + ScrapCount;
        public float ScrapRate01 => TotalCount > 0 ? (float)ScrapCount / TotalCount : 0f;
        public IReadOnlyDictionary<string, int> DefectCounts => _defectCounts;
        public IReadOnlyList<QualityResult> History => _history;
        public QualityResult LastResult => _history.Count > 0 ? _history[_history.Count - 1] : null;
        public SO_QualitySpec Spec => _spec;

        private float _restoredRunTimeSeconds;

        /// <summary>Production time of this shift: time since scene start plus the run time of a loaded save.</summary>
        public float RunTimeSeconds => _restoredRunTimeSeconds + Time.timeSinceLevelLoad;

        /// <summary>Good units per minute over the spec's throughput window.</summary>
        public float ThroughputPerMinute
        {
            get
            {
                TrimThroughputWindow();
                float window = _spec != null ? _spec.ThroughputWindowSeconds : 60f;
                float elapsed = Mathf.Min(window, Time.timeSinceLevelLoad);
                return elapsed > 0f ? _goodTimestamps.Count * 60f / elapsed : 0f;
            }
        }

        private void OnEnable()
        {
            if (_lineEnd != null)
            {
                _lineEnd.ProductArrived += HandleProductArrived;
            }
        }

        private void OnDisable()
        {
            if (_lineEnd != null)
            {
                _lineEnd.ProductArrived -= HandleProductArrived;
            }
        }

        private void HandleProductArrived(ProductInstance product) => Evaluate(product);

        /// <summary>Judges a product. Public so other sinks (e.g. a manual reject bin) can feed it too.</summary>
        public QualityResult Evaluate(ProductInstance product)
        {
            var result = new QualityResult { Product = product, Time = Time.time };
            if (product == null)
            {
                return result;
            }

            if (_spec == null)
            {
                Debug.LogWarning($"{name}: no SO_QualitySpec assigned - every product counts as good.", this);
            }
            else
            {
                Check(product, _spec, result.Defects);
            }

            Record(result);
            return result;
        }

        private static void Check(ProductInstance p, SO_QualitySpec spec, List<string> defects)
        {
            if (p.IsRejected)
            {
                defects.Add($"{DefectRejected}:{p.RejectReason}");
            }

            if (p.CurrentState < spec.RequiredFinalState)
            {
                defects.Add($"{DefectNotFinished}:{p.CurrentState}");
            }

            bool require = spec.RequireAllMeasurements;

            if (spec.CheckWeight)
            {
                CheckRange(p.MeasuredWeightGrams, spec.TargetWeightGrams, spec.WeightToleranceGrams,
                    DefectUnderweight, DefectOverweight, "Weight", require, defects);
            }

            if (spec.CheckDimensions)
            {
                CheckRange(p.FormedDiameterCm, spec.TargetDiameterCm, spec.DiameterToleranceCm,
                    DefectDiameter, DefectDiameter, DefectDiameter, require, defects);
                CheckRange(p.FormedThicknessMm, spec.TargetThicknessMm, spec.ThicknessToleranceMm,
                    DefectThickness, DefectThickness, DefectThickness, require, defects);
            }

            if (spec.CheckDosing)
            {
                CheckRange(p.DosedSauceGrams, spec.TargetSauceGrams, spec.SauceToleranceGrams,
                    DefectSauceLow, DefectSauceHigh, "Sauce", require, defects);
                CheckRange(p.DosedToppingGrams, spec.TargetToppingGrams, spec.ToppingToleranceGrams,
                    DefectToppingLow, DefectToppingHigh, "Topping", require, defects);
            }

            if (spec.CheckBaking)
            {
                CheckRange(p.ActualBakeTimeSeconds, spec.TargetBakeTimeSeconds, spec.BakeTimeToleranceSeconds,
                    DefectUnderbaked, DefectOverbaked, "BakeTime", require, defects);

                if (p.BakeTemperatureCelsius.HasValue)
                {
                    float t = p.BakeTemperatureCelsius.Value;
                    if (t < spec.MinBakeTemperatureCelsius || t > spec.MaxBakeTemperatureCelsius)
                    {
                        defects.Add(DefectBakeTemperature);
                    }
                }
                else if (require)
                {
                    defects.Add(DefectBakeTemperature + MissingSuffix);
                }
            }

            if (spec.CheckCooling)
            {
                CheckMax(p.CoolingTemperatureCelsius, spec.MaxCoolingTemperatureCelsius,
                    DefectCoolingTemperature, require, defects);
            }

            if (spec.CheckFreezing)
            {
                CheckMax(p.FreezingTemperatureCelsius, spec.MaxFreezingTemperatureCelsius,
                    DefectColdChain, require, defects);
            }
        }

        private static void CheckRange(float? value, float target, float tolerance,
            string lowCode, string highCode, string missingCode, bool require, List<string> defects)
        {
            if (!value.HasValue)
            {
                if (require)
                {
                    defects.Add(missingCode + MissingSuffix);
                }
                return;
            }

            if (value.Value < target - tolerance)
            {
                defects.Add(lowCode);
            }
            else if (value.Value > target + tolerance)
            {
                defects.Add(highCode);
            }
        }

        private static void CheckMax(float? value, float max, string code, bool require, List<string> defects)
        {
            if (!value.HasValue)
            {
                if (require)
                {
                    defects.Add(code + MissingSuffix);
                }
                return;
            }

            if (value.Value > max)
            {
                defects.Add(code);
            }
        }

        private void Record(QualityResult result)
        {
            if (result.IsGood)
            {
                GoodCount++;
                _goodTimestamps.Enqueue(result.Time);
            }
            else
            {
                ScrapCount++;
                foreach (string defect in result.Defects)
                {
                    // Count by code without the detail after ':' (Rejected:PressInterrupted -> Rejected).
                    int colon = defect.IndexOf(':');
                    string code = colon > 0 ? defect.Substring(0, colon) : defect;
                    _defectCounts.TryGetValue(code, out int count);
                    _defectCounts[code] = count + 1;
                }
            }

            _history.Add(result);
            if (_history.Count > _historySize)
            {
                _history.RemoveAt(0);
            }

            if (_logResults)
            {
                Debug.Log($"[Quality] {result.Product.InstanceId} ({result.Product.CurrentState}): {result.Summary}", this);
            }

            ProductEvaluated?.Invoke(result);
        }

        // ---- Save/Load ----

        /// <summary>Counters for the save file (history and throughput window start empty after loading).</summary>
        public void CaptureStatistics(SaveValues values)
        {
            values.Set("good", GoodCount);
            values.Set("runTime", RunTimeSeconds);
            values.Set("scrap", ScrapCount);
            foreach (KeyValuePair<string, int> entry in _defectCounts)
            {
                values.Set("defect." + entry.Key, entry.Value);
            }
        }

        public void RestoreStatistics(SaveValues values)
        {
            GoodCount = values.GetInt("good", GoodCount);
            _restoredRunTimeSeconds = Mathf.Max(0f, values.GetFloat("runTime", 0f) - Time.timeSinceLevelLoad);
            ScrapCount = values.GetInt("scrap", ScrapCount);
            _defectCounts.Clear();
            for (int i = 0; i < values.Keys.Count; i++)
            {
                if (values.Keys[i].StartsWith("defect.", StringComparison.Ordinal))
                {
                    _defectCounts[values.Keys[i].Substring(7)] = (int)Math.Round(values.Values[i]);
                }
            }
        }

        private void TrimThroughputWindow()
        {
            float window = _spec != null ? _spec.ThroughputWindowSeconds : 60f;
            while (_goodTimestamps.Count > 0 && Time.time - _goodTimestamps.Peek() > window)
            {
                _goodTimestamps.Dequeue();
            }
        }
    }
}
