using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Game.Production
{
    /// <summary>
    /// One fault case of the FaultSystem: what the operator sees in the alarm list and what fixes it.
    /// The <see cref="Code"/> matches the reason a machine passes to <see cref="MachineBase.TriggerFault"/>
    /// (prefix match, so "WrongProduct" also covers "WrongProduct:MixedDough").
    /// </summary>
    [Serializable]
    public class FaultDefinition
    {
        [Tooltip("Fault reason code as used in TriggerFault (prefix match).")]
        public string Code;

        [Tooltip("Alarm text. Leave empty until the key exists - the fallback text is used then.")]
        public LocalizedString MessageKey;

        [Tooltip("English fallback alarm text until the localization key exists.")]
        public string FallbackMessage;

        [Tooltip("Remedy hint for the operator (shown in the alarm list / machine panel).")]
        public LocalizedString RemedyKey;

        [Tooltip("English fallback remedy text until the localization key exists.")]
        public string FallbackRemedy;

        [Tooltip("Part of the curated training set (the 3-5 fault cases of the MVP).")]
        public bool IsTrainingCase = true;

        [Tooltip("Can be injected by the FaultMonitor for training (scenario / debug menu).")]
        public bool CanBeInjected;

        public string Message => Localized(MessageKey, FallbackMessage, Code);
        public string Remedy => Localized(RemedyKey, FallbackRemedy, string.Empty);

        public bool Matches(string reason) =>
            !string.IsNullOrEmpty(Code) && !string.IsNullOrEmpty(reason)
            && reason.StartsWith(Code, StringComparison.Ordinal);

        private static string Localized(LocalizedString key, string fallback, string lastResort)
        {
            if (key != null && !key.IsEmpty)
            {
                string text = key.GetLocalizedString();
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return string.IsNullOrEmpty(fallback) ? lastResort : fallback;
        }
    }

    /// <summary>
    /// Content asset listing the fault cases the operator can run into (FaultSystem, Woche 2).
    /// The machines detect and raise faults themselves; this catalog only gives them texts, remedies and
    /// marks which of them form the curated training set.
    ///
    /// MVP training set (PM decision 02.10.):
    ///   1. SauceEmpty / ToppingEmpty  - dosing station, refill button
    ///   2. BufferFull                 - press buffer, press stopped / too slow
    ///   3. Jam                        - belt jam, remove product, restart
    ///   4. TemperatureOutOfRange / HeatUpTimeout - oven heater failure (injectable)
    ///   5. WrongProduct               - wrong product in the press
    /// </summary>
    [CreateAssetMenu(fileName = "FaultCatalog", menuName = "Production/Fault Catalog")]
    public class SO_FaultCatalog : ScriptableObject
    {
        [SerializeField] private List<FaultDefinition> _faults = new List<FaultDefinition>();

        public IReadOnlyList<FaultDefinition> Faults => _faults;

        /// <summary>Finds the definition for a fault reason (longest matching code wins). Null if unknown.</summary>
        public FaultDefinition Find(string reason)
        {
            FaultDefinition best = null;
            foreach (FaultDefinition fault in _faults)
            {
                if (fault != null && fault.Matches(reason)
                    && (best == null || fault.Code.Length > best.Code.Length))
                {
                    best = fault;
                }
            }
            return best;
        }

        /// <summary>Fills the catalog with the MVP fault cases (used by the editor setup on first creation).</summary>
        public void ResetToDefaults()
        {
            _faults = new List<FaultDefinition>
            {
                Define("SauceEmpty", "Sauce tank empty",
                    "Refill the sauce tank at the dosing station (refill button), acknowledge, complete maintenance, start."),
                Define("ToppingEmpty", "Topping hopper empty",
                    "Refill the topping hopper at the dosing station (refill button), acknowledge, complete maintenance, start."),
                Define("BufferFull", "Press buffer full",
                    "Check the press (running? cycle time too long?). Then acknowledge the buffer belt and restart."),
                Define("Jam", "Conveyor jam",
                    "Remove the stuck product from the belt, acknowledge, complete maintenance, start the belt."),
                Define("TemperatureOutOfRange", "Temperature out of range",
                    "Heater/cooling unit defect. Acknowledge, complete maintenance (repairs the unit), start and wait for temperature.",
                    canBeInjected: true),
                Define("HeatUpTimeout", "Setpoint temperature not reached",
                    "Check the temperature setpoint. Acknowledge, complete maintenance, start again."),
                Define("WrongProduct", "Wrong product in press",
                    "The product is marked as scrap and will be ignored. Acknowledge, complete maintenance, start."),
                Define("BeltFault", "Station belt fault",
                    "Fix the belt fault first (see belt alarm), then acknowledge the station.", isTraining: false),
            };
        }

        private static FaultDefinition Define(string code, string message, string remedy,
            bool canBeInjected = false, bool isTraining = true) => new FaultDefinition
        {
            Code = code,
            FallbackMessage = message,
            FallbackRemedy = remedy,
            IsTrainingCase = isTraining,
            CanBeInjected = canBeInjected,
            MessageKey = new LocalizedString(),
            RemedyKey = new LocalizedString()
        };
    }
}
