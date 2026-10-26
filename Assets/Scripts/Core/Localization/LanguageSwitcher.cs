using System.Collections;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Game.Core
{
    /// <summary>
    /// Sets the language (main menu, e.g. a button per locale). No notification needed: LocalizedStrings and
    /// LocText follow LocalizationSettings.SelectedLocaleChanged themselves.
    /// </summary>
    public class LanguageSwitcher : MonoBehaviour
    {
        public void SetLanguage(string code) => StartCoroutine(SetLocale(code));

        private IEnumerator SetLocale(string code)
        {
            yield return LocalizationSettings.InitializationOperation;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(code); // "en", "fr", "de", "eu"
            if (locale != null)
            {
                LocalizationSettings.SelectedLocale = locale;
            }
        }
    }
}
