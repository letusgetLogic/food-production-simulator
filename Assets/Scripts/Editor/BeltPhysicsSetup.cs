using System.Collections.Generic;
using System.IO;
using Game.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Gives every belt surface (child "Cube" of a belt root) a frictionless physics material.
    ///
    /// Why: ConveyorBelt moves products by setting their velocity every physics step. With default friction
    /// (0.6) the solver takes up to ~0.12 m/s per step away again, so slow belts (oven 0.21 m/s) barely move
    /// a pizza, and at a belt-to-belt handover (two contacts, slight tilt) the product stops completely and
    /// the belt reports a Jam. Without friction the product moves exactly at belt speed; ConveyorBelt sets
    /// the velocity to zero itself while held or stopped.
    ///
    /// Menu: Tools / Food Production / Setup Belt Surface Physics (repeatable, undoable).
    /// The tunnel setups call <see cref="ApplyToBelt"/> for the belts they create.
    /// </summary>
    public static class BeltPhysicsSetup
    {
        private const string MaterialFolder = "Assets/Physics";
        private const string MaterialPath = MaterialFolder + "/PM_BeltSurface.physicMaterial";

        [MenuItem("Tools/Food Production/Setup Belt Surface Physics")]
        public static void Setup()
        {
            PhysicsMaterial material = LoadOrCreateMaterial();
            var done = new HashSet<Transform>();
            int count = 0;

            foreach (ConveyorBelt belt in Object.FindObjectsByType<ConveyorBelt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Transform root = belt.transform.root;
                if (done.Add(root) && ApplyToBelt(root, material))
                {
                    count++;
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Setup Belt Surface Physics",
                $"{count} Bandoberflächen haben jetzt das reibungsfreie Material {MaterialPath}.\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>Assigns the frictionless material to the belt surface collider(s) below <paramref name="beltRoot"/>.</summary>
        public static bool ApplyToBelt(Transform beltRoot, PhysicsMaterial material = null)
        {
            material ??= LoadOrCreateMaterial();
            bool applied = false;
            foreach (BoxCollider box in beltRoot.GetComponentsInChildren<BoxCollider>(true))
            {
                if (box.isTrigger || box.name != "Cube")
                {
                    continue;
                }

                Undo.RecordObject(box, "Belt surface material");
                box.sharedMaterial = material;
                applied = true;
            }
            return applied;
        }

        private static PhysicsMaterial LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            material = new PhysicsMaterial("PM_BeltSurface")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
