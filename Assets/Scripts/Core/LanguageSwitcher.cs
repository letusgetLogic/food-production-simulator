using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    public class LanguageSwitcher : MonoBehaviour
    {
        [SerializeField] private SO_LanguageSwitcherChannel _channel;

        public void SetLanguage(string code) => StartCoroutine(SetLocale(code));

        private IEnumerator SetLocale(string code)
        {
            yield return LocalizationSettings.InitializationOperation;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code); // "en", "fr", "de", "eu"
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
                _channel.NotifyLanguageChanged();
            }
        }
    }
}
