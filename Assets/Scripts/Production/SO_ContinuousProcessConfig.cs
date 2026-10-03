using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Content-driven configuration for a <see cref="ContinuousProcessStation"/> (oven, cooling
    /// tunnel, shock freezer). Default values are only used at scene start - the operator sets dwell
    /// time and temperature at the HMI, reading them off the recipe terminal.
    ///
    /// Suggested values (Margherita, preliminary):
    ///   Oven:    ToppedPizza -> BakedPizza,  dwell 15 s, target 280 °C, valid 250..320 °C, record bake time
    ///   Cooling: BakedPizza  -> CooledPizza, dwell 10 s, target  20 °C
    ///   Freezer: CooledPizza -> FrozenPizza, dwell 12 s, target -18 °C
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ContinuousProcessConfig", menuName = "Production/Machines/Continuous Process Config")]
    public class SO_ContinuousProcessConfig : ScriptableObject
    {
        [Header("Product")]
        [Tooltip("State a product must have when it leaves the zone to be processed.")]
        public ProductState InputState = ProductState.ToppedPizza;
        [Tooltip("State the product gets when it leaves the zone.")]
        public ProductState OutputState = ProductState.BakedPizza;
        [Tooltip("Write the measured dwell time to ProductInstance.ActualBakeTimeSeconds (oven only).")]
        public bool RecordDwellAsBakeTime = true;
        [Tooltip("How long a wrong product passing through keeps the warning (HMI amber) active.")]
        [Min(0f)] public float WrongProductWarningSeconds = 3f;

        [Header("Timing")]
        public float StartupDurationSeconds = 1f;
        public float ShutdownDurationSeconds = 1f;

        [Header("Dwell Time (Verweilzeit = zone length / belt speed)")]
        [Min(0.1f)] public float DefaultDwellSeconds = 15f;
        [Min(0.1f)] public float MinDwellSeconds = 5f;
        [Min(0.1f)] public float MaxDwellSeconds = 60f;
        [Tooltip("Step of the dwell time setpoint at the machine terminal.")]
        [Min(0.1f)] public float DwellStepSeconds = 0.5f;

        [Header("Temperature")]
        [Tooltip("Off = station without temperature control (TemperatureSensor may be left empty).")]
        public bool UsesTemperature = true;
        public float DefaultTargetTemperatureCelsius = 280f;
        public float MinTargetTemperatureCelsius = 150f;
        public float MaxTargetTemperatureCelsius = 320f;
        [Tooltip("Step of the temperature setpoint at the machine terminal.")]
        [Min(0.1f)] public float TemperatureStepCelsius = 5f;
        [Tooltip("Deviation from the setpoint above which the station reports a warning (HMI amber).")]
        [Min(0f)] public float WarningToleranceCelsius = 10f;
        [Tooltip("Below this the process is invalid (oven: 250 °C). Fault after the grace time.")]
        public float MinValidTemperatureCelsius = 250f;
        [Tooltip("Above this the process is invalid. Fault after the grace time.")]
        public float MaxValidTemperatureCelsius = 320f;
        [Min(0f)] public float OutOfRangeFaultGraceSeconds = 5f;

        [Header("Heat-up")]
        [Tooltip("Starting waits until the temperature is within the warning tolerance before the belt runs.")]
        public bool RequireTemperatureBeforeRun = true;
        [Min(0f)] public float HeatUpTimeoutSeconds = 60f;
        [Tooltip("Keep heating/cooling while Stopped (short stops don't need a new heat-up).")]
        public bool KeepTemperatureWhileStopped = true;
    }
}
