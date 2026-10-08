using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.HMI
{
    /// <summary>What finishes a tutorial step. Evaluated by <see cref="TutorialController"/> every frame.</summary>
    public enum TutorialCondition
    {
        /// <summary>Step stays until the player presses the "next" key (or the auto-advance time runs out).</summary>
        Manual,
        PlayerMoved,
        UiOpened,
        MixerRunning,
        MixingComplete,
        DrumTilted,
        HoldingObject,
        PotOnLift,
        HopperFilled,
        PortionProduced,
        PizzaFormed,
        ProductInspected,
        /// <summary>The recipe page of the HMI is shown (appended - values are stored as numbers in the asset).</summary>
        RecipeOpened
    }

    /// <summary>
    /// Content of the guided first batch (PM content, no code): the step list with text keys and the
    /// condition that completes each step. Texts come from the localization table via <c>LocText</c>
    /// (key <c>tutorial.*</c>), the English text here is the fallback. Placeholders: {0} = interact key,
    /// {1} = drop key, {2} = key for the next step, {3} = key to hide the tutorial.
    /// </summary>
    [CreateAssetMenu(fileName = "TutorialSequence", menuName = "Food Production/Tutorial Sequence")]
    public class SO_TutorialSequence : ScriptableObject
    {
        [Serializable]
        public class Step
        {
            [Tooltip("Localization key of the title, e.g. tutorial.move.title")]
            public string TitleKey;
            public string TitleFallback;

            [Tooltip("Localization key of the instruction text, e.g. tutorial.move.text")]
            public string TextKey;
            [TextArea(2, 5)] public string TextFallback;

            public TutorialCondition Condition;

            [Tooltip("PlayerMoved: metres to walk. Manual: seconds until the step advances by itself (0 = only by key).")]
            [Min(0f)] public float Amount;
        }

        [SerializeField] private List<Step> _steps = new List<Step>();

        public IReadOnlyList<Step> Steps => _steps;

        /// <summary>Editor setup only.</summary>
        public void SetSteps(IEnumerable<Step> steps)
        {
            _steps = new List<Step>(steps);
        }
    }
}
