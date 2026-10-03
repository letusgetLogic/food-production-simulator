using System;
using System.Collections.Generic;

namespace Game.Production
{
    /// <summary>
    /// Flat key/value store for the runtime state of one machine in a save file (JsonUtility-friendly:
    /// two parallel lists instead of a dictionary). Only numbers - flags are stored as 0/1.
    /// </summary>
    [Serializable]
    public class SaveValues
    {
        public List<string> Keys = new List<string>();
        public List<float> Values = new List<float>();

        public void Set(string key, float value)
        {
            int index = Keys.IndexOf(key);
            if (index >= 0)
            {
                Values[index] = value;
                return;
            }

            Keys.Add(key);
            Values.Add(value);
        }

        public void Set(string key, int value) => Set(key, (float)value);
        public void Set(string key, bool value) => Set(key, value ? 1f : 0f);

        public bool TryGet(string key, out float value)
        {
            int index = Keys.IndexOf(key);
            if (index >= 0 && index < Values.Count)
            {
                value = Values[index];
                return true;
            }

            value = 0f;
            return false;
        }

        public float GetFloat(string key, float fallback) => TryGet(key, out float value) ? value : fallback;
        public int GetInt(string key, int fallback) => TryGet(key, out float value) ? (int)Math.Round(value) : fallback;
        public bool GetBool(string key, bool fallback) => TryGet(key, out float value) ? value > 0.5f : fallback;
    }

    /// <summary>
    /// Implemented by machines whose runtime content is not covered by their HMI setpoints
    /// (<see cref="IMachineParameterSource"/> setpoints are saved generically): tank levels, counters,
    /// a simulated defect. Called by the save system after the scene has started.
    /// </summary>
    public interface ISaveableState
    {
        void CaptureState(SaveValues values);
        void RestoreState(SaveValues values);
    }
}
