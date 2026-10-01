using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Durchlaufstation (oven, cooling tunnel, shock freezer): products ride through on the station's
    /// own <see cref="ConveyorBelt"/> and are processed while they are inside the
    /// <see cref="ProcessZone"/> - the station never takes a single product off the belt.
    ///
    ///  - Dwell time (e.g. "Backzeit 15 s") is NOT a cycle per product but
    ///    zone length / belt speed. <see cref="SetDwellTime"/> therefore sets the belt speed.
    ///  - Temperature comes from a <see cref="TemperatureSensor"/>; the station sets its setpoint and
    ///    judges the reading (warning outside tolerance, fault outside the valid range after a grace time).
    ///  - When a product leaves the zone, the station records the real dwell time and the average zone
    ///    temperature it was exposed to, and switches InputState -> OutputState. It deliberately does not
    ///    judge quality (too short/too cold) - that is the QualitySystem's job (Woche 2). If the belt
    ///    stops, the product stays inside longer and the recorded dwell time grows (= overbaked).
    ///  - Upstream belts see this station through <see cref="IsReadyForInfeed"/>, so their buffers fill
    ///    up instead of pushing products into a stopped tunnel.
    ///
    /// Deliberately does NOT implement IProductProcessor (products are handed over by physics).
    /// No RecipeDefinition binding in code: dwell/temperature are operator settings from the HMI.
    /// </summary>
    public class ContinuousProcessStation : MachineBase, IInfeedReadiness
    {
        /// <summary>Content messages this station reports via NotifyContentChanged.</summary>
        public enum ProcessInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            ReachingTemperature,
            ProductCompleted,
            WrongProduct,
            TemperatureDeviation,
            TemperatureOutOfRange,
            HeatUpTimeout,
            BeltFault
        }

        public const string FaultReasonTemperatureOutOfRange = "TemperatureOutOfRange";
        public const string FaultReasonHeatUpTimeout = "HeatUpTimeout";
        public const string FaultReasonBeltFault = "BeltFault";
        public const string WarningReasonTemperatureDeviation = "TemperatureDeviation";
        public const string WarningReasonWrongProduct = "WrongProduct";

        [SerializeField] private SO_ContinuousProcessConfig _config;

        [Header("Components")]
        [Tooltip("The belt running through the tunnel. Owned by this station - do NOT list it in a ConveyorLineController.")]
        [SerializeField] private ConveyorBelt _belt;

        [SerializeField] private ProcessZone _processZone;

        [Tooltip("Zone temperature. Optional if the config does not use temperature.")]
        [SerializeField] private TemperatureSensor _temperatureSensor;

        [Header("HMI Content")]
        [SerializeField] private List<Content<ProcessInfo>> _contents = new List<Content<ProcessInfo>>();

        // Accumulated temperature exposure per product inside the zone: (sum of T*dt, sum of dt).
        private readonly Dictionary<ProductToken, Vector2> _exposure = new Dictionary<ProductToken, Vector2>();
        private readonly List<ProductToken> _scratch = new List<ProductToken>();

        private float _dwellSeconds;
        private float _targetTemperature;
        private float _outOfRangeTimer;
        private float _wrongProductWarningTimer;
        private int _completedCount;
        private int _wrongProductCount;

        private ProcessInfo? _lastReportedInfo;
        private string _lastReportedDetail;
        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        /// <summary>Raised when a product has left the zone and was switched to the output state.</summary>
        public event Action<ProductInstance> ProductProcessed;

        public float DwellTimeSeconds => _dwellSeconds;
        public float TargetTemperatureCelsius => _targetTemperature;
        public float CurrentTemperatureCelsius => _temperatureSensor != null ? _temperatureSensor.CurrentValue : float.NaN;
        public int ProductsInside => _processZone != null ? _processZone.ProductCount : 0;
        public int CompletedCount => _completedCount;
        public int WrongProductCount => _wrongProductCount;
        public ConveyorBelt Belt => _belt;

        /// <inheritdoc />
        public bool IsReadyForInfeed => CurrentState == MachineState.Running && _belt != null && _belt.IsMoving;

        private bool UsesTemperature => _config.UsesTemperature && _temperatureSensor != null;

        // ---- Unity lifecycle ----

        private void Awake()
        {
            _dwellSeconds = _config.DefaultDwellSeconds;
            _targetTemperature = _config.DefaultTargetTemperatureCelsius;

            if (_temperatureSensor != null)
            {
                _temperatureSensor.SetHeatingTarget(_targetTemperature);
            }
        }

        private void Start()
        {
            // Belt config is read in the belt's Awake - apply our speed afterwards.
            ApplyBeltSpeed();
        }

        private void OnEnable()
        {
            foreach (Content<ProcessInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged += content.SetInfo;
                }
            }

            if (_processZone != null)
            {
                _processZone.ProductEntered += HandleProductEntered;
                _processZone.ProductExited += HandleProductExited;
            }

            if (_belt != null)
            {
                _belt.StateChanged += HandleBeltStateChanged;
            }
        }

        private void OnDisable()
        {
            foreach (Content<ProcessInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged -= content.SetInfo;
                }
            }

            if (_processZone != null)
            {
                _processZone.ProductEntered -= HandleProductEntered;
                _processZone.ProductExited -= HandleProductExited;
            }

            if (_belt != null)
            {
                _belt.StateChanged -= HandleBeltStateChanged;
            }
        }

        private void Update()
        {
            AccumulateExposure(Time.deltaTime);

            if (CurrentState != MachineState.Running)
            {
                return;
            }

            if (_wrongProductWarningTimer > 0f)
            {
                _wrongProductWarningTimer -= Time.deltaTime;
            }

            MonitorTemperature(Time.deltaTime);
        }

        // ---- HMI parameters ----

        /// <summary>Operator setting (recipe terminal, e.g. Backzeit). Sets the belt speed = zone length / dwell.</summary>
        public void SetDwellTime(float seconds)
        {
            _dwellSeconds = Mathf.Clamp(seconds, _config.MinDwellSeconds, _config.MaxDwellSeconds);
            ApplyBeltSpeed();
        }

        /// <summary>Operator setting (recipe terminal, e.g. Backtemperatur).</summary>
        public void SetTargetTemperature(float celsius)
        {
            _targetTemperature = Mathf.Clamp(celsius,
                _config.MinTargetTemperatureCelsius, _config.MaxTargetTemperatureCelsius);

            if (_temperatureSensor != null)
            {
                _temperatureSensor.SetHeatingTarget(_targetTemperature);
            }
        }

        private void ApplyBeltSpeed()
        {
            if (_belt == null || _processZone == null)
            {
                return;
            }

            float length = _processZone.LengthMeters;
            _belt.SetSpeed(length / _dwellSeconds);

            if (!Mathf.Approximately(_belt.CurrentSpeedMetersPerSecond, length / _dwellSeconds))
            {
                Debug.LogWarning(
                    $"{name}: dwell {_dwellSeconds:0.0} s needs {length / _dwellSeconds:0.000} m/s, " +
                    $"belt config allows only {_belt.CurrentSpeedMetersPerSecond:0.000} m/s - adjust SO_ConveyorConfig min/max speed.", this);
            }
        }

        // ---- Process ----

        private void HandleProductEntered(ProductToken token) => _exposure[token] = Vector2.zero;

        private void HandleProductExited(ProductToken token, float secondsInside)
        {
            Vector2 exposure = _exposure.TryGetValue(token, out Vector2 value) ? value : Vector2.zero;
            _exposure.Remove(token);

            ProductInstance product = token != null ? token.Product : null;
            if (product == null)
            {
                return;
            }

            if (product.CurrentState != _config.InputState)
            {
                _wrongProductCount++;
                _wrongProductWarningTimer = _config.WrongProductWarningSeconds;
                Report(ProcessInfo.WrongProduct, product.CurrentState.ToString(), force: true);
                return;
            }

            product.CurrentState = _config.OutputState;

            if (UsesTemperature && exposure.y > 0f)
            {
                product.MeasuredTemperatureCelsius = exposure.x / exposure.y;
            }

            if (_config.RecordDwellAsBakeTime)
            {
                product.ActualBakeTimeSeconds = secondsInside;
            }

            _completedCount++;
            ProductProcessed?.Invoke(product);
            Report(ProcessInfo.ProductCompleted, $"{secondsInside:0.0} s", force: true);
        }

        private void AccumulateExposure(float deltaTime)
        {
            if (_exposure.Count == 0)
            {
                return;
            }

            float temperature = UsesTemperature ? _temperatureSensor.CurrentValue : 0f;

            _scratch.Clear();
            _scratch.AddRange(_exposure.Keys);
            foreach (ProductToken token in _scratch)
            {
                if (token == null)
                {
                    _exposure.Remove(token);
                    continue;
                }

                Vector2 value = _exposure[token];
                _exposure[token] = new Vector2(value.x + temperature * deltaTime, value.y + deltaTime);
            }
        }

        private void MonitorTemperature(float deltaTime)
        {
            if (!UsesTemperature)
            {
                SetWarning(_wrongProductWarningTimer > 0f, WarningReasonWrongProduct);
                return;
            }

            float temperature = _temperatureSensor.CurrentValue;
            bool isOutOfRange = temperature < _config.MinValidTemperatureCelsius
                || temperature > _config.MaxValidTemperatureCelsius;
            bool isDeviating = Mathf.Abs(temperature - _targetTemperature) > _config.WarningToleranceCelsius;

            _temperatureSensor.SetNormalRangeExternally(!isOutOfRange);

            if (isOutOfRange)
            {
                _outOfRangeTimer += deltaTime;
                Report(ProcessInfo.TemperatureOutOfRange, $"{temperature:0} °C");
                if (_outOfRangeTimer >= _config.OutOfRangeFaultGraceSeconds)
                {
                    _outOfRangeTimer = 0f;
                    TriggerFault(FaultReasonTemperatureOutOfRange);
                    return;
                }
            }
            else
            {
                _outOfRangeTimer = 0f;
                if (isDeviating)
                {
                    Report(ProcessInfo.TemperatureDeviation, $"{temperature:0} / {_targetTemperature:0} °C");
                }
            }

            if (isOutOfRange || isDeviating)
            {
                SetWarning(true, WarningReasonTemperatureDeviation);
            }
            else
            {
                SetWarning(_wrongProductWarningTimer > 0f, WarningReasonWrongProduct);
            }
        }

        private void HandleBeltStateChanged(MachineState previous, MachineState next)
        {
            // Products stuck in a hot/cold tunnel are a station problem, not only a belt problem.
            if (next == MachineState.Fault && CurrentState != MachineState.Fault
                && CurrentState != MachineState.Maintenance)
            {
                Report(ProcessInfo.BeltFault, force: true);
                TriggerFault(FaultReasonBeltFault);
            }
        }

        private void SetTemperatureControl(bool isActive)
        {
            if (_temperatureSensor != null)
            {
                _temperatureSensor.SetHeating(isActive && _config.UsesTemperature);
            }
        }

        // ---- Content messages ----

        private void Report(ProcessInfo info, string detail = null, bool force = false)
        {
            if (!force && _lastReportedInfo == info && _lastReportedDetail == detail)
            {
                return;
            }

            _lastReportedInfo = info;
            _lastReportedDetail = detail;
            NotifyContentChanged(string.IsNullOrEmpty(detail) ? Info(info) : detail + "\n" + Info(info));
        }

        private string Info(ProcessInfo info)
        {
            string localized = _contents.Find(c => c.State == info)?.Info;
            return string.IsNullOrEmpty(localized) ? FallbackText(info) : localized;
        }

        /// <summary>Used until the localization keys exist in the table.</summary>
        private static string FallbackText(ProcessInfo info) => info switch
        {
            ProcessInfo.ReachingTemperature => "Reaching temperature",
            ProcessInfo.ProductCompleted => "Product completed",
            ProcessInfo.WrongProduct => "Wrong product passed through",
            ProcessInfo.TemperatureDeviation => "Temperature deviation",
            ProcessInfo.TemperatureOutOfRange => "Temperature out of range",
            ProcessInfo.HeatUpTimeout => "Setpoint not reached",
            ProcessInfo.BeltFault => "Belt fault",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady()
        {
            SetTemperatureControl(false);
            Report(ProcessInfo.Ready, force: true);
        }

        protected override void OnEnterStarting()
        {
            Report(ProcessInfo.Starting, force: true);
            SetTemperatureControl(true);
            RestartRoutine(ref _startingRoutine, StartingRoutine());
        }

        protected override void OnEnterRunning()
        {
            _outOfRangeTimer = 0f;
            Report(ProcessInfo.Running, force: true);

            if (_belt != null)
            {
                _belt.RequestRun();
            }
        }

        protected override void OnEnterStopping()
        {
            Report(ProcessInfo.Stopping, force: true);
            SetWarning(false);

            if (_belt != null)
            {
                _belt.StopRun();
            }

            RestartRoutine(ref _stoppingRoutine, StoppingRoutine());
        }

        protected override void OnEnterStopped()
        {
            SetTemperatureControl(_config.KeepTemperatureWhileStopped);
            Report(ProcessInfo.Stopped, force: true);
        }

        protected override void OnEnterFault(string reason)
        {
            if (reason == FaultReasonHeatUpTimeout)
            {
                Report(ProcessInfo.HeatUpTimeout, force: true);
            }
            else if (reason == FaultReasonTemperatureOutOfRange)
            {
                Report(ProcessInfo.TemperatureOutOfRange, force: true);
            }
            else if (reason != FaultReasonBeltFault)
            {
                Report(ProcessInfo.Fault, force: true);
            }

            SetWarning(false);
            StopRoutine(ref _startingRoutine);
            StopRoutine(ref _stoppingRoutine);

            if (_belt != null)
            {
                _belt.StopRun();
            }
        }

        protected override void OnEnterMaintenance()
        {
            SetTemperatureControl(false);
            _outOfRangeTimer = 0f;
            Report(ProcessInfo.Maintenance, force: true);
        }

        private IEnumerator StartingRoutine()
        {
            yield return new WaitForSeconds(_config.StartupDurationSeconds);

            if (UsesTemperature && _config.RequireTemperatureBeforeRun)
            {
                float elapsed = 0f;
                while (Mathf.Abs(_temperatureSensor.CurrentValue - _targetTemperature) > _config.WarningToleranceCelsius)
                {
                    Report(ProcessInfo.ReachingTemperature, $"{_temperatureSensor.CurrentValue:0} / {_targetTemperature:0} °C");

                    elapsed += Time.deltaTime;
                    if (elapsed >= _config.HeatUpTimeoutSeconds)
                    {
                        _startingRoutine = null;
                        TriggerFault(FaultReasonHeatUpTimeout);
                        yield break;
                    }
                    yield return null;
                }
            }

            _startingRoutine = null;
            SetState(MachineState.Running);
        }

        private IEnumerator StoppingRoutine()
        {
            yield return new WaitForSeconds(_config.ShutdownDurationSeconds);
            _stoppingRoutine = null;
            SetState(MachineState.Stopped);
        }

        private void RestartRoutine(ref Coroutine routine, IEnumerator body)
        {
            StopRoutine(ref routine);
            routine = StartCoroutine(body);
        }

        private void StopRoutine(ref Coroutine routine)
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
        }
    }
}
