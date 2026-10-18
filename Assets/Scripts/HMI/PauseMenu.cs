using Game.Core;
using Game.Persistence;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Pause menu (Woche 3, Plattform-UX): P or Escape while no other UI is open pauses the game
    /// (Time.timeScale 0, UI focus) and shows Resume / Save / Load / Controls / Tutorial / Main menu / Quit.
    /// Save and load use the slot chosen in the main menu (<see cref="SaveLoadController.CurrentSlot"/>).
    /// P or Escape again, Q, right click or Resume closes it. With a terminal (HMI) open, Escape first closes the
    /// terminal (UI close request), the next Escape pauses; P closes the terminal and pauses at once. In a fullscreen browser the first Escape is taken by
    /// the browser to leave fullscreen - P always works.
    /// A small hint at the bottom right shows the pause keys (or the close keys while a screen is open).
    ///
    /// The language is chosen in the main menu only (no switch while playing).
    ///
    /// Builds its own overlay canvas from code (no prefab). Needs an EventSystem in the scene (the HMI has one).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private SaveLoadController _saveLoad;

        [Tooltip("Font is copied from this text. Empty = TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [SerializeField] private int _sortingOrder = 500;
        [SerializeField] private float _fontSize = 28f;

        [Tooltip("Scene opened by the main menu button (must be in the build settings).")]
        [SerializeField] private string _mainMenuScene = "MainMenu";

        private Canvas _canvas;
        private GameObject _helpRoot;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _helpText;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _resumeLabel;
        private TextMeshProUGUI _saveLabel;
        private TextMeshProUGUI _loadLabel;
        private TextMeshProUGUI _helpLabel;
        private TextMeshProUGUI _tutorialLabel;
        private TextMeshProUGUI _mainMenuLabel;
        private TextMeshProUGUI _quitLabel;
        private Button _loadButton;
        private Canvas _hintCanvas;
        private TextMeshProUGUI _hint;
        private bool _hintShowsClose;

        private bool _isOpen;
        private bool _wasUiFocusedLastFrame;
        private float _previousTimeScale = 1f;

        public bool IsOpen => _isOpen;

        private TMP_FontAsset Font => _styleSource != null ? _styleSource.font : null;
        private Color PanelColor => _theme != null ? _theme.PanelBackground : new Color(0.09f, 0.11f, 0.13f, 0.96f);
        private Color ButtonColor => _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f, 1f);
        private Color TextColor => _theme != null ? _theme.ValueText : Color.white;
        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f);

        private void Awake()
        {
            if (_saveLoad == null)
            {
                _saveLoad = FindFirstObjectByType<SaveLoadController>();
            }

            Build();
            BuildHint();
            _canvas.gameObject.SetActive(false);
        }

        private void Start()
        {
            // Buttons need an EventSystem. The world-space HMI may work without one (aim raycasts),
            // so create one with the Input System module if the scene has none.
            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem.transform.SetParent(null);
                Debug.Log("[PauseMenu] No EventSystem in scene - created one.", eventSystem);
            }
        }

        private void OnEnable()
        {
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.CloseRequested += Close;
            }
            LocText.TableChanged += HandleTableChanged;
        }

        private void OnDisable()
        {
            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.CloseRequested -= Close;
            }
            LocText.TableChanged -= HandleTableChanged;
        }

        private void OnDestroy()
        {
            // Scene reload (load game) while paused: never leave the game frozen.
            if (_isOpen)
            {
                Time.timeScale = _previousTimeScale;
                if (_uiFocusChannel != null)
                {
                    _uiFocusChannel.PopFocus();
                }
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !(keyboard.pKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
            {
                return;
            }

            // Escape is also the UI close request (input callbacks run before Update): with the menu or a terminal
            // open, that request has already closed it this frame and the UI was still focused last frame -
            // so the same press never reopens anything. The next press pauses.
            if (_isOpen)
            {
                Close();
            }
            else if (!_wasUiFocusedLastFrame)
            {
                Open();
            }
            else if (keyboard.pKey.wasPressedThisFrame && _uiFocusChannel != null)
            {
                // P with a terminal open: close it and pause in one press (Escape only closes the terminal).
                _uiFocusChannel.CloseFocus();
                Open();
            }
        }

        private void LateUpdate()
        {
            _wasUiFocusedLastFrame = _uiFocusChannel != null && _uiFocusChannel.IsUiFocused;
            UpdateHint();
        }

        // ---- Key hint ----

        private void UpdateHint()
        {
            if (_hintCanvas == null)
            {
                return;
            }

            bool otherUiOpen = !_isOpen && _wasUiFocusedLastFrame;
            bool visible = !_isOpen;
            if (_hintCanvas.gameObject.activeSelf != visible)
            {
                _hintCanvas.gameObject.SetActive(visible);
            }

            if (visible && (otherUiOpen != _hintShowsClose || string.IsNullOrEmpty(_hint.text)))
            {
                _hintShowsClose = otherUiOpen;
                RefreshHint();
            }
        }

        private void RefreshHint()
        {
            if (_hint != null)
            {
                _hint.text = _hintShowsClose
                    ? LocText.Get("hmi.close_hint", "Esc / Q / right click – close screen")
                    : LocText.Get("hmi.pause_hint", "P / Esc – pause");
            }
        }

        private void BuildHint()
        {
            var canvasGo = new GameObject("PauseHintCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _hintCanvas = canvasGo.AddComponent<Canvas>();
            _hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _hintCanvas.sortingOrder = _sortingOrder - 1;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // No raycaster: the hint never takes clicks.
            Image back = HmiUiFactory.CreateImage(canvasGo.transform, "Hint", new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.75f));
            var rect = (RectTransform)back.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-20f, 20f);
            var layout = back.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = back.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _hint = HmiUiFactory.CreateText(back.transform, "Text", string.Empty, _fontSize * 0.6f, LabelColor,
                TextAlignmentOptions.Right, Font);
            _hint.textWrappingMode = TextWrappingModes.NoWrap;
            _hint.raycastTarget = false;
        }

        public void Open()
        {
            if (_isOpen)
            {
                return;
            }

            _isOpen = true;
            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            _helpRoot.SetActive(false);
            _status.text = string.Empty;
            RefreshTexts();
            _canvas.gameObject.SetActive(true);

            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.PushFocus();
            }

            // The key press of this frame must not count as "UI was focused" for the next frame's check.
            _wasUiFocusedLastFrame = true;
        }

        public void Close()
        {
            if (!_isOpen)
            {
                return;
            }

            _isOpen = false;
            Time.timeScale = _previousTimeScale;
            _canvas.gameObject.SetActive(false);

            if (_uiFocusChannel != null)
            {
                _uiFocusChannel.PopFocus();
            }
        }

        // ---- Actions ----

        private void SaveGame()
        {
            if (_saveLoad == null)
            {
                return;
            }

            bool saved = _saveLoad.Save();
            _status.text = saved
                ? LocText.Get("hmi.game_saved", "Game saved")
                : LocText.Get("hmi.save_failed", "Saving failed");
            RefreshTexts();
        }

        private void LoadGame()
        {
            if (_saveLoad == null || !_saveLoad.HasSave())
            {
                return;
            }

            // Restore time before the scene reloads (OnDestroy also guards this).
            Time.timeScale = _previousTimeScale;
            if (!_saveLoad.Load())
            {
                Time.timeScale = 0f;
                _status.text = LocText.Get("hmi.load_failed", "Loading failed");
            }
        }

        /// <summary>Back to the main menu. Unsaved progress is lost (the menu shows the saved slots).</summary>
        private void OpenMainMenu()
        {
            if (!Application.CanStreamedLevelBeLoaded(_mainMenuScene))
            {
                _status.text = $"Scene \"{_mainMenuScene}\" is not in the build settings";
                return;
            }

            // Time and UI focus are restored in OnDestroy.
            SceneManager.LoadScene(_mainMenuScene);
        }

        // The string table loads asynchronously - texts of an already open menu follow.
        private void HandleTableChanged()
        {
            if (_isOpen)
            {
                RefreshTexts();
            }
            RefreshHint();
        }

        private void RestartTutorial()
        {
            TutorialController tutorial = FindFirstObjectByType<TutorialController>();
            Close();
            if (tutorial != null)
            {
                tutorial.Restart();
            }
        }

        private void ToggleHelp()
        {
            _helpRoot.SetActive(!_helpRoot.activeSelf);
            RefreshTexts();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- UI ----

        private void Build()
        {
            var canvasGo = new GameObject("PauseMenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = _sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dimmed background blocks clicks into the world UI.
            Image dim = HmiUiFactory.CreateImage(canvasGo.transform, "Dim", new Color(0f, 0f, 0f, 0.6f));
            dim.raycastTarget = true;
            HmiUiFactory.Stretch((RectTransform)dim.transform);

            Image panel = HmiUiFactory.CreateImage(canvasGo.transform, "Panel", PanelColor);
            panel.raycastTarget = true;
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(560f, 0f);
            HmiUiFactory.AddVerticalLayout(panel.gameObject, 12f, 28);
            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = HmiUiFactory.CreateText(panel.transform, "Title", "PAUSED", _fontSize * 1.4f, TextColor,
                TextAlignmentOptions.Center, Font);
            _title.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(_title.gameObject, _fontSize * 2.2f);

            AddButton(panel.transform, "Resume", Close, out _resumeLabel);
            AddButton(panel.transform, "Save", SaveGame, out _saveLabel);
            _loadButton = AddButton(panel.transform, "Load", LoadGame, out _loadLabel);
            AddButton(panel.transform, "Controls", ToggleHelp, out _helpLabel);
            AddButton(panel.transform, "Tutorial", RestartTutorial, out _tutorialLabel);
            AddButton(panel.transform, "MainMenu", OpenMainMenu, out _mainMenuLabel);

            _helpRoot = HmiUiFactory.CreateRect("Help", panel.transform).gameObject;
            HmiUiFactory.AddVerticalLayout(_helpRoot, 0f, 4);
            _helpText = HmiUiFactory.CreateText(_helpRoot.transform, "HelpText", string.Empty, _fontSize * 0.75f, LabelColor,
                TextAlignmentOptions.TopLeft, Font);
            _helpText.textWrappingMode = TextWrappingModes.Normal;
            _helpText.overflowMode = TextOverflowModes.Overflow;
            HmiUiFactory.SetHeight(_helpText.gameObject, _fontSize * 0.75f * 1.35f * 11f);

#if !UNITY_WEBGL || UNITY_EDITOR
            AddButton(panel.transform, "Quit", QuitGame, out _quitLabel);
#endif

            _status = HmiUiFactory.CreateText(panel.transform, "Status", string.Empty, _fontSize * 0.7f, LabelColor,
                TextAlignmentOptions.Center, Font);
            HmiUiFactory.SetHeight(_status.gameObject, _fontSize);
        }

        private Button AddButton(Transform parent, string objectName, UnityEngine.Events.UnityAction onClick,
            out TextMeshProUGUI label)
        {
            Button button = HmiUiFactory.CreateButton(parent, objectName, objectName, _fontSize, ButtonColor, TextColor, Font, out label);
            HmiUiFactory.SetHeight(button.gameObject, _fontSize * 2f);
            button.onClick.AddListener(onClick);
            return button;
        }

        private void RefreshTexts()
        {
            _title.text = LocText.Get("hmi.paused", "PAUSED");
            _resumeLabel.text = $"{LocText.Get("hmi.resume", "Resume")}  <size=70%>(P / Esc)</size>";
            _saveLabel.text = LocText.Get("hmi.save_game", "Save game");
            _loadLabel.text = LocText.Get("hmi.load_game", "Load game");
            _tutorialLabel.text = LocText.Get("hmi.tutorial", "Restart tutorial");
            _helpLabel.text = LocText.Get("hmi.controls", "Controls");
            _mainMenuLabel.text = LocText.Get("menu.main_menu", "Main menu");
            if (_quitLabel != null)
            {
                _quitLabel.text = LocText.Get("hmi.quit", "Quit");
            }

            _loadButton.interactable = _saveLoad != null && _saveLoad.HasSave();
            // {0}/{1}/{2}: quick save, quick load, debug panel - keys differ per platform (see DebugPanel)
            _helpText.text = string.Format(LocText.Get("hmi.controls_text",
                "WASD / arrow keys - move\n" +
                "Mouse - look around\n" +
                "Shift - sprint\n" +
                "E - interact / pick up\n" +
                "Q - put down\n" +
                "Left click - buttons on screens and terminals (up close)\n" +
                "Esc / Q / right click - close screen\n" +
                "P / Esc - pause menu\n" +
                "{0} - quick save, {1} - quick load\n" +
                "{2} - debug panel (development)\n" +
                "R – pallet jack: raise/lower forks\n" +
                "T – pallet jack: ride (Q gets off)\n" +
                "N / B / H – tutorial: next / back / hide"),
                ShortcutHints.QuickSave, ShortcutHints.QuickLoad, ShortcutHints.DebugPanel);
        }
    }
}
