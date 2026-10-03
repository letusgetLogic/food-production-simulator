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
    public class ContinuousProcessStation : MachineBase, IInfeedReadiness, IMachineParameterSource, ISaveableState
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
        private float _lastDwellSeconds = -1f;
        private float _lastExposureCelsius = float.NaN;
        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

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

        /// <summary>True when this station controls a zone temperature (oven, cooling, freezer - not packaging).</summary>
        public bool UsesTemperatureControl => UsesTemperature;

        /// <summary>Input -> output state of this station (e.g. ToppedPizza -> BakedPizza).</summary>
        public ProductState InputState => _config.InputState;
        public ProductState OutputState => _config.OutputState;

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
                float average = exposure.x / exposure.y;
                product.MeasuredTemperatureCelsius = average;
                RecordStageTemperature(product, average);
            }

            if (_config.RecordDwellAsBakeTime)
            {
                product.ActualBakeTimeSeconds = secondsInside;
            }

            _completedCount++;
            _lastDwellSeconds = secondsInside;
            _lastExposureCelsius = UsesTemperature && exposure.y > 0f ? exposure.x / exposure.y : float.NaN;
            ProductProcessed?.Invoke(product);
            Report(ProcessInfo.ProductCompleted, $"{secondsInside:0.0} s", force: true);
        }

        /// <summary>Keeps each tunnel's temperature on the product (MeasuredTemperature is overwritten by the next tunnel).</summary>
        private void RecordStageTemperature(ProductInstance product, float average)
        {
            switch (_config.OutputState)
            {
                case ProductState.BakedPizza:
                    product.BakeTemperatureCelsius = average;
                    break;
                case ProductState.CooledPizza:
                    product.CoolingTemperatureCelsius = average;
                    break;
                case ProductState.FrozenPizza:
                    product.FreezingTemperatureCelsius = average;
                    break;
            }
        }

        // ---- Training fault: heater / cooling unit failure ----

        private bool _heaterFailed;

        /// <summary>True while a simulated heater (oven) or cooling unit (cooling/freezer) failure is active.</summary>
        public bool HasHeaterFailure => _heaterFailed;

        /// <summary>
        /// FaultSystem training case: the heater (or cooling unit) fails. Temperature control switches off,
        /// the zone drifts to ambient, the station reports a deviation (amber) and, once the temperature leaves
        /// the valid range, faults with TemperatureOutOfRange - exactly like a real defect. Only maintenance
        /// repairs it (cleared in OnEnterMaintenance).
        /// </summary>
        public void SimulateHeaterFailure()
        {
            if (!UsesTemperature)
            {
                return;
            }

            _heaterFailed = true;
            if (_temperatureSensor != null)
            {
                _temperatureSensor.SetHeating(false);
            }
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
                _temperatureSensor.SetHeating(isActive && _config.UsesTemperature && !_heaterFailed);
            }
        }

        // ---- HMI terminal values (IMachineParameterSource) ----

        /// <inheritdoc />
        public IReadOnlyList<MachineParameter> Parameters
        {
            get
            {
                if (_parameters == null)
                {
                    BuildHmiValues();
                }
                return _parameters;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<MachineReadout> Readouts
        {
            get
            {
                if (_readouts == null)
                {
                    BuildHmiValues();
                }
                return _readouts;
            }
        }

        private void BuildHmiValues()
        {
            // TODO localization: labels are English fallbacks until keys exist in the table.
            _parameters = new List<MachineParameter>
            {
                new MachineParameter("dwellTime", _config.RecordDwellAsBakeTime ? "Bake time" : "Dwell time", "s",
                    _config.MinDwellSeconds, _config.MaxDwellSeconds, _config.DwellStepSeconds, "0.0",
                    () => _dwellSeconds, SetDwellTime),
            };

            if (_config.UsesTemperature)
            {
                _parameters.Add(new MachineParameter("targetTemperature", "Temperature", "\u00b0C",
                    _config.MinTargetTemperatureCelsius, _config.MaxTargetTemperatureCelsius,
                    _config.TemperatureStepCelsius, "0",
                    () => _targetTemperature, SetTargetTemperature));
            }

            _readouts = new List<MachineReadout>
            {
                new MachineReadout("temperature", "Temperature", "\u00b0C",
                    () => UsesTemperature ? _temperatureSensor.CurrentValue.ToString("0") : "--",
                    EvaluateTemperature, MachineReadoutSlot.Temperature),
                new MachineReadout("cycleTime", "Dwell time", "s",
                    () => _dwellSeconds.ToString("0.0"),
                    () => MachineValueLevel.Normal, MachineReadoutSlot.CycleTime),
                new MachineReadout("setpoint", "Setpoint", "",
                    () => _config.UsesTemperature
                        ? $"{_targetTemperature:0} \u00b0C / {_dwellSeconds:0.#} s"
                        : $"{_dwellSeconds:0.#} s",
                    () => MachineValueLevel.Normal, MachineReadoutSlot.Setpoint),
                new MachineReadout("productsInside", "Products inside", "pcs",
                    () => ProductsInside.ToString(),
                    () => ProductsInside > 0 ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
                new MachineReadout("lastDwell", _config.RecordDwellAsBakeTime ? "Last bake time" : "Last dwell time", "s",
                    () => _lastDwellSeconds >= 0f ? _lastDwellSeconds.ToString("0.0") : "--",
                    EvaluateLastDwell),
                new MachineReadout("lastExposure", "Last product temp.", "\u00b0C",
                    () => float.IsNaN(_lastExposureCelsius) ? "--" : _lastExposureCelsius.ToString("0"),
                    () => float.IsNaN(_lastExposureCelsius) ? MachineValueLevel.Inactive
                        : _lastExposureCelsius < _config.MinValidTemperatureCelsius
                          || _lastExposureCelsius > _config.MaxValidTemperatureCelsius
                            ? MachineValueLevel.Warning : MachineValueLevel.Normal),
                new MachineReadout("completed", "Products completed", "pcs",
                    () => _completedCount.ToString()),
                new MachineReadout("wrongProducts", "Wrong products", "pcs",
                    () => _wrongProductCount.ToString(),
                    () => _wrongProductCount > 0 ? MachineValueLevel.Warning : MachineValueLevel.Normal),
                new MachineReadout("belt", "Belt", "",
                    () => _belt == null ? "--" : _belt.IsMoving ? $"{_belt.CurrentSpeedMetersPerSecond:0.00} m/s" : _belt.CurrentState.ToString(),
                    () => _belt == null ? MachineValueLevel.Inactive
                        : _belt.CurrentState == MachineState.Fault ? MachineValueLevel.Alarm
                        : _belt.IsMoving ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
            };
        }

        private MachineValueLevel EvaluateTemperature()
        {
            if (!UsesTemperature)
            {
                return MachineValueLevel.Inactive;
            }

            float temperature = _temperatureSensor.CurrentValue;
            if (temperature < _config.MinValidTemperatureCelsius || temperature > _config.MaxValidTemperatureCelsius)
            {
                return CurrentState == MachineState.Running ? MachineValueLevel.Alarm : MachineValueLevel.Inactive;
            }

            return Mathf.Abs(temperature - _targetTemperature) > _config.WarningToleranceCelsius
                ? MachineValueLevel.Warning
                : MachineValueLevel.Normal;
        }

        private MachineValueLevel EvaluateLastDwell()
        {
            if (_lastDwellSeconds < 0f)
            {
                return MachineValueLevel.Inactive;
            }

            // More than 10 % off the setpoint (e.g. belt stood still = overbaked) -> amber.
            return Mathf.Abs(_lastDwellSeconds - _dwellSeconds) > _dwellSeconds * 0.1f
                ? MachineValueLevel.Warning
                : MachineValueLevel.Normal;
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
            return string.IsNullOrEmpty(localized) ? LocText.Info("process", info, FallbackText(info)) : localized;
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
            _heaterFailed = false; // maintenance repairs a simulated heater failure
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

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("completed", _completedCount);
            values.Set("wrongProducts", _wrongProductCount);
            values.Set("lastDwell", _lastDwellSeconds);
            values.Set("hasLastExposure", !float.IsNaN(_lastExposureCelsius));
            values.Set("lastExposure", float.IsNaN(_lastExposureCelsius) ? 0f : _lastExposureCelsius);
            values.Set("heaterFailed", _heaterFailed);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            _completedCount = values.GetInt("completed", _completedCount);
            _wrongProductCount = values.GetInt("wrongProducts", _wrongProductCount);
            _lastDwellSeconds = values.GetFloat("lastDwell", _lastDwellSeconds);
            _lastExposureCelsius = values.GetBool("hasLastExposure", false)
                ? values.GetFloat("lastExposure", 0f)
                : float.NaN;

            if (values.GetBool("heaterFailed", false))
            {
                SimulateHeaterFailure();
            }
        }
    }
}
