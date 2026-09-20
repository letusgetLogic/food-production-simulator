using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Machine-specific configuration for a dough mixer station.
    /// These are physical/mechanical properties of the machine itself,
    /// independent of any recipe (the operator dials in recipe-driven values
    /// like mixing duration at the HMI - see MixerMachine.SetMixingDuration).
    /// </summary>
    [CreateAssetMenu(fileName = "MixerConfig", menuName = "Production/Machines/Mixer Config")]
    public class SO_MixerConfig : ScriptableObject
    {
        [Header("Capacity / Motor")]
        [Tooltip("Currently unused by MixerMachine logic (ingredients are not " +
                 "dialed in via the mixer itself). Kept as a documented machine limit " +
                 "for a future automated dosing / quality-check pass.")]
        [Min(0f)] public float MaxBatchWeightKg = 5f;
        [Tooltip("Currently unused by MixerMachine logic - see MaxBatchWeightKg note.")]
        [Min(0f)] public float MixingSpeedRpm = 60f;

        [Header("Output")]
        [Tooltip("Weight assigned to the dough ball's ProductInstance when the drum is tilted. " +
                 "A fixed machine value rather than a sensor reading or operator input, since the " +
                 "dough ball has no scale of its own at this point in the line.")]
        [Min(0f)] public float DoughBallWeightGrams = 500f;

        [Header("Mixing")]
        [Tooltip("Default mixing duration used until the operator overrides it via " +
                 "MixerMachine.SetMixingDuration at the HMI.")]
        [Min(0f)] public float DefaultMixingDurationSeconds = 20f;

        [Header("State Transition Timing")]
        [Tooltip("Each machine subclass defines its own Starting->Running and " +
                 "Stopping->Stopped timing. Wired into MixerMachine via the " +
                 "OnEnterStarting/OnEnterStopping hooks + SetState().")]
        [Min(0f)] public float StartupDurationSeconds = 2f;
        [Min(0f)] public float ShutdownDurationSeconds = 1.5f;

        [Header("Drum Tilt")]
        [Tooltip("Duration of the drum's animated rotation on the X-axis between upright " +
                 "(0 deg) and tilted (90 deg), used for both the tilt button and the reset button.")]
        [Min(0f)] public float TiltRotationDurationSeconds = 1f;
    }
}