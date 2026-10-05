using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Game.Production
{
    /// <summary>Medium a dosing unit applies.</summary>
    public enum DosingMedium
    {
        Sauce,
        Topping
    }

    /// <summary>
    /// Fourth production station (Dosierstation): applies sauce and topping to pizza bases while they
    /// ride through on the station's own belt - continuous, like a waterfall applicator, the product is
    /// never taken off the belt.
    ///
    /// Material flow:
    ///   1. FormedPizza enters the <see cref="_sauceZone"/> under the sauce nozzle -> SaucedPizza,
    ///      the dosed amount (setpoint +- dosing deviation) is taken from the sauce tank and recorded
    ///      in <see cref="ProductInstance.DosedSauceGrams"/>.
    ///   2. SaucedPizza enters the <see cref="_toppingZone"/> under the topping dispenser -> ToppedPizza,
    ///      recorded in <see cref="ProductInstance.DosedToppingGrams"/>.
    ///   The pizza prefab (PizzaStateVisual) follows the state automatically (Sauce / Toppings visuals).
    ///
    /// Reservoirs: sauce tank and topping hopper are consumed per pizza. Below LowLevelWarningPercent
    /// -> warning (HMI amber); not enough for the next pizza -> Fault "SauceEmpty"/"ToppingEmpty",
    /// the belt stops. The operator refills with the refill buttons at the station (allowed in any
    /// state) and clears the fault via the terminal (Acknowledge -> Maintenance -> Complete -> Start).
    ///
    /// Wrong products (e.g. an unpressed dough, or a base that missed the sauce) pass through
    /// unchanged and only raise a short warning - judging them is the QualitySystem's job (Woche 2).
    ///
    /// Upstream belts see this station through <see cref="IsReadyForInfeed"/>: while it is not running,
    /// the belt before it holds the pizzas instead of pushing them onto a stopped belt.
    /// Products are handed over by physics (belts), not through a processor interface.
    /// No RecipeDefinition binding in code: amounts are operator settings from the HMI.
    /// </summary>
    public class DosingMachine : MachineBase, IInfeedReadiness, IMachineParameterSource, ISaveableState
    {
        /// <summary>Content messages this station reports via NotifyContentChanged.</summary>
        public enum DosingInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            SauceApplied,
            ToppingApplied,
            SauceLow,
            ToppingLow,
            SauceEmpty,
            ToppingEmpty,
            SauceRefilled,
            ToppingRefilled,
            WrongProduct,
            BeltFault
        }

        public const string FaultReasonSauceEmpty = "SauceEmpty";
        public const string FaultReasonToppingEmpty = "ToppingEmpty";
        public const string FaultReasonBeltFault = "BeltFault";
        public const string WarningReasonLowLevel = "LowLevel";
        public const string WarningReasonWrongProduct = "WrongProduct";

        private const float WrongProductWarningSeconds = 3f;

        [SerializeField] private SO_DosingConfig _config;

        [Header("Components")]
        [Tooltip("The belt running through the station. Owned by this station: it is started/stopped with it.")]
        [SerializeField] private ConveyorBelt _belt;

        [Tooltip("Trigger volume under the sauce nozzle (FormedPizza -> SaucedPizza).")]
        [SerializeField] private ProcessZone _sauceZone;

        [Tooltip("Trigger volume under the topping dispenser (SaucedPizza -> ToppedPizza).")]
        [SerializeField] private ProcessZone _toppingZone;

        [Header("HMI Content")]
        [SerializeField] private List<Content<DosingInfo>> _contents = new List<Content<DosingInfo>>();

        private float _sauceSetpointGrams;
        private float _toppingSetpointGrams;
        private float _sauceLevelGrams;
        private float _toppingLevelGrams;

        private float _lastSauceGrams = -1f;
        private float _lastToppingGrams = -1f;
        private int _completedCount;
        private int _wrongProductCount;
        private float _wrongProductWarningTimer;

        private DosingInfo? _lastReportedInfo;
        private string _lastReportedDetail;
        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

        /// <summary>Raised when a pizza has received its topping (= left the station as ToppedPizza).</summary>
        public event Action<ProductInstance> ProductDosed;

        public float SauceSetpointGrams => _sauceSetpointGrams;
        public float ToppingSetpointGrams => _toppingSetpointGrams;
        public float SauceLevelGrams => _sauceLevelGrams;
        public float ToppingLevelGrams => _toppingLevelGrams;
        public float SauceLevel01 => _config.SauceTankCapacityGrams > 0f ? _sauceLevelGrams / _config.SauceTankCapacityGrams : 0f;
        public float ToppingLevel01 => _config.ToppingHopperCapacityGrams > 0f ? _toppingLevelGrams / _config.ToppingHopperCapacityGrams : 0f;
        public int CompletedCount => _completedCount;
        public int WrongProductCount => _wrongProductCount;
        public ConveyorBelt Belt => _belt;

        /// <inheritdoc />
        public bool IsReadyForInfeed => CurrentState == MachineState.Running && _belt != null && _belt.IsMoving;

        private bool IsSauceLow => SauceLevel01 * 100f < _config.LowLevelWarningPercent;
        private bool IsToppingLow => ToppingLevel01 * 100f < _config.LowLevelWarningPercent;
        private bool IsSauceInsufficient => _sauceLevelGrams < _sauceSetpointGrams;
        private bool IsToppingInsufficient => _toppingLevelGrams < _toppingSetpointGrams;

        // ---- Unity lifecycle ----

        private void Awake()
        {
            _sauceSetpointGrams = _config.DefaultSauceGrams;
            _toppingSetpointGrams = _config.DefaultToppingGrams;

            float initial = Mathf.Clamp01(_config.InitialFillPercent / 100f);
            _sauceLevelGrams = _config.SauceTankCapacityGrams * initial;
            _toppingLevelGrams = _config.ToppingHopperCapacityGrams * initial;
        }

        private void OnEnable()
        {
            foreach (Content<DosingInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged += content.SetInfo;
                }
            }

            if (_sauceZone != null)
            {
                _sauceZone.ProductEntered += HandleSauceZoneEntered;
            }

            if (_toppingZone != null)
            {
                _toppingZone.ProductEntered += HandleToppingZoneEntered;
            }

            if (_belt != null)
            {
                _belt.StateChanged += HandleBeltStateChanged;
            }
        }

        private void OnDisable()
        {
            foreach (Content<DosingInfo> content in _contents)
            {
                if (!content.InfoKey.IsEmpty)
                {
                    content.InfoKey.StringChanged -= content.SetInfo;
                }
            }

            if (_sauceZone != null)
            {
                _sauceZone.ProductEntered -= HandleSauceZoneEntered;
            }

            if (_toppingZone != null)
            {
                _toppingZone.ProductEntered -= HandleToppingZoneEntered;
            }

            if (_belt != null)
            {
                _belt.StateChanged -= HandleBeltStateChanged;
            }
        }

        private void Update()
        {
            if (_wrongProductWarningTimer > 0f)
            {
                _wrongProductWarningTimer -= Time.deltaTime;
            }

            if (CurrentState != MachineState.Running)
            {
                return;
            }

            // Not enough for the next pizza: stop before a base passes through undosed.
            if (IsSauceInsufficient)
            {
                Report(DosingInfo.SauceEmpty, force: true);
                TriggerFault(FaultReasonSauceEmpty);
                return;
            }

            if (IsToppingInsufficient)
            {
                Report(DosingInfo.ToppingEmpty, force: true);
                TriggerFault(FaultReasonToppingEmpty);
                return;
            }

            UpdateWarning();
        }

        // ---- HMI parameters ----

        /// <summary>Operator setting (recipe terminal: Sauce 80 g).</summary>
        public void SetSauceAmount(float grams)
        {
            _sauceSetpointGrams = Mathf.Clamp(grams, _config.MinSauceGrams, _config.MaxSauceGrams);
        }

        /// <summary>Operator setting (recipe terminal: Belag 120 g).</summary>
        public void SetToppingAmount(float grams)
        {
            _toppingSetpointGrams = Mathf.Clamp(grams, _config.MinToppingGrams, _config.MaxToppingGrams);
        }

        // ---- Reservoirs ----

        /// <summary>
        /// Operator action (refill button at the station): fills the tank/hopper completely. Allowed in
        /// every state - refilling during Maintenance is the normal way out of an "empty" fault.
        /// </summary>
        public bool TryRefill(DosingMedium medium)
        {
            if (medium == DosingMedium.Sauce)
            {
                _sauceLevelGrams = _config.SauceTankCapacityGrams;
                Report(DosingInfo.SauceRefilled, force: true);
            }
            else
            {
                _toppingLevelGrams = _config.ToppingHopperCapacityGrams;
                Report(DosingInfo.ToppingRefilled, force: true);
            }

            if (CurrentState == MachineState.Running)
            {
                UpdateWarning();
            }

            return true;
        }

        // ---- Process ----

        private void HandleSauceZoneEntered(ProductToken token)
        {
            ProductInstance product = token != null ? token.Product : null;
            if (product == null || CurrentState != MachineState.Running)
            {
                return;
            }

            if (product.CurrentState != ProductState.FormedPizza)
            {
                // Already sauced (re-entering) is fine; anything before the press is a wrong product.
                if (product.CurrentState < ProductState.FormedPizza)
                {
                    RegisterWrongProduct(product);
                }
                return;
            }

            float dosed = Dose(_sauceSetpointGrams, _sauceLevelGrams);
            _sauceLevelGrams -= dosed;
            _lastSauceGrams = dosed;

            product.DosedSauceGrams = dosed;
            product.CurrentState = ProductState.SaucedPizza;
            Report(DosingInfo.SauceApplied, $"{dosed:0} g", force: true);
        }

        private void HandleToppingZoneEntered(ProductToken token)
        {
            ProductInstance product = token != null ? token.Product : null;
            if (product == null || CurrentState != MachineState.Running)
            {
                return;
            }

            if (product.CurrentState != ProductState.SaucedPizza)
            {
                // A base without sauce (or a wrong product) passes without topping.
                if (product.CurrentState < ProductState.SaucedPizza)
                {
                    RegisterWrongProduct(product);
                }
                return;
            }

            float dosed = Dose(_toppingSetpointGrams, _toppingLevelGrams);
            _toppingLevelGrams -= dosed;
            _lastToppingGrams = dosed;

            product.DosedToppingGrams = dosed;
            product.CurrentState = ProductState.ToppedPizza;

            _completedCount++;
            ProductDosed?.Invoke(product);
            Report(DosingInfo.ToppingApplied, $"{_lastSauceGrams:0} g / {dosed:0} g", force: true);
        }

        /// <summary>Setpoint with an approximately normal deviation, never more than what is left.</summary>
        private float Dose(float setpoint, float available)
        {
            float sigma = setpoint * _config.DosingDeviationPercent / 100f;
            // Sum of three uniforms ~ normal distribution with standard deviation 'sigma'.
            float noise = (Random.value + Random.value + Random.value - 1.5f) * 2f * sigma;
            return Mathf.Clamp(setpoint + noise, 0f, available);
        }

        private void RegisterWrongProduct(ProductInstance product)
        {
            _wrongProductCount++;
            _wrongProductWarningTimer = WrongProductWarningSeconds;
            Report(DosingInfo.WrongProduct, product.CurrentState.ToString(), force: true);
        }

        private void UpdateWarning()
        {
            if (IsSauceLow || IsToppingLow)
            {
                SetWarning(true, WarningReasonLowLevel);
                Report(IsSauceLow ? DosingInfo.SauceLow : DosingInfo.ToppingLow,
                    IsSauceLow ? $"{SauceLevel01 * 100f:0} %" : $"{ToppingLevel01 * 100f:0} %");
                return;
            }

            SetWarning(_wrongProductWarningTimer > 0f, WarningReasonWrongProduct);
        }

        private void HandleBeltStateChanged(MachineState previous, MachineState next)
        {
            if (next == MachineState.Fault && CurrentState != MachineState.Fault
                && CurrentState != MachineState.Maintenance)
            {
                Report(DosingInfo.BeltFault, force: true);
                TriggerFault(FaultReasonBeltFault);
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
                new MachineParameter("sauceAmount", "Sauce", "g",
                    _config.MinSauceGrams, _config.MaxSauceGrams, _config.SauceStepGrams, "0",
                    () => _sauceSetpointGrams, SetSauceAmount),
                new MachineParameter("toppingAmount", "Topping", "g",
                    _config.MinToppingGrams, _config.MaxToppingGrams, _config.ToppingStepGrams, "0",
                    () => _toppingSetpointGrams, SetToppingAmount),
            };

            _readouts = new List<MachineReadout>
            {
                new MachineReadout("sauceLevel", "Sauce tank", "%",
                    () => (SauceLevel01 * 100f).ToString("0"),
                    () => EvaluateLevel(IsSauceInsufficient, IsSauceLow), MachineReadoutSlot.FillLevel),
                new MachineReadout("setpoint", "Setpoint", "g",
                    () => $"{_sauceSetpointGrams:0} / {_toppingSetpointGrams:0}",
                    () => MachineValueLevel.Normal, MachineReadoutSlot.Setpoint),
                new MachineReadout("sauceTank", "Sauce tank", "kg",
                    () => (_sauceLevelGrams / 1000f).ToString("0.00"),
                    () => EvaluateLevel(IsSauceInsufficient, IsSauceLow)),
                new MachineReadout("toppingHopper", "Topping hopper", "kg",
                    () => (_toppingLevelGrams / 1000f).ToString("0.00"),
                    () => EvaluateLevel(IsToppingInsufficient, IsToppingLow)),
                new MachineReadout("lastSauce", "Last sauce", "g",
                    () => _lastSauceGrams >= 0f ? _lastSauceGrams.ToString("0") : "--",
                    () => EvaluateDose(_lastSauceGrams, _sauceSetpointGrams, _config.SauceToleranceGrams)),
                new MachineReadout("lastTopping", "Last topping", "g",
                    () => _lastToppingGrams >= 0f ? _lastToppingGrams.ToString("0") : "--",
                    () => EvaluateDose(_lastToppingGrams, _toppingSetpointGrams, _config.ToppingToleranceGrams)),
                new MachineReadout("completed", "Pizzas dosed", "pcs",
                    () => _completedCount.ToString()),
                new MachineReadout("wrongProducts", "Passed undosed", "pcs",
                    () => _wrongProductCount.ToString(),
                    () => _wrongProductCount > 0 ? MachineValueLevel.Warning : MachineValueLevel.Normal),
                new MachineReadout("belt", "Belt", "",
                    () => _belt == null ? "--" : _belt.IsMoving ? LocText.Get("hmi.moving", "Moving") : _belt.CurrentState.ToString(),
                    () => _belt == null ? MachineValueLevel.Inactive
                        : _belt.CurrentState == MachineState.Fault ? MachineValueLevel.Alarm
                        : _belt.IsMoving ? MachineValueLevel.Normal : MachineValueLevel.Inactive),
            };
        }

        private static MachineValueLevel EvaluateLevel(bool isInsufficient, bool isLow)
        {
            if (isInsufficient)
            {
                return MachineValueLevel.Alarm;
            }

            return isLow ? MachineValueLevel.Warning : MachineValueLevel.Normal;
        }

        private static MachineValueLevel EvaluateDose(float last, float setpoint, float tolerance)
        {
            if (last < 0f)
            {
                return MachineValueLevel.Inactive;
            }

            return Mathf.Abs(last - setpoint) <= tolerance ? MachineValueLevel.Normal : MachineValueLevel.Warning;
        }

        // ---- Content messages ----

        private void Report(DosingInfo info, string detail = null, bool force = false)
        {
            if (!force && _lastReportedInfo == info && _lastReportedDetail == detail)
            {
                return;
            }

            _lastReportedInfo = info;
            _lastReportedDetail = detail;
            NotifyContentChanged(string.IsNullOrEmpty(detail) ? Info(info) : detail + "\n" + Info(info));
        }

        private string Info(DosingInfo info)
        {
            string localized = _contents.Find(c => c.State == info)?.Info;
            return string.IsNullOrEmpty(localized) ? LocText.Info("dosing", info, FallbackText(info)) : localized;
        }

        /// <summary>Used until the localization keys exist in the table.</summary>
        private static string FallbackText(DosingInfo info) => info switch
        {
            DosingInfo.SauceApplied => "Sauce applied",
            DosingInfo.ToppingApplied => "Pizza topped (sauce / topping)",
            DosingInfo.SauceLow => "Sauce tank low",
            DosingInfo.ToppingLow => "Topping hopper low",
            DosingInfo.SauceEmpty => "Sauce tank empty - refill",
            DosingInfo.ToppingEmpty => "Topping hopper empty - refill",
            DosingInfo.SauceRefilled => "Sauce tank refilled",
            DosingInfo.ToppingRefilled => "Topping hopper refilled",
            DosingInfo.WrongProduct => "Product passed without dosing",
            DosingInfo.BeltFault => "Belt fault",
            _ => info.ToString()
        };

        // ---- State machine hooks ----

        protected override void OnEnterReady() => Report(DosingInfo.Ready, force: true);

        protected override void OnEnterStarting()
        {
            Report(DosingInfo.Starting, force: true);
            RestartRoutine(ref _startingRoutine, StartingRoutine());
        }

        protected override void OnEnterRunning()
        {
            Report(DosingInfo.Running, force: true);
            if (_belt != null)
            {
                _belt.RequestRun();
            }
        }

        protected override void OnEnterStopping()
        {
            Report(DosingInfo.Stopping, force: true);
            SetWarning(false);
            if (_belt != null)
            {
                _belt.StopRun();
            }
            RestartRoutine(ref _stoppingRoutine, StoppingRoutine());
        }

        protected override void OnEnterStopped() => Report(DosingInfo.Stopped, force: true);

        protected override void OnEnterFault(string reason)
        {
            if (reason != FaultReasonSauceEmpty && reason != FaultReasonToppingEmpty && reason != FaultReasonBeltFault)
            {
                Report(DosingInfo.Fault, force: true);
            }

            SetWarning(false);
            StopRoutine(ref _startingRoutine);
            StopRoutine(ref _stoppingRoutine);

            if (_belt != null)
            {
                _belt.StopRun();
            }
        }

        protected override void OnEnterMaintenance() => Report(DosingInfo.Maintenance, force: true);

        private IEnumerator StartingRoutine()
        {
            yield return new WaitForSeconds(_config.StartupDurationSeconds);
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

        private void OnDrawGizmosSelected()
        {
            DrawZoneGizmo(_sauceZone, new Color(0.85f, 0.15f, 0.1f));
            DrawZoneGizmo(_toppingZone, new Color(0.95f, 0.8f, 0.2f));
        }

        private static void DrawZoneGizmo(ProcessZone zone, Color color)
        {
            if (zone == null || !zone.TryGetComponent(out BoxCollider box))
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("sauceLevel", _sauceLevelGrams);
            values.Set("toppingLevel", _toppingLevelGrams);
            values.Set("lastSauce", _lastSauceGrams);
            values.Set("lastTopping", _lastToppingGrams);
            values.Set("completed", _completedCount);
            values.Set("wrongProducts", _wrongProductCount);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            _sauceLevelGrams = Mathf.Clamp(values.GetFloat("sauceLevel", _sauceLevelGrams), 0f, _config.SauceTankCapacityGrams);
            _toppingLevelGrams = Mathf.Clamp(values.GetFloat("toppingLevel", _toppingLevelGrams), 0f, _config.ToppingHopperCapacityGrams);
            _lastSauceGrams = values.GetFloat("lastSauce", _lastSauceGrams);
            _lastToppingGrams = values.GetFloat("lastTopping", _lastToppingGrams);
            _completedCount = values.GetInt("completed", _completedCount);
            _wrongProductCount = values.GetInt("wrongProducts", _wrongProductCount);
        }
    }
}
