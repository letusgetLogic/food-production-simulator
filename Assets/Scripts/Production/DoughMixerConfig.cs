using UnityEngine;

namespace Game.Production
{
    /// <summary>
    /// Machine-specific configuration for a dough mixer station.
    /// These are physical/mechanical properties of the machine itself,
    /// independent of any recipe (the operator dials in recipe-driven values
    /// like mixing duration at the HMI - see DoughMixerMachine.SetMixingDuration).
    /// </summary>
    [CreateAssetMenu(fileName = "DoughMixerConfig", menuName = "Production/Machines/Dough Mixer Config")]
    public class DoughMixerConfig : ScriptableObject
    {
        [Header("Capacity / Motor")]
        [Tooltip("Currently unused by DoughMixerMachine logic (ingredients are not " +
                 "dialed in via the mixer itself). Kept as a documented machine limit " +
                 "for a future automated dosing / quality-check pass.")]
        [Min(0f)] public float MaxBatchWeightKg = 5f;
        [Tooltip("Currently unused by DoughMixerMachine logic - see MaxBatchWeightKg note.")]
        [Min(0f)] public float MixingSpeedRpm = 60f;

        [Header("State Transition Timing")]
        [Tooltip("Each machine subclass defines its own Starting->Running and " +
                 "Stopping->Stopped timing. Wired into DoughMixerMachine via the " +
                 "OnEnterStarting/OnEnterStopping hooks + SetState().")]
        [Min(0f)] public float StartupDurationSeconds = 2f;
        [Min(0f)] public float ShutdownDurationSeconds = 1.5f;
    }
}
