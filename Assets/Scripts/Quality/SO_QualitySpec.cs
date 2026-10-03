using UnityEngine;

namespace Game.Quality
{
    /// <summary>
    /// Target values and tolerances the QualitySystem checks a finished product against (end of line).
    /// Content only - tuned by the PM after playtests. Defaults = Margherita (preliminary, 02.10.).
    ///
    /// Each check can be switched off. A value that a product does not carry (e.g. no oven yet) counts as
    /// a defect "missing" only if the check is on and <see cref="RequireAllMeasurements"/> is set.
    /// </summary>
    [CreateAssetMenu(fileName = "QualitySpec", menuName = "Quality/Quality Spec")]
    public class SO_QualitySpec : ScriptableObject
    {
        [Tooltip("Shown in the HMI / logs.")]
        public string DisplayName = "Margherita";

        [Header("Final state")]
        [Tooltip("Products that arrive at the line end in an earlier state are scrap (NotFinished).")]
        public Game.Production.ProductState RequiredFinalState = Game.Production.ProductState.PackagedPizza;

        [Tooltip("A value missing on the product (station skipped) is a defect.")]
        public bool RequireAllMeasurements = true;

        [Header("Portion weight (g)")]
        public bool CheckWeight = true;
        public float TargetWeightGrams = 250f;
        [Min(0f)] public float WeightToleranceGrams = 15f;

        [Header("Pizza base (press setting, cm / mm)")]
        public bool CheckDimensions = true;
        public float TargetDiameterCm = 28f;
        [Min(0f)] public float DiameterToleranceCm = 1f;
        public float TargetThicknessMm = 3f;
        [Min(0f)] public float ThicknessToleranceMm = 0.5f;

        [Header("Dosing (g)")]
        public bool CheckDosing = true;
        public float TargetSauceGrams = 80f;
        [Min(0f)] public float SauceToleranceGrams = 8f;
        public float TargetToppingGrams = 120f;
        [Min(0f)] public float ToppingToleranceGrams = 12f;

        [Header("Baking")]
        public bool CheckBaking = true;
        public float TargetBakeTimeSeconds = 15f;
        [Min(0f)] public float BakeTimeToleranceSeconds = 1.5f;
        [Tooltip("Average oven temperature below this = underbaked (Fehlerfall Mindesttemperatur).")]
        public float MinBakeTemperatureCelsius = 250f;
        public float MaxBakeTemperatureCelsius = 320f;

        [Header("Cooling / freezing")]
        public bool CheckCooling = true;
        [Tooltip("Average cooling tunnel temperature must not be above this.")]
        public float MaxCoolingTemperatureCelsius = 25f;
        public bool CheckFreezing = true;
        [Tooltip("Average freezer temperature must not be above this (cold chain).")]
        public float MaxFreezingTemperatureCelsius = -15f;

        [Header("Statistics")]
        [Tooltip("Window for the throughput figure (units per minute).")]
        [Min(5f)] public float ThroughputWindowSeconds = 60f;
    }
}
