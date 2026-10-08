using System;
using System.Collections;
using System.Globalization;
using Game.Persistence;
using Game.Production;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.HMI
{
    /// <summary>
    /// Main menu (scene "MainMenu", first in the build settings):
    ///  - three save slots (<see cref="SaveLoadController.Slots"/>): load, new game (asks before overwriting an
    ///    existing save), delete (asks). The chosen slot becomes <see cref="SaveLoadController.CurrentSlot"/> -
    ///    save/load in the pause menu and the quick keys use it.
    ///  - language: the only place to switch it. Stored in PlayerPrefs and applied at start.
    ///  - quit (not in WebGL).
    ///
    /// Builds its own overlay canvas from code (like <see cref="PauseMenu"/>), creates an EventSystem if needed.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public const string LocalePrefsKey = "fps.locale";

        [Tooltip("Scene started by \"New game\" (must be in the build settings).")]
        [SerializeField] private string _gameScene = "Factory_Prototyp";

        [SerializeField] private SO_HmiTheme _theme;

        [Tooltip("Font is copied from this text. Empty = TMP default font.")]
        [SerializeField] private TextMeshProUGUI _styleSource;

        [SerializeField] private float _fontSize = 28f;

        private sealed class SlotRow
        {
            public string Slot;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Info;
            public Button Load;
            public TextMeshProUGUI LoadLabel;
            public TextMeshProUGUI NewLabel;
            public Button Delete;
            public TextMeshProUGUI DeleteLabel;
        }

        private readonly SlotRow[] _rows = new SlotRow[SaveLoadController.Slots.Length];
        private ISaveStorage _storage;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _languageLabel;
        private TextMeshProUGUI _quitLabel;
        private TextMeshProUGUI _status;
        private GameObject _confirmRoot;
        private TextMeshProUGUI _confirmText;
        private TextMeshProUGUI _confirmYesLabel;
        private TextMeshProUGUI _confirmNoLabel;
        private Action _confirmAction;
        private bool _isSwitchingLanguage;

        private TMP_FontAsset Font => _styleSource != null ? _styleSource.font : null;
        private Color PanelColor => _theme != null ? _theme.PanelBackground : new Color(0.09f, 0.11f, 0.13f, 0.96f);
        private Color ButtonColor => _theme != null ? _theme.HeaderBackground : new Color(0.13f, 0.16f, 0.19f, 1f);
        private Color TextColor => _theme != null ? _theme.ValueText : Color.white;
        private Color LabelColor => _theme != null ? _theme.LabelText : new Color(0.62f, 0.68f, 0.73f);
        private Color DangerColor => _theme != null ? _theme.Alarm : new Color(0.92f, 0.28f, 0.24f);

        private void Awake()
        {
            _storage = SaveStorageFactory.CreateDefault();
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EnsureCamera();
            Build();
        }

        private void OnEnable() => LocText.TableChanged += RefreshTexts;

        private void OnDisable() => LocText.TableChanged -= RefreshTexts;

        private IEnumerator Start()
        {
            if (EventSystem.current == null && FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            }

            RefreshTexts();
            yield return ApplyStoredLocale();
            RefreshTexts();
        }

        // ---- Slots ----

        private void LoadSlot(string slot)
        {
            if (!SaveLoadController.BeginLoad(slot, _storage, out string error))
            {
                Debug.LogWarning("[MainMenu] " + error, this);
                _status.text = LocText.Get("hmi.load_failed", "Loading failed");
            }
        }

        private void NewGame(string slot)
        {
            if (!_storage.Exists(slot))
            {
                StartNewGame(slot);
                return;
            }

            Confirm(string.Format(LocText.Get("menu.confirm_overwrite", "Overwrite save {0}? It is deleted now."), SlotNumber(slot)),
                () =>
                {
                    _storage.Delete(slot);
                    StartNewGame(slot);
                });
        }

        private void StartNewGame(string slot)
        {
            if (!Application.CanStreamedLevelBeLoaded(_gameScene))
            {
                _status.text = $"Scene \"{_gameScene}\" is not in the build settings";
                return;
            }

            SaveLoadController.CurrentSlot = slot;
            SceneManager.LoadScene(_gameScene);
        }

        private void DeleteSlot(string slot)
        {
            Confirm(string.Format(LocText.Get("menu.confirm_delete", "Delete save {0}?"), SlotNumber(slot)),
                () =>
                {
                    _storage.Delete(slot);
                    RefreshTexts();
                });
        }

        private static int SlotNumber(string slot) => Array.IndexOf(SaveLoadController.Slots, slot) + 1;

        /// <summary>"07.10.2026 14:22 · 12 min" or "Empty".</summary>
        private string SlotInfo(string slot, out bool hasSave)
        {
            hasSave = _storage.Exists(slot);
            if (!hasSave)
            {
                return LocText.Get("menu.empty", "Empty");
            }

            if (!SaveLoadController.TryRead(slot, _storage, out SaveData data, out _))
            {
                return LocText.Get("menu.unreadable", "Save cannot be read");
            }

            CultureInfo culture = Culture();
            string date = DateTime.TryParse(data.SavedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime saved)
                ? saved.ToLocalTime().ToString("g", culture)
                : "-";
            int minutes = Mathf.Max(0, Mathf.RoundToInt(data.PlayTimeSeconds / 60f));
            return $"{date}  ·  {minutes} min";
        }

        // ---- Confirmation ----

        private void Confirm(string question, Action onYes)
        {
            _confirmAction = onYes;
            _confirmText.text = question;
            _confirmRoot.SetActive(true);
        }

        private void AnswerConfirm(bool yes)
        {
            _confirmRoot.SetActive(false);
            Action action = _confirmAction;
            _confirmAction = null;
            if (yes)
            {
                action?.Invoke();
            }
        }

        // ---- Language ----

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
                Locale next = locales[(index + 1) % locales.Count];
                LocalizationSettings.SelectedLocale = next;
                PlayerPrefs.SetString(LocalePrefsKey, next.Identifier.Code);
                PlayerPrefs.Save();
            }

            _isSwitchingLanguage = false;
            RefreshTexts();
        }

        /// <summary>The language chosen last time (the project has no PlayerPrefs locale selector).</summary>
        private static IEnumerator ApplyStoredLocale()
        {
            yield return LocalizationSettings.InitializationOperation;
            string code = PlayerPrefs.GetString(LocalePrefsKey, string.Empty);
            Locale locale = string.IsNullOrEmpty(code) ? null : LocalizationSettings.AvailableLocales.GetLocale(code);
            if (locale != null && locale != LocalizationSettings.SelectedLocale)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }

        /// <summary>Native name of the selected language, e.g. "Deutsch", "Français".</summary>
        private static string CurrentLanguageName()
        {
            Locale locale = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;
            if (locale == null)
            {
                return "-";
            }

            CultureInfo culture = locale.Identifier.CultureInfo;
            string name = culture != null ? culture.NativeName : locale.LocaleName;
            return string.IsNullOrEmpty(name) ? locale.Identifier.Code : char.ToUpper(name[0]) + name.Substring(1);
        }

        private static CultureInfo Culture()
        {
            Locale locale = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;
            return locale != null && locale.Identifier.CultureInfo != null ? locale.Identifier.CultureInfo : CultureInfo.InvariantCulture;
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

        /// <summary>The menu scene only holds this component - the camera behind the overlay is created here.</summary>
        private void EnsureCamera()
        {
            if (FindFirstObjectByType<Camera>() != null)
            {
                return;
            }

            var cameraGo = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            Camera menuCamera = cameraGo.GetComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 1f);
            menuCamera.cullingMask = 0;
        }

        private void Build()
        {
            var canvasGo = new GameObject("MainMenuCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            Image panel = HmiUiFactory.CreateImage(canvasGo.transform, "Panel", PanelColor);
            panel.raycastTarget = true;
            var panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(980f, 0f);
            HmiUiFactory.AddVerticalLayout(panel.gameObject, 14f, 32);
            panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = HmiUiFactory.CreateText(panel.transform, "Title", "Food Production Simulator", _fontSize * 1.6f, TextColor,
                TextAlignmentOptions.Center, Font);
            _title.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(_title.gameObject, _fontSize * 2.6f);

            for (int i = 0; i < _rows.Length; i++)
            {
                _rows[i] = BuildSlotRow(panel.transform, SaveLoadController.Slots[i]);
            }

            AddButton(panel.transform, "Language", NextLanguage, ButtonColor, _fontSize * 2f, out _languageLabel);
#if !UNITY_WEBGL || UNITY_EDITOR
            AddButton(panel.transform, "Quit", QuitGame, ButtonColor, _fontSize * 2f, out _quitLabel);
#endif

            _status = HmiUiFactory.CreateText(panel.transform, "Status", string.Empty, _fontSize * 0.7f, LabelColor,
                TextAlignmentOptions.Center, Font);
            HmiUiFactory.SetHeight(_status.gameObject, _fontSize);

            BuildConfirm(canvasGo.transform);
        }

        private SlotRow BuildSlotRow(Transform parent, string slot)
        {
            var row = new SlotRow { Slot = slot };
            Image background = HmiUiFactory.CreateImage(parent, "Slot_" + slot, ButtonColor);
            HmiUiFactory.SetHeight(background.gameObject, _fontSize * 3.4f);
            var layout = background.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 16, 10, 10);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            RectTransform texts = HmiUiFactory.CreateRect("Texts", background.transform);
            HmiUiFactory.AddVerticalLayout(texts.gameObject, 2f, 0);
            HmiUiFactory.SetFlexible(texts.gameObject, 1f, 200f);
            row.Title = HmiUiFactory.CreateText(texts, "Title", slot, _fontSize, TextColor, TextAlignmentOptions.MidlineLeft, Font);
            row.Title.fontStyle = FontStyles.Bold;
            HmiUiFactory.SetHeight(row.Title.gameObject, _fontSize * 1.3f);
            row.Info = HmiUiFactory.CreateText(texts, "Info", string.Empty, _fontSize * 0.75f, LabelColor, TextAlignmentOptions.MidlineLeft, Font);
            HmiUiFactory.SetHeight(row.Info.gameObject, _fontSize * 1.1f);

            row.Load = AddRowButton(background.transform, "Load", () => LoadSlot(slot), ButtonColor * 1.4f, out row.LoadLabel);
            AddRowButton(background.transform, "New", () => NewGame(slot), ButtonColor * 1.4f, out row.NewLabel);
            row.Delete = AddRowButton(background.transform, "Delete", () => DeleteSlot(slot), DangerColor * 0.7f, out row.DeleteLabel);
            return row;
        }

        private void BuildConfirm(Transform canvas)
        {
            Image dim = HmiUiFactory.CreateImage(canvas, "Confirm", new Color(0f, 0f, 0f, 0.7f));
            dim.raycastTarget = true;
            HmiUiFactory.Stretch((RectTransform)dim.transform);
            _confirmRoot = dim.gameObject;

            Image box = HmiUiFactory.CreateImage(dim.transform, "Box", PanelColor);
            var boxRect = (RectTransform)box.transform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(760f, 0f);
            HmiUiFactory.AddVerticalLayout(box.gameObject, 16f, 28);
            box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _confirmText = HmiUiFactory.CreateText(box.transform, "Question", string.Empty, _fontSize, TextColor, TextAlignmentOptions.Center, Font);
            _confirmText.textWrappingMode = TextWrappingModes.Normal;
            HmiUiFactory.SetHeight(_confirmText.gameObject, _fontSize * 2.8f);

            RectTransform buttons = HmiUiFactory.CreateRow(box.transform, "Buttons", _fontSize * 2f, 16f);
            buttons.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            Button yes = AddButton(buttons, "Yes", () => AnswerConfirm(true), DangerColor * 0.7f, _fontSize * 2f, out _confirmYesLabel);
            HmiUiFactory.SetFlexible(yes.gameObject);
            Button no = AddButton(buttons, "No", () => AnswerConfirm(false), ButtonColor, _fontSize * 2f, out _confirmNoLabel);
            HmiUiFactory.SetFlexible(no.gameObject);

            _confirmRoot.SetActive(false);
        }

        private Button AddButton(Transform parent, string objectName, UnityEngine.Events.UnityAction onClick, Color color,
            float height, out TextMeshProUGUI label)
        {
            Button button = HmiUiFactory.CreateButton(parent, objectName, objectName, _fontSize, color, TextColor, Font, out label);
            HmiUiFactory.SetHeight(button.gameObject, height);
            button.onClick.AddListener(onClick);
            return button;
        }

        private Button AddRowButton(Transform parent, string objectName, UnityEngine.Events.UnityAction onClick, Color color,
            out TextMeshProUGUI label)
        {
            Button button = AddButton(parent, objectName, onClick, color, _fontSize * 2f, out label);
            HmiUiFactory.SetWidth(button.gameObject, _fontSize * 6.2f);
            label.fontSize = _fontSize * 0.85f;
            return button;
        }

        private void RefreshTexts()
        {
            _title.text = LocText.Get("menu.title", "Food Production Simulator");
            foreach (SlotRow row in _rows)
            {
                row.Title.text = string.Format(LocText.Get("menu.slot", "Save {0}"), SlotNumber(row.Slot));
                row.Info.text = SlotInfo(row.Slot, out bool hasSave);
                row.Load.gameObject.SetActive(hasSave);
                row.Delete.gameObject.SetActive(hasSave);
                row.LoadLabel.text = LocText.Get("menu.continue", "Continue");
                row.NewLabel.text = LocText.Get("menu.new_game", "New game");
                row.DeleteLabel.text = LocText.Get("menu.delete", "Delete");
            }

            _languageLabel.text = $"{LocText.Get("hmi.language", "Language")}: {CurrentLanguageName()}";
            if (_quitLabel != null)
            {
                _quitLabel.text = LocText.Get("hmi.quit", "Quit");
            }
            _confirmYesLabel.text = LocText.Get("hmi.yes", "Yes");
            _confirmNoLabel.text = LocText.Get("hmi.no", "No");
        }
    }
}
