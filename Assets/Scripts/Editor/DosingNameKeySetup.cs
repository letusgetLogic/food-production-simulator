using Game.Production;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.EditorTools
{
    /// <summary>
    /// Gives the dosing station its own name key "text.dosing_station" (en/de/fr/es) in "Localization Table" and
    /// assigns it to every <see cref="DosingMachine"/> in the open scene. Before, the station referenced the topping
    /// entry and showed up as "Belag" in the HMI.
    ///
    /// The key is also listed in Docs/localization-keys.csv/.tsv - add it to the Google Sheet as well, otherwise a
    /// pull with "Remove Missing Pulled Keys" deletes it again.
    ///
    /// Menu: Tools / Food Production / Setup Dosing Station Name Key (repeatable, undoable).
    /// </summary>
    public static class DosingNameKeySetup
    {
        private const string TableCollectionName = "Localization Table";
        private const string Key = "text.dosing_station";

        private static readonly (string locale, string text)[] Translations =
        {
            ("en", "Dosing station"),
            ("de", "Dosierstation"),
            ("fr", "Station de dosage"),
            ("es", "Estación de dosificación")
        };

        [MenuItem("Tools/Food Production/Setup Dosing Station Name Key")]
        public static void Setup()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableCollectionName);
            if (collection == null)
            {
                SetupUi.Dialog("Setup Dosing Station Name Key", $"Tabellen-Sammlung \"{TableCollectionName}\" nicht gefunden.", "OK");
                return;
            }

            foreach ((string locale, string text) in Translations)
            {
                var table = collection.GetTable(locale) as UnityEngine.Localization.Tables.StringTable;
                if (table == null)
                {
                    continue;
                }

                Undo.RecordObject(table, "Dosing station name key");
                Undo.RecordObject(table.SharedData, "Dosing station name key");
                table.AddEntry(Key, text);
                EditorUtility.SetDirty(table);
                EditorUtility.SetDirty(table.SharedData);
            }
            AssetDatabase.SaveAssets();

            long keyId = collection.SharedData.GetId(Key);
            int assigned = 0;
            foreach (DosingMachine dosing in Object.FindObjectsByType<DosingMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(dosing);
                SerializedProperty nameKey = serialized.FindProperty("_nameKey");
                var localized = new LocalizedString(collection.SharedData.TableCollectionNameGuid, keyId);
                nameKey.boxedValue = localized;
                serialized.ApplyModifiedProperties();
                assigned++;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Setup Dosing Station Name Key",
                $"Key {Key} (Id {keyId}) angelegt und {assigned} Dosierstation(en) zugewiesen.\n" +
                "Key auch ins Google Sheet eintragen. Szene speichern (Strg+S).", "OK");
        }
    }
}
