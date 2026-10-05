using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Production;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Moving belt visuals: puts a flat quad with a scrolling stripe texture (<see cref="ConveyorSurfaceScroll"/>)
    /// on the surface of every <see cref="ConveyorBelt"/> in the open scene.
    ///
    /// - Surface = the belt's non-trigger "Cube" collider (sibling of BeltDrive). The quad lies 2 mm above it,
    ///   has no collider and no shadow - products still rest on the Cube collider.
    /// - Buffer belts (<see cref="AccumulationZone"/>) get one quad per slot, so waiting products lie on a standing
    ///   section while the free sections keep running. The Kenney roller mesh (conveyor-bars-*) of a buffer belt
    ///   is swapped for the flat 2 m belt mesh of the transport belts, otherwise the roller tops would poke through.
    /// - Creates Assets/Materials/Conveyor/T_BeltSurface.png + M_BeltSurface.mat on first run.
    ///
    /// Menu: Tools / Food Production / Setup Moving Belt Visuals (repeatable - old quads are replaced, undoable).
    /// </summary>
    public static class ConveyorVisualSetup
    {
        private const string Folder = "Assets/Materials/Conveyor";
        private const string TexturePath = Folder + "/T_BeltSurface.png";
        private const string MaterialPath = Folder + "/M_BeltSurface.mat";
        private const string FlatBeltMeshName = "conveyor-long-stripe";

        public const string VisualRootName = "BeltSurfaceVisual";

        /// <summary>Share of the collider width covered by the moving surface (the rest is the dark side rim).</summary>
        private const float WidthFraction = 0.84f;
        private const float HeightAboveSurface = 0.002f;
        private const float StripeSpacingMeters = 0.25f;

        [MenuItem("Tools/Food Production/Setup Moving Belt Visuals")]
        public static void Setup()
        {
            Material material = LoadOrCreateMaterial();
            int belts = 0;
            int quads = 0;
            var skipped = new List<string>();

            foreach (ConveyorBelt belt in Object.FindObjectsByType<ConveyorBelt>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int created = ApplyToBelt(belt, material);
                if (created > 0)
                {
                    belts++;
                    quads += created;
                }
                else
                {
                    skipped.Add(belt.transform.parent != null ? belt.transform.parent.name : belt.name);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            string skippedText = skipped.Count > 0 ? "\nÜbersprungen (keine Oberfläche \"Cube\" gefunden): " + string.Join(", ", skipped) : string.Empty;
            SetupUi.Dialog("Setup Moving Belt Visuals",
                $"{belts} Bänder haben jetzt eine laufende Oberfläche ({quads} Segmente).{skippedText}\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>Creates (or replaces) the scrolling surface of one belt. Returns the number of quads created.</summary>
        public static int ApplyToBelt(ConveyorBelt belt, Material material = null)
        {
            material ??= LoadOrCreateMaterial();
            Transform beltRoot = belt.transform.parent;
            if (beltRoot == null)
            {
                return 0;
            }

            BoxCollider surface = FindSurface(beltRoot);
            if (surface == null)
            {
                return 0;
            }

            Transform old = beltRoot.Find(VisualRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            Vector3 direction = Vector3.ProjectOnPlane(belt.TravelDirection, Vector3.up).normalized;
            if (direction.sqrMagnitude < 0.5f)
            {
                return 0;
            }

            Bounds bounds = surface.bounds;
            float length = Mathf.Abs(direction.x) * bounds.size.x + Mathf.Abs(direction.z) * bounds.size.z;
            float width = (Mathf.Abs(direction.z) * bounds.size.x + Mathf.Abs(direction.x) * bounds.size.z) * WidthFraction;

            var visualRoot = new GameObject(VisualRootName);
            Undo.RegisterCreatedObjectUndo(visualRoot, "Moving belt visual");
            visualRoot.transform.SetParent(beltRoot, false);
            visualRoot.transform.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.max.y + HeightAboveSurface, bounds.center.z),
                Quaternion.LookRotation(direction, Vector3.up));

            var segments = new List<(int index, float from, float to)>();
            AccumulationZone zone = belt.AccumulationZone;
            if (zone != null && zone.Config != null)
            {
                SwapRollerMesh(beltRoot);
                BuildSlotSegments(zone, visualRoot.transform, length, segments);
            }
            else
            {
                segments.Add((-1, -length * 0.5f, length * 0.5f));
            }

            foreach ((int index, float from, float to) in segments)
            {
                CreateQuad(visualRoot.transform, belt, material, index, from, to, width);
            }
            return segments.Count;
        }

        private static BoxCollider FindSurface(Transform beltRoot)
        {
            // Direct children only - a machine root can hold several belts further down.
            foreach (Transform child in beltRoot)
            {
                if (child.name == "Cube" && child.TryGetComponent(out BoxCollider box) && !box.isTrigger)
                {
                    return box;
                }
            }
            return null;
        }

        /// <summary>One section per slot (0 = rear), free belt length before/after the slots runs with the motor.</summary>
        private static void BuildSlotSegments(AccumulationZone zone, Transform visualRoot, float length,
            List<(int index, float from, float to)> segments)
        {
            float half = length * 0.5f;
            float slotLength = zone.Config.SlotLengthMeters;
            var slots = new List<(int index, float from, float to)>();
            for (int i = 0; i < zone.Capacity; i++)
            {
                float center = visualRoot.InverseTransformPoint(zone.GetSlotCenter(i)).z;
                float from = Mathf.Max(-half, center - slotLength * 0.5f);
                float to = Mathf.Min(half, center + slotLength * 0.5f);
                if (to - from > 0.01f)
                {
                    slots.Add((i, from, to));
                }
            }

            slots.Sort((a, b) => a.from.CompareTo(b.from));
            float cursor = -half;
            foreach (var slot in slots)
            {
                if (slot.from - cursor > 0.02f)
                {
                    segments.Add((-1, cursor, slot.from));
                }
                segments.Add(slot);
                cursor = Mathf.Max(cursor, slot.to);
            }
            if (half - cursor > 0.02f)
            {
                segments.Add((-1, cursor, half));
            }
        }

        private static void CreateQuad(Transform parent, ConveyorBelt belt, Material material, int segmentIndex,
            float from, float to, float width)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = segmentIndex < 0 ? "Surface" : $"Surface Slot {segmentIndex}";
            quad.transform.SetParent(parent, false);
            // Quad faces -Z: +90° around X lays it flat facing up, its V axis (local Y) then points along +Z = travel.
            quad.transform.localPosition = new Vector3(0f, 0f, (from + to) * 0.5f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(width, to - from, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            quad.AddComponent<ConveyorSurfaceScroll>().Configure(belt, segmentIndex, to - from, StripeSpacingMeters);
        }

        /// <summary>
        /// Buffer belt: the roller mesh (conveyor-bars-sides, rollers stick 2 cm above the surface collider) is replaced
        /// by the flat 2 m belt mesh the transport belts use - same mesh, local rotation/scale and height as a transport
        /// belt in the scene, so the moving surface sits flush on it.
        /// </summary>
        private static void SwapRollerMesh(Transform beltRoot)
        {
            MeshFilter reference = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.sharedMesh != null && f.sharedMesh.name == FlatBeltMeshName && f.transform.parent != beltRoot);
            if (reference == null)
            {
                Debug.LogWarning($"[Setup Moving Belt Visuals] No transport belt mesh '{FlatBeltMeshName}' in the scene - buffer keeps its rollers.");
                return;
            }

            foreach (Transform child in beltRoot)
            {
                if (child.name == VisualRootName || !child.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null
                    || !filter.sharedMesh.name.StartsWith("conveyor-") || filter.sharedMesh == reference.sharedMesh)
                {
                    continue;
                }

                Undo.RecordObject(filter, "Flat buffer belt mesh");
                filter.sharedMesh = reference.sharedMesh;
                Undo.RecordObject(child, "Flat buffer belt mesh");
                child.rotation = reference.transform.rotation;
                child.localScale = reference.transform.localScale;
                child.position = new Vector3(child.position.x, reference.transform.position.y + (beltRoot.position.y - reference.transform.parent.position.y), child.position.z);
                Undo.RecordObject(child.gameObject, "Flat buffer belt mesh");
                child.name = FlatBeltMeshName;
            }
        }

        // ---- Assets ----

        private static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            Texture2D texture = LoadOrCreateTexture();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = "M_BeltSurface", mainTexture = texture };
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.15f);
            }
            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        /// <summary>One stripe period (V axis = travel direction): rubber belt with a darker cross cleat.</summary>
        private static Texture2D LoadOrCreateTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (existing != null)
            {
                return existing;
            }

            const int width = 16;
            const int height = 64;
            var belt = new Color32(116, 119, 146, 255);
            var cleat = new Color32(72, 74, 94, 255);
            var highlight = new Color32(136, 139, 168, 255);

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                // rows 0-9 cleat, row 10 lit edge of the cleat, rest belt
                Color32 color = y < 10 ? cleat : y < 12 ? highlight : belt;
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }
    }
}
