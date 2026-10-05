using System.Collections;
using Game.Core;
using Game.Persistence;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Pause menu (Woche 3, Plattform-UX): Escape while no other UI is open pauses the game
    /// (Time.timeScale 0, UI focus) and shows Resume / Save / Load / Language / Controls / Quit.
    /// Escape again (or Resume) closes it - Escape is routed through <see cref="SO_UiFocusChannel.CloseRequested"/>
    /// like every other UI, so an open HMI is closed first and only the next Escape opens the menu.
    ///
    /// The language button cycles through the available locales and notifies <see cref="_languageChannel"/>
    /// (machine names in the HMI follow it).
    ///
    /// Builds its own overlay canvas from code (no prefab). Needs an EventSystem in the scene (the HMI has one).
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [SerializeField] private SO_UiFocusChannel _uiFocusChannel;
        [SerializeField] private SO_HmiTheme _theme;
        [SerializeField] private SaveLoadController _saveLoad;
        [SerializeField] private SO_LanguageSwitcherChannel _languageChannel;

        [Tooltip("Font is copied from this text. Empty = TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [SerializeField] private int _sortingOrder = 500;
        [SerializeField] private float _fontSize = 28f;

        private Canvas _canvas;
        private GameObject _helpRoot;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _helpText;
        private TextMeshProUGUI _status;
        private TextMeshProUGUI _resumeLabel;
        private TextMeshProUGUI _saveLabel;
        private TextMeshProUGUI _loadLabel;
        private TextMeshProUGUI _helpLabel;
        private TextMeshProUGUI _languageLabel;
        private TextMeshProUGUI _quitLabel;
        private Button _loadButton;

        private bool _isOpen;
        private bool _isSwitchingLanguage;
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
            if (!_isOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !_wasUiFocusedLastFrame)
            {
                Open();
            }
        }

        private void LateUpdate()
        {
            _wasUiFocusedLastFrame = _uiFocusChannel != null && _uiFocusChannel.IsUiFocused;
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

            // Escape of this frame must not count as "UI was focused" for the next frame's check.
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

        private void NextLanguage()
        {
            if (!_isSwitchingLanguage)
            {
                StartCoroutine(SwitchToNextLocale());
            }
        }

        private IEnumerator SwitchToNextLocale()
        {
            _isSwitchingLanguage = true;
            yield return LocalizationSettings.InitializationOperation;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            if (locales.Count > 1)
            {
                int index = locales.IndexOf(LocalizationSettings.SelectedLocale);
                LocalizationSettings.SelectedLocale = locales[(index + 1) % locales.Count];
                if (_languageChannel != null)
                {
                    _languageChannel.NotifyLanguageChanged();
                }
            }

            _isSwitchingLanguage = false;
            RefreshTexts();
        }

        /// <summary>Native name of the selected language, e.g. "Deutsch", "Français".</summary>
        private static string CurrentLanguageName()
        {
            Locale locale = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;
            if (locale == null)
            {
                return "-";
            }

            System.Globalization.CultureInfo culture = locale.Identifier.CultureInfo;
            string name = culture != null ? culture.NativeName : locale.LocaleName;
            return string.IsNullOrEmpty(name) ? locale.Identifier.Code : char.ToUpper(name[0]) + name.Substring(1);
        }

        // Texts arrive asynchronously after a language switch.
        private void HandleTableChanged()
        {
            if (_isOpen)
            {
                RefreshTexts();
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
            AddButton(panel.transform, "Language", NextLanguage, out _languageLabel);
            AddButton(panel.transform, "Controls", ToggleHelp, out _helpLabel);

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
            _resumeLabel.text = LocText.Get("hmi.resume", "Resume");
            _saveLabel.text = LocText.Get("hmi.save_game", "Save game");
            _loadLabel.text = LocText.Get("hmi.load_game", "Load game");
            _languageLabel.text = $"{LocText.Get("hmi.language", "Language")}: {CurrentLanguageName()}";
            _helpLabel.text = LocText.Get("hmi.controls", "Controls");
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
                "Left click - buttons on screens and terminals\n" +
                "Esc - close screen / pause\n" +
                "{0} - quick save, {1} - quick load\n" +
                "{2} - debug panel (development)"),
                ShortcutHints.QuickSave, ShortcutHints.QuickLoad, ShortcutHints.DebugPanel);
        }
    }
}
