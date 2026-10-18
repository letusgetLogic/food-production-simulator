using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Core;
using Random = UnityEngine.Random;

namespace Game.Production
{
    /// <summary>
    /// Dosing station (Dosierstation), modular: applies the ingredients of its <see cref="DosingUnit"/>s to
    /// pre-baked, cooled pizza bases while they ride through on the station's own belt - continuous, the
    /// product is never taken off the belt. Line order: press -> oven (pre-bake) -> cooling -> dosing -> freezer.
    ///
    /// Units (dispensers) are the child <see cref="DosingUnit"/>s in hierarchy order, e.g. tomato sauce, then
    /// mozzarella. Adding or replacing an ingredient needs no code: add/duplicate a unit, assign an
    /// <see cref="SO_DosingIngredient"/>. Inactive units are skipped.
    ///
    /// Material flow (per unit, when a product enters the zone under its round head):
    ///   - the first unit takes a CooledPizza (pre-baked, cooled base), later units take a SaucedPizza that has
    ///     received all ingredients of the previous units;
    ///   - the dose (setpoint +- dosing deviation) is taken from the unit's reservoir and recorded on the
    ///     product (<see cref="ProductInstance.SetDose"/>, by ingredient id);
    ///   - state: SaucedPizza while dosing is in progress, ToppedPizza after the last unit.
    ///   The pizza prefab shows each dosed ingredient (PizzaIngredientVisual).
    ///
    /// Reservoirs: below LowLevelWarningPercent -> warning (HMI amber); not enough for the next pizza ->
    /// Fault "IngredientEmpty:&lt;id&gt;", the belt stops. The operator refills with the unit's refill button
    /// (allowed in any state) and clears the fault via the terminal (Acknowledge -> Maintenance -> Complete -> Start).
    ///
    /// Wrong products (e.g. a base that was not pre-baked/cooled, or one that missed a dispenser) pass through
    /// unchanged and only raise a short warning - judging them is the QualitySystem's job.
    ///
    /// Upstream belts see this station through <see cref="IsReadyForInfeed"/>: while it is not running,
    /// the belt before it holds the pizzas instead of pushing them onto a stopped belt.
    /// No RecipeDefinition binding in code: amounts are operator settings from the HMI.
    /// </summary>
    public class DosingMachine : MachineBase, IInfeedReadiness, IDownstreamLink, IMachineParameterSource, ISaveableState
    {
        /// <summary>Content messages this station reports via NotifyContentChanged (detail = ingredient).</summary>
        public enum DosingInfo
        {
            Ready,
            Starting,
            Running,
            Stopping,
            Stopped,
            Fault,
            Maintenance,
            IngredientApplied,
            DosingComplete,
            ReservoirLow,
            ReservoirEmpty,
            ReservoirRefilled,
            WrongProduct,
            BeltFault
        }

        /// <summary>Fault code prefix; the full reason is "IngredientEmpty:&lt;ingredient id&gt;".</summary>
        public const string FaultReasonIngredientEmpty = "IngredientEmpty";
        public const string FaultReasonBeltFault = "BeltFault";
        public const string WarningReasonLowLevel = "LowLevel";
        public const string WarningReasonWrongProduct = "WrongProduct";

        private const float WrongProductWarningSeconds = 3f;

        [SerializeField] private SO_DosingConfig _config;

        [Header("Components")]
        [Tooltip("The belt running through the station. Owned by this station: it is started/stopped with it.")]
        [SerializeField] private ConveyorBelt _belt;

        [Header("HMI Content")]
        [SerializeField] private List<Content<DosingInfo>> _contents = new List<Content<DosingInfo>>();

        private readonly List<DosingUnit> _units = new List<DosingUnit>();

        private int _completedCount;
        private int _wrongProductCount;
        private float _wrongProductWarningTimer;

        private DosingInfo? _lastReportedInfo;
        private string _lastReportedDetail;
        private Coroutine _startingRoutine;
        private Coroutine _stoppingRoutine;

        private List<MachineParameter> _parameters;
        private List<MachineReadout> _readouts;

        /// <summary>Raised when a pizza has received its last ingredient (= leaves the station as ToppedPizza).</summary>
        public event Action<ProductInstance> ProductDosed;

        /// <summary>Dispensers in dosing order.</summary>
        public IReadOnlyList<DosingUnit> Units => _units;
        public int CompletedCount => _completedCount;
        public int WrongProductCount => _wrongProductCount;
        public ConveyorBelt Belt => _belt;

        /// <inheritdoc />
        public bool IsReadyForInfeed => CurrentState == MachineState.Running && _belt != null && _belt.IsMoving;

        /// <summary>
        /// Wired by ConveyorLineController: the station's own belt hands its products to the next element
        /// of the line, so the station order is defined only by the controller's element list.
        /// </summary>
        public void SetDownstream(MachineBase downstream)
        {
            if (_belt != null)
            {
                _belt.SetDownstream(downstream);
            }
        }

        /// <summary>Unit for an ingredient id, or null.</summary>
        public DosingUnit FindUnit(string ingredientId) => _units.Find(u => u.IngredientId == ingredientId);

        // ---- Unity lifecycle ----

        private void Awake()
        {
            _units.Clear();
            _units.AddRange(GetComponentsInChildren<DosingUnit>());
            if (_units.Count == 0)
            {
                Debug.LogWarning($"{name}: dosing station without DosingUnit children - nothing is dosed.", this);
            }

            foreach (DosingUnit unit in _units)
            {
                unit.Initialize(_config.InitialFillPercent);
            }
        }

        private void OnEnable()
        {
            foreach (DosingUnit unit in _units)
            {
                unit.ProductEntered += HandleUnitProductEntered;
            }

            if (_belt != null)
            {
                _belt.StateChanged += HandleBeltStateChanged;
            }
        }

        private void OnDisable()
        {
            foreach (DosingUnit unit in _units)
            {
                unit.ProductEntered -= HandleUnitProductEntered;
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
            DosingUnit empty = _units.Find(u => u.IsInsufficient);
            if (empty != null)
            {
                Report(DosingInfo.ReservoirEmpty, empty.DisplayName, force: true);
                TriggerFault($"{FaultReasonIngredientEmpty}:{empty.IngredientId}");
                return;
            }

            UpdateWarning();
        }

        // ---- Reservoirs ----

        /// <summary>
        /// Operator action (refill button at the unit): fills the reservoir completely. Allowed in every
        /// state - refilling during Maintenance is the normal way out of an "empty" fault.
        /// </summary>
        public bool TryRefill(DosingUnit unit)
        {
            if (unit == null || !_units.Contains(unit))
            {
                return false;
            }

            unit.Refill();
            Report(DosingInfo.ReservoirRefilled, unit.DisplayName, force: true);
            ClearEmptyFaultIfRefilled();

            if (CurrentState == MachineState.Running)
            {
                UpdateWarning();
            }

            return true;
        }

        /// <summary>
        /// Training (tutorial fault drill): empties a reservoir. A running station then stops by itself with
        /// IngredientEmpty, exactly like a real empty tank; a station that is not running gets the fault directly.
        /// </summary>
        public bool DrainReservoir(DosingUnit unit)
        {
            if (unit == null || !_units.Contains(unit))
            {
                return false;
            }

            unit.Restore(0f, unit.LastDoseGrams);
            if (CurrentState != MachineState.Running && CurrentState != MachineState.Fault
                && CurrentState != MachineState.Maintenance)
            {
                Report(DosingInfo.ReservoirEmpty, unit.DisplayName, force: true);
                TriggerFault($"{FaultReasonIngredientEmpty}:{unit.IngredientId}");
            }
            return true;
        }

        /// <summary>
        /// Refilling is the operator's remedy for an empty reservoir: once no reservoir is short any more, an
        /// IngredientEmpty fault (or the maintenance after it) is cleared and the station is Ready - the operator
        /// only has to press Start (I). Not while an emergency stop is engaged (MachineBase blocks it).
        /// </summary>
        private void ClearEmptyFaultIfRefilled()
        {
            bool emptyFault = FaultReason != null && FaultReason.StartsWith(FaultReasonIngredientEmpty, StringComparison.Ordinal);
            if (!emptyFault || _units.Exists(u => u.IsInsufficient))
            {
                return;
            }

            if (CurrentState == MachineState.Fault)
            {
                AcknowledgeFault();
            }
            if (CurrentState == MachineState.Maintenance)
            {
                CompleteMaintenance();
            }
        }

        /// <summary>Refills the reservoir of an ingredient (debug panel, tests).</summary>
        public bool TryRefill(string ingredientId) => TryRefill(FindUnit(ingredientId));

        /// <summary>Refills all reservoirs.</summary>
        public void RefillAll()
        {
            foreach (DosingUnit unit in _units)
            {
                TryRefill(unit);
            }
        }

        // ---- Process ----

        private void HandleUnitProductEntered(DosingUnit unit, ProductToken token)
        {
            ProductInstance product = token != null ? token.Product : null;
            if (product == null || CurrentState != MachineState.Running || product.HasDose(unit.IngredientId))
            {
                return; // already dosed by this unit (re-entering) or station not running
            }

            if (product.CurrentState >= ProductState.ToppedPizza)
            {
                return; // finished product passing by
            }

            int index = _units.IndexOf(unit);
            bool previousDone = _units.Take(index).All(u => product.HasDose(u.IngredientId));
            bool stateOk = index == 0
                ? product.CurrentState == ProductState.CooledPizza || product.CurrentState == ProductState.SaucedPizza
                : product.CurrentState == ProductState.SaucedPizza;

            if (!previousDone || !stateOk)
            {
                RegisterWrongProduct(product);
                return;
            }

            float dosed = Dose(unit.SetpointGrams, unit.LevelGrams);
            unit.Take(dosed);
            product.SetDose(unit.IngredientId, dosed);

            bool last = index == _units.Count - 1;
            product.CurrentState = last ? ProductState.ToppedPizza : ProductState.SaucedPizza;
            Report(DosingInfo.IngredientApplied, $"{unit.DisplayName} {dosed:0} g", force: true);

            if (last)
            {
                _completedCount++;
                ProductDosed?.Invoke(product);
                Report(DosingInfo.DosingComplete,
                    string.Join(" / ", _units.Select(u => $"{u.LastDoseGrams:0} g")), force: true);
            }
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
            DosingUnit low = _units.Find(u => u.IsLow(_config.LowLevelWarningPercent));
            if (low != null)
            {
                SetWarning(true, WarningReasonLowLevel);
                Report(DosingInfo.ReservoirLow, $"{low.DisplayName} {low.Level01 * 100f:0} %");
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

        /// <summary>
        /// Built from the units: one setpoint per ingredient, reservoir and last dose per ingredient.
        /// Labels are English texts; their keys are derived (hmi.mozzarella, hmi.mozzarella_reservoir, hmi.last_mozzarella).
        /// </summary>
        private void BuildHmiValues()
        {
            _parameters = new List<MachineParameter>();
            foreach (DosingUnit unit in _units)
            {
                SO_DosingIngredient ingredient = unit.Ingredient;
                if (ingredient == null)
                {
                    continue;
                }

                DosingUnit captured = unit;
                _parameters.Add(new MachineParameter($"{ingredient.Id}Amount", ingredient.DisplayNameFallback, "g",
                    ingredient.MinGrams, ingredient.MaxGrams, ingredient.StepGrams, "0",
                    () => captured.SetpointGrams, captured.SetSetpoint));
            }

            _readouts = new List<MachineReadout>
            {
                new MachineReadout("lowestReservoir", "Lowest reservoir", "%",
                    () => _units.Count > 0 ? (_units.Min(u => u.Level01) * 100f).ToString("0") : "--",
                    EvaluateLowestLevel, MachineReadoutSlot.FillLevel),
                new MachineReadout("setpoint", "Setpoint", "g",
                    () => string.Join(" / ", _units.Select(u => u.SetpointGrams.ToString("0"))),
                    () => MachineValueLevel.Normal, MachineReadoutSlot.Setpoint),
            };

            foreach (DosingUnit unit in _units)
            {
                DosingUnit captured = unit;
                string name = unit.DisplayNameFallback;
                float tolerance = unit.Ingredient != null ? unit.Ingredient.ToleranceGrams : 0f;
                _readouts.Add(new MachineReadout($"{unit.IngredientId}Reservoir", $"{name} reservoir", "kg",
                    () => (captured.LevelGrams / 1000f).ToString("0.00"),
                    () => EvaluateLevel(captured)));
                _readouts.Add(new MachineReadout($"{unit.IngredientId}Last", $"Last {name}", "g",
                    () => captured.LastDoseGrams >= 0f ? captured.LastDoseGrams.ToString("0") : "--",
                    () => EvaluateDose(captured.LastDoseGrams, captured.SetpointGrams, tolerance)));
            }

            _readouts.Add(new MachineReadout("completed", "Pizzas dosed", "pcs",
                () => _completedCount.ToString()));
            _readouts.Add(new MachineReadout("wrongProducts", "Passed undosed", "pcs",
                () => _wrongProductCount.ToString(),
                () => _wrongProductCount > 0 ? MachineValueLevel.Warning : MachineValueLevel.Normal));
            _readouts.Add(new MachineReadout("belt", "Belt", "",
                () => _belt == null ? "--" : _belt.IsMoving ? LocText.Get("hmi.moving", "Moving") : _belt.CurrentState.ToString(),
                () => _belt == null ? MachineValueLevel.Inactive
                    : _belt.CurrentState == MachineState.Fault ? MachineValueLevel.Alarm
                    : _belt.IsMoving ? MachineValueLevel.Normal : MachineValueLevel.Inactive));
        }

        private MachineValueLevel EvaluateLevel(DosingUnit unit)
        {
            if (unit.IsInsufficient)
            {
                return MachineValueLevel.Alarm;
            }

            return unit.IsLow(_config.LowLevelWarningPercent) ? MachineValueLevel.Warning : MachineValueLevel.Normal;
        }

        private MachineValueLevel EvaluateLowestLevel()
        {
            MachineValueLevel worst = MachineValueLevel.Normal;
            foreach (DosingUnit unit in _units)
            {
                MachineValueLevel level = EvaluateLevel(unit);
                if (level == MachineValueLevel.Alarm)
                {
                    return level;
                }

                if (level == MachineValueLevel.Warning)
                {
                    worst = level;
                }
            }
            return worst;
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
            DosingInfo.IngredientApplied => "Ingredient applied",
            DosingInfo.DosingComplete => "Pizza topped",
            DosingInfo.ReservoirLow => "Reservoir low",
            DosingInfo.ReservoirEmpty => "Reservoir empty - refill",
            DosingInfo.ReservoirRefilled => "Reservoir refilled",
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
            bool ownReport = reason != null
                && (reason.StartsWith(FaultReasonIngredientEmpty, StringComparison.Ordinal) || reason == FaultReasonBeltFault);
            if (!ownReport)
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
            foreach (DosingUnit unit in GetComponentsInChildren<DosingUnit>())
            {
                if (unit.Zone == null || !unit.Zone.TryGetComponent(out BoxCollider box))
                {
                    continue;
                }

                Gizmos.color = new Color(0.95f, 0.6f, 0.2f);
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            foreach (DosingUnit unit in _units)
            {
                values.Set($"{unit.IngredientId}.level", unit.LevelGrams);
                values.Set($"{unit.IngredientId}.last", unit.LastDoseGrams);
            }
            values.Set("completed", _completedCount);
            values.Set("wrongProducts", _wrongProductCount);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            foreach (DosingUnit unit in _units)
            {
                unit.Restore(values.GetFloat($"{unit.IngredientId}.level", unit.LevelGrams),
                    values.GetFloat($"{unit.IngredientId}.last", unit.LastDoseGrams));
            }
            _completedCount = values.GetInt("completed", _completedCount);
            _wrongProductCount = values.GetInt("wrongProducts", _wrongProductCount);
        }
    }
}
