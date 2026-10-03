using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Judgement of a displayed value, decided by the machine (sensors deliver raw values,
    /// machines evaluate). Mapped 1:1 onto the HMI severity colours by the HMI layer.
    /// </summary>
    public enum MachineValueLevel
    {
        Inactive,
        Normal,
        Warning,
        Alarm
    }

    /// <summary>
    /// Optional fixed tile in the machine panel a readout should be shown in. Readouts without a
    /// slot (or whose tile does not exist in a panel) are listed under "Actual values".
    /// </summary>
    public enum MachineReadoutSlot
    {
        None,
        Temperature,
        FillLevel,
        CycleTime,
        Setpoint
    }

    /// <summary>
    /// An operator setpoint (Sollwert) of a machine. The operator reads the value off the recipe
    /// terminal and dials it in at the machine terminal - there is no recipe binding in code.
    /// </summary>
    public sealed class MachineParameter
    {
        private readonly Func<float> _getter;
        private readonly Action<float> _setter;

        public MachineParameter(string id, string label, string unit, float min, float max, float step,
            string format, Func<float> getter, Action<float> setter)
        {
            Id = id;
            _label = label;
            _unit = unit;
            _labelKey = LocText.KeyFromText("hmi", label);
            _unitKey = LocText.KeyFromText("unit", unit);
            Min = Mathf.Min(min, max);
            Max = Mathf.Max(min, max);
            Step = Mathf.Max(0f, step);
            Format = string.IsNullOrEmpty(format) ? "0.##" : format;
            _getter = getter;
            _setter = setter;
        }

        private readonly string _label;
        private readonly string _unit;
        private readonly string _labelKey;
        private readonly string _unitKey;

        public string Id { get; }

        /// <summary>Localized label (key "hmi.&lt;english_label&gt;", English fallback).</summary>
        public string Label => LocText.Get(_labelKey, _label);

        /// <summary>Localized unit (key "unit.&lt;unit&gt;" for units with letters, e.g. "pcs").</summary>
        public string Unit => LocText.Get(_unitKey, _unit);

        public float Min { get; }
        public float Max { get; }
        public float Step { get; }
        public string Format { get; }

        public float Value => _getter != null ? _getter() : 0f;

        public string FormattedValue => Value.ToString(Format);

        /// <summary>Clamps to [Min, Max], snaps to the step grid and forwards to the machine.</summary>
        public void SetValue(float value)
        {
            float clamped = Mathf.Clamp(value, Min, Max);
            if (Step > 0f)
            {
                clamped = Mathf.Clamp(Min + Mathf.Round((clamped - Min) / Step) * Step, Min, Max);
            }

            _setter?.Invoke(clamped);
        }

        /// <summary>Changes the value by a number of steps (negative = down).</summary>
        public void Nudge(int steps) => SetValue(Value + steps * Step);
    }

    /// <summary>An actual value (Ist-Wert) of a machine, polled by the HMI.</summary>
    public sealed class MachineReadout
    {
        private readonly Func<string> _text;
        private readonly Func<MachineValueLevel> _level;

        public MachineReadout(string id, string label, string unit, Func<string> text,
            Func<MachineValueLevel> level = null, MachineReadoutSlot slot = MachineReadoutSlot.None)
        {
            Id = id;
            _label = label;
            _unit = unit;
            _labelKey = LocText.KeyFromText("hmi", label);
            _unitKey = LocText.KeyFromText("unit", unit);
            Slot = slot;
            _text = text;
            _level = level;
        }

        private readonly string _label;
        private readonly string _unit;
        private readonly string _labelKey;
        private readonly string _unitKey;

        public string Id { get; }

        /// <summary>Localized label (key "hmi.&lt;english_label&gt;", English fallback).</summary>
        public string Label => LocText.Get(_labelKey, _label);

        /// <summary>Localized unit (key "unit.&lt;unit&gt;" for units with letters, e.g. "pcs").</summary>
        public string Unit => LocText.Get(_unitKey, _unit);

        public MachineReadoutSlot Slot { get; }

        public string Text => _text != null ? _text() : "--";
        public MachineValueLevel Level => _level != null ? _level() : MachineValueLevel.Normal;
    }

    /// <summary>
    /// Implemented by machines whose terminal offers setpoints and actual values. The HMI only knows
    /// this interface, never the concrete machine type.
    /// </summary>
    public interface IMachineParameterSource
    {
        IReadOnlyList<MachineParameter> Parameters { get; }
        IReadOnlyList<MachineReadout> Readouts { get; }
    }
}
