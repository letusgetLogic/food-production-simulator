using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Production
{
    /// <summary>
    /// Code-side text lookup in the shared string table ("Localization Table", Google Sheets).
    /// Used for texts that are created in code (HMI labels, content messages, fault texts) so they
    /// can be translated without wiring a LocalizedString per text in the inspector.
    ///
    ///  - <see cref="Get"/> returns the translation of the selected locale, or the English fallback
    ///    while the key does not exist in the sheet yet or the table is still loading.
    ///  - The table is loaded asynchronously (WebGL cannot load synchronously) and reloaded when the
    ///    language changes; <see cref="TableChanged"/> fires afterwards.
    ///  - Key conventions (see Docs/localization-keys.csv): "hmi.*" HMI labels and values,
    ///    "unit.*" units, "&lt;machine&gt;.*" content messages, "fault.&lt;code&gt;.msg|.remedy" fault texts.
    /// </summary>
    public static class LocText
    {
        public const string TableName = "Localization Table";

        private static StringTable _table;
        private static bool _isLoading;
        private static bool _isSubscribed;
        private static readonly Dictionary<string, string> SlugCache = new Dictionary<string, string>();

        /// <summary>Raised after the table for the (new) selected locale has been loaded.</summary>
        public static event Action TableChanged;

        /// <summary>Translation of <paramref name="key"/>, or <paramref name="fallback"/> if not available (yet).</summary>
        public static string Get(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key))
            {
                return fallback;
            }

            EnsureLoaded();
            if (_table == null)
            {
                return fallback;
            }

            StringTableEntry entry = _table.GetEntry(key);
            if (entry == null || string.IsNullOrEmpty(entry.Value))
            {
                return fallback;
            }

            return entry.GetLocalizedString();
        }

        /// <summary>
        /// Text of a <see cref="LocalizedString"/> without blocking: the translation if its table is already loaded,
        /// otherwise <paramref name="fallback"/>. Never use <c>LocalizedString.GetLocalizedString()</c> in game code -
        /// it waits synchronously (WaitForCompletion), which throws in WebGL while the table is still loading.
        /// </summary>
        public static string Now(LocalizedString text, string fallback)
        {
            if (text == null || text.IsEmpty)
            {
                return fallback;
            }

            var operation = text.GetLocalizedStringAsync();
            return operation.IsDone && !string.IsNullOrEmpty(operation.Result) ? operation.Result : fallback;
        }

        /// <summary>Content message of a machine: key "&lt;prefix&gt;.&lt;enum_value_in_snake_case&gt;".</summary>
        public static string Info<TEnum>(string prefix, TEnum info, string fallback) where TEnum : Enum =>
            Get(prefix + "." + Snake(info.ToString()), fallback);

        /// <summary>Key derived from an English text: ("hmi", "Last bake time") -> "hmi.last_bake_time".</summary>
        public static string KeyFromText(string prefix, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string cacheKey = prefix + "|" + text;
            if (SlugCache.TryGetValue(cacheKey, out string cached))
            {
                return cached;
            }

            var builder = new StringBuilder();
            bool pendingUnderscore = false;
            foreach (char c in text.ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    if (pendingUnderscore && builder.Length > 0)
                    {
                        builder.Append('_');
                    }
                    builder.Append(c);
                    pendingUnderscore = false;
                }
                else
                {
                    pendingUnderscore = true;
                }
            }

            // Texts without letters (units like "°C", "%") get no key - nothing to translate.
            string key = builder.Length > 0 ? prefix + "." + builder : null;
            SlugCache[cacheKey] = key;
            return key;
        }

        /// <summary>"WrongProduct" / "SauceEmpty" -> "wrong_product" / "sauce_empty".</summary>
        public static string Snake(string pascal)
        {
            if (string.IsNullOrEmpty(pascal))
            {
                return pascal;
            }

            var builder = new StringBuilder(pascal.Length + 8);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0 && pascal[i - 1] != '_')
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        private static void EnsureLoaded()
        {
            if (!_isSubscribed)
            {
                if (!LocalizationSettings.HasSettings)
                {
                    return;
                }

                LocalizationSettings.SelectedLocaleChanged += _ => Reload();
                _isSubscribed = true;
            }

            if (_table == null && !_isLoading)
            {
                Reload();
            }
        }

        private static void Reload()
        {
            _isLoading = true;
            AsyncOperationHandle<StringTable> handle = LocalizationSettings.StringDatabase.GetTableAsync(TableName);
            handle.Completed += operation =>
            {
                _isLoading = false;
                if (operation.Status == AsyncOperationStatus.Succeeded && operation.Result != null)
                {
                    _table = operation.Result;
                    TableChanged?.Invoke();
                }
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Enter Play Mode without domain reload: start clean every session.
            _table = null;
            _isLoading = false;
            _isSubscribed = false;
            TableChanged = null;
        }
    }
}
