using System;
using System.IO;
using UnityEngine;

namespace Game.Persistence
{
    /// <summary>Where save files live. Desktop/editor: JSON files; WebGL: PlayerPrefs (IndexedDB).</summary>
    public interface ISaveStorage
    {
        bool Exists(string slot);
        string Read(string slot);
        void Write(string slot, string json);
        void Delete(string slot);
        string Describe(string slot);
    }

    /// <summary>One JSON file per slot under persistentDataPath/saves.</summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _folder;

        public FileSaveStorage(string folder = null)
        {
            _folder = folder ?? Path.Combine(Application.persistentDataPath, "saves");
        }

        private string PathOf(string slot) => Path.Combine(_folder, slot + ".json");

        public bool Exists(string slot) => File.Exists(PathOf(slot));

        public string Read(string slot) => Exists(slot) ? File.ReadAllText(PathOf(slot)) : null;

        public void Write(string slot, string json)
        {
            Directory.CreateDirectory(_folder);

            // Write to a temp file first so a crash mid-write never destroys the previous save.
            string target = PathOf(slot);
            string temp = target + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(target))
            {
                File.Delete(target);
            }
            File.Move(temp, target);
        }

        public void Delete(string slot)
        {
            if (Exists(slot))
            {
                File.Delete(PathOf(slot));
            }
        }

        public string Describe(string slot) => PathOf(slot);
    }

    /// <summary>
    /// WebGL: files in persistentDataPath are not flushed to IndexedDB reliably without a JS plugin,
    /// PlayerPrefs are. Limit ~1 MB per origin - a save of this line is a few KB.
    /// </summary>
    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        private const string Prefix = "fps.save.";

        public bool Exists(string slot) => PlayerPrefs.HasKey(Prefix + slot);

        public string Read(string slot) => Exists(slot) ? PlayerPrefs.GetString(Prefix + slot) : null;

        public void Write(string slot, string json)
        {
            PlayerPrefs.SetString(Prefix + slot, json);
            PlayerPrefs.Save();
        }

        public void Delete(string slot)
        {
            PlayerPrefs.DeleteKey(Prefix + slot);
            PlayerPrefs.Save();
        }

        public string Describe(string slot) => "PlayerPrefs:" + Prefix + slot;
    }

    public static class SaveStorageFactory
    {
        public static ISaveStorage CreateDefault()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return new PlayerPrefsSaveStorage();
#else
            return new FileSaveStorage();
#endif
        }
    }
}
