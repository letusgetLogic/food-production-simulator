using System.IO;
using System.Linq;
using Game.Core;
using Game.HMI;
using Game.Production;
using Game.Quality;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Guided first batch: creates ScriptableObjects/Tutorial/TutorialSequence_FirstBatch.asset (13 steps, English
    /// fallback texts, keys tutorial.*) and a root object "Tutorial" with a wired <see cref="TutorialController"/>.
    /// The "read the recipe" step is only included when the scene has a recipe terminal.
    /// Translations: Docs/localization-keys.csv/.tsv (keys tutorial.*, hmi.tutorial).
    ///
    /// Menu: Tools / Food Production / Setup Tutorial (repeatable - the sequence asset is rewritten, the object reused).
    /// </summary>
    public static class TutorialSetup
    {
        private const string Folder = "Assets/ScriptableObjects/Tutorial";
        private const string SequencePath = Folder + "/TutorialSequence_FirstBatch.asset";

        [MenuItem("Tools/Food Production/Setup Tutorial")]
        public static void Setup()
        {
            SO_TutorialSequence sequence = CreateOrUpdateSequence(out int stepCount);

            var controller = Object.FindFirstObjectByType<TutorialController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                var go = new GameObject("Tutorial");
                Undo.RegisterCreatedObjectUndo(go, "Setup Tutorial");
                controller = go.AddComponent<TutorialController>();
            }

            var pauseMenu = Object.FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            var serializedPause = pauseMenu != null ? new SerializedObject(pauseMenu) : null;
            GameObject player = GameObject.Find("Player");

            var so = new SerializedObject(controller);
            so.FindProperty("_sequence").objectReferenceValue = sequence;
            so.FindProperty("_player").objectReferenceValue = player != null ? player.transform : null;
            MixerMachine[] mixers = Object.FindObjectsByType<MixerMachine>(FindObjectsSortMode.None);
            SerializedProperty mixerList = so.FindProperty("_mixers");
            mixerList.arraySize = mixers.Length;
            for (int i = 0; i < mixers.Length; i++)
            {
                mixerList.GetArrayElementAtIndex(i).objectReferenceValue = mixers[i];
            }
            so.FindProperty("_portioner").objectReferenceValue = Object.FindFirstObjectByType<PortionerMachine>();
            so.FindProperty("_press").objectReferenceValue = Object.FindFirstObjectByType<PressMachine>();
            so.FindProperty("_qualityInspector").objectReferenceValue = Object.FindFirstObjectByType<QualityInspector>();
            so.FindProperty("_uiFocusChannel").objectReferenceValue =
                serializedPause?.FindProperty("_uiFocusChannel").objectReferenceValue ?? FindAsset<SO_UiFocusChannel>();
            so.FindProperty("_holdChannel").objectReferenceValue = FindAsset<SO_HoldChannel>();
            if (serializedPause != null)
            {
                so.FindProperty("_theme").objectReferenceValue = serializedPause.FindProperty("_theme").objectReferenceValue;
                so.FindProperty("_styleSource").objectReferenceValue = serializedPause.FindProperty("_styleSource").objectReferenceValue;
            }
            so.ApplyModifiedProperties();

            // Show the tutorial again on the next Play in the editor (also resets automation test runs).
            PlayerPrefs.DeleteKey(TutorialController.CompletedPrefsKey);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            string missing = string.Join(", ", new[] { "_player", "_portioner", "_press", "_qualityInspector", "_uiFocusChannel", "_holdChannel" }
                .Where(name => so.FindProperty(name).objectReferenceValue == null));
            SetupUi.Dialog("Setup Tutorial",
                $"Tutorial mit {stepCount} Schritten angelegt ({SequencePath}).\n" +
                $"{mixers.Length} Teigmischer verknüpft.\n" +
                (missing.Length > 0 ? $"Nicht gefunden: {missing}\n" : string.Empty) +
                "Startet beim ersten Spielstart; Pausemenü → Tutorial startet es neu. Szene speichern (Strg+S).", "OK");
        }

        private static SO_TutorialSequence CreateOrUpdateSequence(out int stepCount)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            var sequence = AssetDatabase.LoadAssetAtPath<SO_TutorialSequence>(SequencePath);
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<SO_TutorialSequence>();
                AssetDatabase.CreateAsset(sequence, SequencePath);
            }

            bool hasRecipeTerminal = Object.FindFirstObjectByType<RecipeTerminalInteractable>(FindObjectsInactive.Include) != null;
            SO_TutorialSequence.Step[] steps =
            {
            Step("welcome", TutorialCondition.PlayerMoved, 2f,
                "Welcome to the line",
                "Today you run a frozen pizza line - from the dough mixer to the packed pizza. Walk with W A S D and look around with the mouse."),
            Step("recipe", TutorialCondition.UiOpened, 0f,
                "Read the recipe",
                "Aim at the recipe terminal and press {0}. It lists the Margherita values every machine needs. Close it with Esc."),
            Step("mixer_start", TutorialCondition.MixerRunning, 0f,
                "Start the dough mixer",
                "Go to a dough mixer and start it: green start button on the mixer, or its terminal ({0})."),
            Step("mixer_wait", TutorialCondition.MixingComplete, 0f,
                "Mixing",
                "The mixer needs a moment. Make sure the pot stands under the drum - pick it up with {0}, put it down with {1}."),
            Step("mixer_tilt", TutorialCondition.DrumTilted, 0f,
                "Tilt the drum",
                "Mixing is done. Press the tilt button on the mixer with {0} - the dough ball drops into the pot."),
            Step("pot_carry", TutorialCondition.HoldingObject, 0f,
                "Pick up the pot",
                "Aim at the pot with the dough and pick it up with {0}."),
            Step("pot_lift", TutorialCondition.PotOnLift, 0f,
                "Load the portioner",
                "Carry the pot to the portioner and put it down on the lift with {1}."),
            Step("lift_up", TutorialCondition.HopperFilled, 0f,
                "Raise the lift",
                "Press the lift-up button with {0}. The pot tips and the dough falls into the hopper."),
            Step("portion", TutorialCondition.PortionProduced, 0f,
                "Portioning",
                "The portioner cuts the dough into portions (target 250 g). If it is stopped, start it at its terminal."),
            Step("press", TutorialCondition.PizzaFormed, 0f,
                "Forming",
                "The portions wait in the buffer and run into the press, which forms the pizza base. Watch the first stroke."),
            Step("hmi", TutorialCondition.UiOpened, 0f,
                "Watch the line",
                "Open the line terminal next to the press with {0}. Green = running, amber = warning, red = fault."),
            Step("first_pizza", TutorialCondition.ProductInspected, 0f,
                "The first pizza",
                "Follow your pizza: sauce and topping, oven, cooling, shock freezer, packaging. Quality is checked at the end of the line."),
            Step("done", TutorialCondition.Manual, 12f,
                "First batch done!",
                "Faults will happen - the HMI shows the cause and the remedy. Esc opens the pause menu with controls, save and load.")
            };
            var used = steps.Where(s => hasRecipeTerminal || s.TitleKey != "tutorial.recipe.title").ToArray();
            sequence.SetSteps(used);
            EditorUtility.SetDirty(sequence);
            AssetDatabase.SaveAssets();
            stepCount = used.Length;
            return sequence;
        }

        private static SO_TutorialSequence.Step Step(string id, TutorialCondition condition, float amount, string title, string text) =>
            new SO_TutorialSequence.Step
            {
                TitleKey = $"tutorial.{id}.title",
                TitleFallback = title,
                TextKey = $"tutorial.{id}.text",
                TextFallback = text,
                Condition = condition,
                Amount = amount
            };

        private static T FindAsset<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(asset => asset != null);
    }
}
