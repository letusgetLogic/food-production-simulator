using System.Collections;
using Game.Core;
using Game.Persistence;
using Game.Production;
using Game.Quality;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Guided first batch (Woche 3, Tutorial/Onboarding): shows one instruction at a time in a small overlay
    /// panel (top left) and advances when the player has done it - walk, read the recipe page of the HMI, start the
    /// mixer, tilt the drum, carry the pot to the portioner, raise the lift, first portion, first pizza base,
    /// open the HMI, first packaged pizza, fault drill (empty sauce reservoir: read the alarm, refill, start), palletizer, pallet jack (grab, raise forks), changing the pallet. Content (texts, order, conditions)
    /// lives in <see cref="SO_TutorialSequence"/>.
    ///
    /// Conditions read the real machines (no tutorial hooks in machine code). They also accept "the player is
    /// already further", e.g. a tilted drum completes "wait for mixing", so nobody gets stuck by doing steps early.
    ///
    /// Progress belongs to the save slot (<see cref="ISaveableState"/>): a new game starts the tutorial at step 1,
    /// a loaded game continues it where it was saved (or not at all once finished). <see cref="Restart"/> starts it
    /// again. Keys: previous step (default B - only shows it again, it stays done), N browses forward again up to
    /// the current step; N completes the current step only if it is a Manual (read-only) step - steps with a task
    /// cannot be skipped. Hide/show (default H). The last step stays until the player closes it with N.
    /// Builds its own overlay canvas from code, like <see cref="PauseMenu"/>.
    /// </summary>
    public class TutorialController : MonoBehaviour, ISaveableState
    {
        [SerializeField] private SO_TutorialSequence _sequence;

        [Header("Scene references")]
        [SerializeField] private Transform _player;
        [Tooltip("All dough mixers - any of them completes the mixer steps.")]
        [SerializeField] private MixerMachine[] _mixers = new MixerMachine[0];
        [SerializeField] private PortionerMachine _portioner;
        [SerializeField] private PressMachine _press;
        [SerializeField] private QualityInspector _qualityInspector;
        [Tooltip("Empty = found in the scene at runtime.")]
        [SerializeField] private PalletStation _palletStation;
        [Tooltip("Empty = found in the scene at runtime.")]
        [SerializeField] private PalletJack _palletJack;
        [Tooltip("Fault drill: the station whose first reservoir is emptied. Empty = the drill steps complete by themselves.")]
        [SerializeField] private DosingMachine _faultDrillStation;
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private SO_HoldChannel _holdChannel;

        [Header("Behaviour")]
        [Tooltip("Start automatically with a new game (a loaded game restores the saved progress instead).")]
        [SerializeField] private bool _startAutomatically = true;
        [SerializeField] private Key _nextKey = Key.N;
        [SerializeField] private Key _hideKey = Key.H;
        [SerializeField] private Key _backKey = Key.B;

        [Header("Look")]
        [SerializeField] private SO_HmiTheme _theme;
        [Tooltip("Font is copied from this text. Empty = TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;
        [Tooltip("Below the HMI screen (Canvas_HMI: 10) - an open terminal covers the tutorial panel.")]
        [SerializeField] private int _sortingOrder = 5;
        [SerializeField] private float _fontSize = 22f;

        private Canvas _canvas;
        private TextMeshProUGUI _header;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _footer;
        private Image _progressFill;

        private int _stepIndex = -1;
        /// <summary>Step shown in the panel: the current one, or an earlier (done) one while browsing back.</summary>
        private int _viewIndex = -1;
        private bool _wasRestored;
        private bool _isRunning;
        private bool _isHidden;
        private float _stepTime;

        // Baselines captured when a step starts.
        private Vector3 _startPosition;
        private int _startPortions;
        private int _startFormed;
        private int _startInspected;
        private int _startCartons;
        private bool _uiOpenedSinceStep;
        private bool _drumTiltedSinceStep;
        private bool _drillFaultSeen;

        public bool IsRunning => _isRunning;
        public int StepIndex => _stepIndex;
        public int StepCount => _sequence != null ? _sequence.Steps.Count : 0;

        private TMP_FontAsset Font => _styleSource != null ? _styleSource.font : null;
        private Color PanelColor => _theme != null ? _theme.PanelBackground : new Color(0.09f, 0.11f, 0.13f, 0.92f);
        private Color TextColor => _theme != null ? _theme.ValueText : Color.white;
        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f);
        private static readonly Color Accent = new Color(0.30f, 0.78f, 0.45f);

        private void Awake()
        {
            Build();
            _canvas.gameObject.SetActive(false);
        }

        private IEnumerator Start()
        {
            if (!_startAutomatically)
            {
                yield break;
            }

            // Loading a save: RestoreState decides (running at step X, or finished). A save from before the
            // tutorial was saved has no entry - it starts like a new game.
            while (SaveLoadController.IsLoadPending)
            {
                yield return null;
            }
            yield return null;

            if (!_wasRestored)
            {
                Restart();
            }
        }

        private void OnEnable()
        {
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.FocusChanged += HandleFocusChanged;
            }
            foreach (MixerMachine mixer in _mixers)
            {
                if (mixer != null)
                {
                    mixer.DrumTilted += HandleDrumTilted;
                }
            }
            LocText.TableChanged += RefreshTexts;
        }

        private void OnDisable()
        {
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.FocusChanged -= HandleFocusChanged;
            }
            foreach (MixerMachine mixer in _mixers)
            {
                if (mixer != null)
                {
                    mixer.DrumTilted -= HandleDrumTilted;
                }
            }
            LocText.TableChanged -= RefreshTexts;
        }

        // ---- Public API ----

        /// <summary>Starts the tutorial from the first step (e.g. from a menu).</summary>
        public void Restart()
        {
            if (_sequence == null || _sequence.Steps.Count == 0)
            {
                Debug.LogWarning("[Tutorial] No sequence assigned.", this);
                return;
            }

            StartAt(0);
        }

        private void StartAt(int stepIndex)
        {
            _isRunning = true;
            _isHidden = false;
            _viewIndex = -1;
            _canvas.gameObject.SetActive(true);
            EnterStep(Mathf.Clamp(stepIndex, 0, _sequence.Steps.Count - 1));
        }

        /// <summary>Completes the current step (key, debug, automation).</summary>
        public void NextStep()
        {
            if (!_isRunning)
            {
                return;
            }

            if (_stepIndex + 1 >= _sequence.Steps.Count)
            {
                Finish();
            }
            else
            {
                EnterStep(_stepIndex + 1);
            }
        }

        /// <summary>Ends the tutorial (saved with the game: a loaded save does not show it again).</summary>
        public void Finish()
        {
            _isRunning = false;
            _canvas.gameObject.SetActive(false);
        }

        // ---- Save/Load (ISaveableState) ----

        /// <inheritdoc />
        public void CaptureState(SaveValues values)
        {
            values.Set("running", _isRunning);
            values.Set("step", _stepIndex);
            values.Set("hidden", _isHidden);
        }

        /// <inheritdoc />
        public void RestoreState(SaveValues values)
        {
            _wasRestored = true;
            if (!values.GetBool("running", false) || _sequence == null || _sequence.Steps.Count == 0)
            {
                Finish();
                return;
            }

            // The step starts over (baselines are taken now) - conditions accept "already further", see class summary.
            StartAt(values.GetInt("step", 0));
            if (values.GetBool("hidden", false))
            {
                _isHidden = true;
                _canvas.gameObject.SetActive(false);
            }
        }

        // ---- Loop ----

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            // Tutorial keys also work while a terminal has the UI focus (the steps often ask the player to read
            // something in the HMI, N/B/H are not used by any screen) - but not in the pause menu.
            bool paused = Time.timeScale <= 0f; // pause menu open - the tutorial waits
            if (keyboard != null && !paused)
            {
                if (keyboard[_hideKey].wasPressedThisFrame)
                {
                    _isHidden = !_isHidden;
                    _canvas.gameObject.SetActive(!_isHidden);
                }
                else if (_isHidden)
                {
                    // Browsing a hidden panel would change it unseen.
                }
                else if (keyboard[_backKey].wasPressedThisFrame)
                {
                    ShowStep(_viewIndex - 1);
                }
                else if (keyboard[_nextKey].wasPressedThisFrame)
                {
                    if (_viewIndex < _stepIndex)
                    {
                        ShowStep(_viewIndex + 1);
                    }
                    else if (_sequence.Steps[_stepIndex].Condition == TutorialCondition.Manual)
                    {
                        // Only read-only steps (and the last one) are confirmed with N - a step with a task
                        // completes when the player has done it; later steps stay locked until then.
                        NextStep();
                        return;
                    }
                }
            }

            _stepTime += Time.unscaledDeltaTime;
            if (_faultDrillStation != null && _faultDrillStation.CurrentState == MachineState.Fault)
            {
                _drillFaultSeen = true;
            }

            if (IsStepDone(_sequence.Steps[_stepIndex]))
            {
                NextStep();
            }
        }

        /// <summary>Shows an earlier step again (browsing) - progress is unchanged.</summary>
        private void ShowStep(int index)
        {
            int clamped = Mathf.Clamp(index, 0, _stepIndex);
            if (clamped != _viewIndex)
            {
                _viewIndex = clamped;
                RefreshTexts();
            }
        }

        private void EnterStep(int index)
        {
            // Browsing stays where it is; whoever looks at the current step follows the progress.
            bool following = _viewIndex < 0 || _viewIndex >= _stepIndex;
            _stepIndex = index;
            if (following)
            {
                _viewIndex = index;
            }
            _stepTime = 0f;
            _startPosition = _player != null ? _player.position : Vector3.zero;
            _startPortions = _portioner != null ? _portioner.PortionsProduced : 0;
            _startFormed = _press != null ? _press.FormedCount : 0;
            _startInspected = _qualityInspector != null ? _qualityInspector.TotalCount : 0;
            _startCartons = PalletStationRef != null ? PalletStationRef.CartonsTotal : 0;
            _uiOpenedSinceStep = false;
            _drumTiltedSinceStep = false;
            if (_sequence.Steps[index].Condition == TutorialCondition.FaultDrillAlarm)
            {
                StartFaultDrill();
            }
            RefreshTexts();
        }

        /// <summary>Empties the first reservoir of the drill station - it faults with IngredientEmpty by itself.</summary>
        private void StartFaultDrill()
        {
            _drillFaultSeen = false;
            if (_faultDrillStation == null || DrillUnit == null)
            {
                return;
            }

            MachineState state = _faultDrillStation.CurrentState;
            _drillFaultSeen = state == MachineState.Fault || state == MachineState.Maintenance;
            _faultDrillStation.DrainReservoir(DrillUnit);
        }

        private DosingUnit DrillUnit =>
            _faultDrillStation != null && _faultDrillStation.Units.Count > 0 ? _faultDrillStation.Units[0] : null;

        private bool IsStepDone(SO_TutorialSequence.Step step)
        {
            switch (step.Condition)
            {
                case TutorialCondition.Manual:
                    return step.Amount > 0f && _stepTime >= step.Amount;

                case TutorialCondition.PlayerMoved:
                    if (_player == null) return true;
                    Vector3 moved = _player.position - _startPosition;
                    moved.y = 0f;
                    return moved.magnitude >= Mathf.Max(0.5f, step.Amount);

                case TutorialCondition.UiOpened:
                    return _uiOpenedSinceStep;

                case TutorialCondition.MixerRunning:
                    return !HasMixers || _drumTiltedSinceStep
                           || AnyMixer(m => m.CurrentState == MachineState.Running || m.IsMixingComplete || m.IsTilted);

                case TutorialCondition.MixingComplete:
                    return !HasMixers || _drumTiltedSinceStep || AnyMixer(m => m.IsMixingComplete || m.IsTilted);

                case TutorialCondition.DrumTilted:
                    return !HasMixers || _drumTiltedSinceStep || AnyMixer(m => m.IsTilted);

                case TutorialCondition.HoldingObject:
                    return (_holdChannel != null && _holdChannel.HoldingObject != null) || IsPotOnLift();

                case TutorialCondition.PotOnLift:
                    return IsPotOnLift() || HasHopperContent();

                case TutorialCondition.HopperFilled:
                    return HasHopperContent() || (_portioner != null && _portioner.PortionsProduced > _startPortions);

                case TutorialCondition.PortionProduced:
                    return _portioner == null || _portioner.PortionsProduced > _startPortions;

                case TutorialCondition.PizzaFormed:
                    return _press == null || _press.FormedCount > _startFormed;

                case TutorialCondition.ProductInspected:
                    return _qualityInspector == null || _qualityInspector.TotalCount > _startInspected;

                case TutorialCondition.RecipeOpened:
                    return IsRecipeShown();

                case TutorialCondition.CartonPalletized:
                    return PalletStationRef == null
                           || PalletStationRef.CartonsTotal > _startCartons
                           || PalletStationRef.PalletsCompleted > 0
                           || (PalletStationRef.CurrentPallet != null && !PalletStationRef.CurrentPallet.IsEmpty);

                case TutorialCondition.PalletJackHeld:
                    return PalletJackRef == null || PalletJackRef.IsHeld || PalletJackRef.IsRaised;

                case TutorialCondition.PalletJackRaised:
                    return PalletJackRef == null || PalletJackRef.IsRaised;

                case TutorialCondition.FaultDrillAlarm:
                    return _faultDrillStation == null || DrillUnit == null
                           || (_drillFaultSeen && (_uiOpenedSinceStep || IsUiFocused
                                                   || _faultDrillStation.CurrentState != MachineState.Fault));

                case TutorialCondition.FaultDrillRefilled:
                    return _faultDrillStation == null
                           || (_faultDrillStation.CurrentState != MachineState.Fault
                               && _faultDrillStation.CurrentState != MachineState.Maintenance
                               && (DrillUnit == null || !DrillUnit.IsInsufficient));

                case TutorialCondition.FaultDrillResolved:
                    return _faultDrillStation == null
                           || (_faultDrillStation.CurrentState == MachineState.Running && (DrillUnit == null || !DrillUnit.IsInsufficient));

                default:
                    return false;
            }
        }

        private bool _palletRefsSearched;

        private PalletStation PalletStationRef
        {
            get
            {
                FindPalletRefs();
                return _palletStation;
            }
        }

        private PalletJack PalletJackRef
        {
            get
            {
                FindPalletRefs();
                return _palletJack;
            }
        }

        /// <summary>Scene without palletizer/pallet jack: the steps complete by themselves.</summary>
        private void FindPalletRefs()
        {
            if (_palletRefsSearched)
            {
                return;
            }
            _palletRefsSearched = true;
            if (_palletStation == null)
            {
                _palletStation = FindFirstObjectByType<PalletStation>();
            }
            if (_palletJack == null)
            {
                _palletJack = FindFirstObjectByType<PalletJack>();
            }
        }

        /// <summary>A terminal (or other UI) is open right now - e.g. it was already open when the step started.</summary>
        private bool IsUiFocused => _uiFocusChannel != null && _uiFocusChannel.IsUiFocused;

        private bool HasMixers => _mixers != null && System.Array.Exists(_mixers, m => m != null);

        private bool AnyMixer(System.Predicate<MixerMachine> predicate) =>
            _mixers != null && System.Array.Exists(_mixers, m => m != null && predicate(m));

        private HmiScreenController _screen;
        private RecipeTerminalPanel _recipePanel;

        /// <summary>HMI open on its recipe page (any terminal, nav rail "Recipe"). No HMI in the scene = done.</summary>
        private bool IsRecipeShown()
        {
            if (_screen == null)
            {
                _screen = FindFirstObjectByType<HmiScreenController>();
                if (_screen == null)
                {
                    return true;
                }
                _recipePanel = _screen.GetPanel<RecipeTerminalPanel>();
            }

            return _recipePanel == null || (_screen.IsOpen && _recipePanel.IsVisible);
        }

        private bool IsPotOnLift() => _portioner != null && _portioner.IsPotLoaded;
        private bool HasHopperContent() => _portioner != null && _portioner.HopperHasContent;

        private void HandleFocusChanged(bool focused)
        {
            if (focused)
            {
                _uiOpenedSinceStep = true;
            }
        }

        private void HandleDrumTilted() => _drumTiltedSinceStep = true;

        // ---- UI ----

        private void RefreshTexts()
        {
            if (!_isRunning || _sequence == null || _stepIndex < 0 || _stepIndex >= _sequence.Steps.Count)
            {
                return;
            }

            int shown = Mathf.Clamp(_viewIndex, 0, _stepIndex);
            bool reviewing = shown < _stepIndex;
            bool lastStep = !reviewing && _stepIndex == _sequence.Steps.Count - 1;
            bool manual = !reviewing && _sequence.Steps[_stepIndex].Condition == TutorialCondition.Manual;
            SO_TutorialSequence.Step step = _sequence.Steps[shown];
            object[] keys = { KeyName(Key.E), KeyName(Key.Q), KeyName(_nextKey), KeyName(_hideKey), KeyName(_backKey) };

            string header = $"{LocText.Get("tutorial.header", "TUTORIAL").ToUpperInvariant()}  {shown + 1}/{_sequence.Steps.Count}";
            if (reviewing)
            {
                header += "  ·  " + LocText.Get("tutorial.step_done", "Done").ToUpperInvariant();
            }
            _header.text = header;
            _title.text = Format(LocText.Get(step.TitleKey, step.TitleFallback), keys);
            _body.text = Format(LocText.Get(step.TextKey, step.TextFallback), keys);

            string footer = reviewing
                ? LocText.Get("tutorial.footer_review", "[{4}] back   [{2}] forward   [{3}] hide")
                : lastStep
                    ? LocText.Get("tutorial.footer_last", "[{4}] back   [{2}] close tutorial   [{3}] hide")
                    : manual
                        ? LocText.Get("tutorial.footer_manual", "[{4}] back   [{2}] next step   [{3}] hide")
                        : LocText.Get("tutorial.footer", "[{4}] back   [{3}] hide");
            _footer.text = Format(footer, keys);

            // Earlier steps are greyed: they are done, the panel only shows them again.
            _title.color = reviewing ? LabelColor : TextColor;
            _body.color = reviewing ? LabelColor : TextColor;

            var fill = (RectTransform)_progressFill.transform;
            fill.anchorMax = new Vector2((_stepIndex + 1f) / _sequence.Steps.Count, 1f);
        }

        private static string Format(string text, params object[] args)
        {
            try
            {
                return string.Format(text ?? string.Empty, args);
            }
            catch (System.FormatException)
            {
                return text;
            }
        }

        private static string KeyName(Key key)
        {
            Keyboard keyboard = Keyboard.current;
            string name = keyboard != null ? keyboard[key].displayName : null;
            return string.IsNullOrEmpty(name) ? key.ToString() : name.ToUpperInvariant();
        }

        private void Build()
        {
            var canvasGo = new GameObject("TutorialCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // No GraphicRaycaster: the panel never takes clicks away from the world or the HMI.
            Image panel = HmiUiFactory.CreateImage(canvasGo.transform, "Panel", PanelColor);
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -24f);
            panelRect.sizeDelta = new Vector2(540f, 0f);
            HmiUiFactory.AddVerticalLayout(panel.gameObject, 6f, 18);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _header = HmiUiFactory.CreateText(panel.transform, "Header", "TUTORIAL", _fontSize * 0.7f, Accent,
                TextAlignmentOptions.Left, Font);
            _header.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(_header.gameObject, _fontSize * 1.0f);

            Image track = HmiUiFactory.CreateImage(panel.transform, "Progress", new Color(1f, 1f, 1f, 0.12f));
            HmiUiFactory.SetHeight(track.gameObject, 4f);
            _progressFill = HmiUiFactory.CreateImage(track.transform, "Fill", Accent);
            var fillRect = (RectTransform)_progressFill.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;

            _title = HmiUiFactory.CreateText(panel.transform, "Title", string.Empty, _fontSize * 1.15f, TextColor,
                TextAlignmentOptions.Left, Font);
            _title.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(_title.gameObject, _fontSize * 1.6f);

            _body = HmiUiFactory.CreateText(panel.transform, "Text", string.Empty, _fontSize, TextColor,
                TextAlignmentOptions.TopLeft, Font);
            _body.textWrappingMode = TextWrappingModes.Normal;
            _body.overflowMode = TextOverflowModes.Overflow; // height = preferred text height (layout group controls it)

            _footer = HmiUiFactory.CreateText(panel.transform, "Footer", string.Empty, _fontSize * 0.7f, LabelColor,
                TextAlignmentOptions.Left, Font);
            HmiUiFactory.SetHeight(_footer.gameObject, _fontSize * 1.1f);
        }
    }
}
