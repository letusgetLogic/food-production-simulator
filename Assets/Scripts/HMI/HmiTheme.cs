using System;
using Game.Production;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>
    /// Central visual configuration for the operator panel. Kept as a
    /// ScriptableObject so colours and thresholds are authored in the editor
    /// instead of hardcoded in views.
    /// </summary>
    [CreateAssetMenu(menuName = "HMI/HMI Theme", fileName = "HmiTheme")]
    public class HmiTheme : ScriptableObject
    {
        [Serializable]
        public struct MachineStateStyle
        {
            public MachineState State;
            public Color Color;
            public string DisplayName;
        }

        [Header("Panel")]
        public Color PanelBackground = new Color(0.09f, 0.11f, 0.13f, 0.96f);
        public Color PanelBorder = new Color(0.22f, 0.26f, 0.30f, 1f);
        public Color HeaderBackground = new Color(0.13f, 0.16f, 0.19f, 1f);

        [Header("Text")]
        public Color LabelText = new Color(0.62f, 0.68f, 0.73f, 1f);
        public Color ValueText = new Color(0.92f, 0.95f, 0.97f, 1f);

        [Header("Semantic")]
        public Color Normal = new Color(0.30f, 0.80f, 0.45f, 1f);
        public Color Warning = new Color(0.98f, 0.72f, 0.16f, 1f);
        public Color Alarm = new Color(0.92f, 0.28f, 0.24f, 1f);
        public Color Inactive = new Color(0.40f, 0.45f, 0.50f, 1f);

        [Header("Machine States")]
        [SerializeField] private MachineStateStyle[] _machineStateStyles;

        public Color GetMachineStateColor(MachineState state)
        {
            return TryGetStyle(state, out MachineStateStyle style) ? style.Color : Inactive;
        }

        public string GetMachineStateDisplayName(MachineState state)
        {
            return TryGetStyle(state, out MachineStateStyle style) && !string.IsNullOrEmpty(style.DisplayName)
                ? style.DisplayName
                : state.ToString();
        }

        private bool TryGetStyle(MachineState state, out MachineStateStyle result)
        {
            if (_machineStateStyles != null)
            {
                for (int i = 0; i < _machineStateStyles.Length; i++)
                {
                    if (_machineStateStyles[i].State == state)
                    {
                        result = _machineStateStyles[i];
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }
    }
}
