using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Lights the production hall (Construction/Production, x 0..60, z -10..10, ceiling at 6 m):
    /// three lighting tracks along the line (z -6 / 0 / +6, the middle one is the existing "Lighting tracks")
    /// with 8 LED bar lamps each, evenly spaced so the first and last lamp sit flush with the track ends.
    /// Every lamp is an instance of the shared prefab <see cref="PrefabPath"/> (emissive housing + downward spot light).
    ///
    /// Why 24 lights without shadows: WebGL uses the Mobile quality level (Forward, max 32 visible lights incl.
    /// the directional light, no additional light shadows). Wide, overlapping cones keep the floor even although
    /// Forward only takes the 4 strongest lights per object; on PC (Forward+) all of them count.
    ///
    /// Menu: Tools / Food Production / Setup Hall Lighting (repeatable: rebuilds "Hall Lamps", undoable).
    /// The prefab is only created when missing, so edits to it are kept. After hand-tuning the layout use
    /// Tools / Food Production / Relayout Hall Lamps to change the lamp count per track instead.
    /// </summary>
    public static class HallLightingSetup
    {
        private const string LightRootPath = "Construction/Production/Light";
        private const string TrackPath = LightRootPath + "/Lighting tracks/Cube";
        private const string LampRootName = "Hall Lamps";
        private const string MaterialPath = "Assets/Materials/Hall Lamp.mat";
        private const string PrefabPath = "Assets/Prefabs/Decorations/Hall Lamp.prefab";
        private const string LampGroupPrefabPath = "Assets/Prefabs/Decorations/Hall Lamps.prefab";
        private const string FloorPath = "Construction/Ground Hygiene";
        private const string FloorTilesName = "Ground Tiles";
        private const float FloorTileSize = 5f;
        private const int LampsPerTrack = 8;
        private const float HousingLength = 1.4f;

        private static readonly float[] TrackZ = { -6f, 0f, 6f };
        private static readonly Color LampColor = new Color(1f, 0.95f, 0.87f); // ~4500 K, neutral white

        [MenuItem("Tools/Food Production/Setup Hall Lighting")]
        public static void Setup()
        {
            GameObject lightRoot = GameObject.Find(LightRootPath);
            GameObject track = GameObject.Find(TrackPath);
            if (lightRoot == null || track == null)
            {
                SetupUi.Dialog("Setup Hall Lighting", $"Nicht gefunden: {LightRootPath} bzw. {TrackPath}", "OK");
                return;
            }

            Transform old = lightRoot.transform.Find(LampRootName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var root = new GameObject(LampRootName);
            Undo.RegisterCreatedObjectUndo(root, "Setup Hall Lighting");
            root.transform.SetParent(lightRoot.transform, false);
            root.transform.position = Vector3.zero;

            GameObject lampPrefab = LoadOrCreatePrefab();
            Bounds trackBounds = track.GetComponent<Renderer>().bounds;
            float firstX = trackBounds.min.x + HousingLength / 2f;
            float lastX = trackBounds.max.x - HousingLength / 2f;
            float spacing = (lastX - firstX) / (LampsPerTrack - 1);

            foreach (float z in TrackZ)
            {
                if (!Mathf.Approximately(z, track.transform.position.z))
                {
                    GameObject copy = Object.Instantiate(track, root.transform);
                    copy.name = $"Track z{z:+0;-0}";
                    copy.transform.position = new Vector3(track.transform.position.x, track.transform.position.y, z);
                }

                for (int i = 0; i < LampsPerTrack; i++)
                {
                    var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, root.transform);
                    lamp.transform.position = new Vector3(firstX + i * spacing, trackBounds.min.y, z);
                    lamp.name = $"Hall Lamp z{z:+0;-0} #{i + 1}";
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Setup Hall Lighting",
                $"{TrackZ.Length * LampsPerTrack} Hallenleuchten ({PrefabPath}) unter {LightRootPath}/{LampRootName} erzeugt.\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>
        /// Sets every track under "Hall Lamps" to <see cref="LampsPerTrack"/> lamps without rebuilding the
        /// hand-tuned layout: the first and last lamp keep their position (and scale/rotation), surplus lamps
        /// are removed, missing ones are added, and all are spaced evenly in between.
        /// </summary>
        [MenuItem("Tools/Food Production/Relayout Hall Lamps")]
        public static void Relayout()
        {
            GameObject root = GameObject.Find(LightRootPath + "/" + LampRootName);
            if (root == null)
            {
                SetupUi.Dialog("Relayout Hall Lamps", $"Nicht gefunden: {LightRootPath}/{LampRootName}", "OK");
                return;
            }

            GameObject lampPrefab = LoadOrCreatePrefab();
            int total = 0;
            foreach (Transform track in root.transform)
            {
                List<Transform> lamps = track.Cast<Transform>()
                    .Where(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) == lampPrefab)
                    .OrderBy(t => t.position.x)
                    .ToList();
                if (lamps.Count < 2)
                {
                    continue;
                }

                Vector3 first = lamps[0].position;
                Vector3 last = lamps[lamps.Count - 1].position;
                Transform template = lamps[0];

                while (lamps.Count > LampsPerTrack)
                {
                    // Remove from the inside so the end lamps stay.
                    Transform surplus = lamps[lamps.Count - 2];
                    lamps.RemoveAt(lamps.Count - 2);
                    Undo.DestroyObjectImmediate(surplus.gameObject);
                }
                while (lamps.Count < LampsPerTrack)
                {
                    var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab, track);
                    Undo.RegisterCreatedObjectUndo(lamp, "Relayout Hall Lamps");
                    lamp.transform.localRotation = template.localRotation;
                    lamp.transform.localScale = template.localScale;
                    lamps.Insert(lamps.Count - 1, lamp.transform);
                }

                string prefix = template.name.Contains("#") ? template.name.Substring(0, template.name.IndexOf('#')) : "Hall Lamp ";
                for (int i = 0; i < lamps.Count; i++)
                {
                    Undo.RecordObject(lamps[i], "Relayout Hall Lamps");
                    Undo.RecordObject(lamps[i].gameObject, "Relayout Hall Lamps");
                    lamps[i].position = Vector3.Lerp(first, last, i / (float)(lamps.Count - 1));
                    lamps[i].name = $"{prefix}#{i + 1}";
                    lamps[i].SetSiblingIndex(i);
                }
                total += lamps.Count;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Relayout Hall Lamps",
                $"{total} Hallenleuchten ({LampsPerTrack} pro Schiene) gleichmäßig verteilt.\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>
        /// Realtime-only hall lighting: Baked lights do nothing without baked lightmaps (the scene has none and
        /// no static objects), and extra realtime directional lights would take all 4 per-object light slots of
        /// the WebGL (Mobile/Forward) renderer, hiding the hall lamps. So the fill directional lights under
        /// <see cref="LightRootPath"/> are deactivated, the even "light from all sides" comes from a Trilight
        /// ambient, the sun stays the only directional light and the lamp prefab is set back to Realtime.
        /// If the lamp group is missing in the scene, the "Hall Lamps" prefab is placed under the light root again.
        /// </summary>
        [MenuItem("Tools/Food Production/Apply Realtime Hall Lighting")]
        public static void ApplyRealtime()
        {
            GameObject lightRoot = GameObject.Find(LightRootPath);
            int disabled = 0;
            if (lightRoot != null)
            {
                foreach (Light fill in lightRoot.GetComponentsInChildren<Light>(true))
                {
                    if (fill.type == LightType.Directional && fill.gameObject.activeSelf)
                    {
                        Undo.RecordObject(fill.gameObject, "Apply Realtime Hall Lighting");
                        fill.gameObject.SetActive(false);
                        disabled++;
                    }
                }
            }

            bool lampsAdded = false;
            var lampGroup = AssetDatabase.LoadAssetAtPath<GameObject>(LampGroupPrefabPath);
            if (lightRoot != null && lampGroup != null && lightRoot.transform.Find(LampRootName) == null)
            {
                // Prefab root sits at local x -30 under "Light" (x 30), i.e. world origin like the original layout.
                var group = (GameObject)PrefabUtility.InstantiatePrefab(lampGroup, lightRoot.transform);
                Undo.RegisterCreatedObjectUndo(group, "Apply Realtime Hall Lighting");
                lampsAdded = true;
            }

            Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.activeInHierarchy);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.64f);     // from the bright ceiling
            RenderSettings.ambientEquatorColor = new Color(0.50f, 0.49f, 0.48f); // walls
            RenderSettings.ambientGroundColor = new Color(0.30f, 0.27f, 0.27f);  // red floor bounce

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            foreach (Light lamp in prefabRoot.GetComponentsInChildren<Light>(true))
            {
                lamp.lightmapBakeType = LightmapBakeType.Realtime;
            }
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Apply Realtime Hall Lighting",
                $"{disabled} Fülllichter deaktiviert, {(lampsAdded ? "Hall Lamps wieder eingesetzt, " : "")}Umgebungslicht Trilight, Sonne: {(sun != null ? sun.name : "keine")}, Hallenleuchten Realtime.\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>
        /// WebGL (Mobile quality, Forward) lights each renderer with only its most important additional lights,
        /// chosen from the renderer's bounds centre. The floor was one 100 x 30 m plane centred at x 10, so the
        /// floor around the player stayed dark in the browser. The floor's look is now drawn by 5 x 5 m quads
        /// (same material, no colliders) and the original renderer is switched off; its MeshCollider stays.
        /// Forward+ would avoid the per-object limit, but URP Forward+ is reported to freeze in WebGL 2.
        /// </summary>
        [MenuItem("Tools/Food Production/Split Floor For Lighting")]
        public static void SplitFloor()
        {
            GameObject floor = GameObject.Find(FloorPath);
            if (floor == null || !floor.TryGetComponent(out MeshRenderer floorRenderer))
            {
                SetupUi.Dialog("Split Floor For Lighting", $"Nicht gefunden: {FloorPath}", "OK");
                return;
            }

            Transform old = floor.transform.parent.Find(FloorTilesName);
            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            var root = new GameObject(FloorTilesName);
            Undo.RegisterCreatedObjectUndo(root, "Split Floor For Lighting");
            root.transform.SetParent(floor.transform.parent, false);
            root.layer = floor.layer;

            Bounds bounds = floorRenderer.bounds;
            int countX = Mathf.CeilToInt(bounds.size.x / FloorTileSize);
            int countZ = Mathf.CeilToInt(bounds.size.z / FloorTileSize);
            float sizeX = bounds.size.x / countX;
            float sizeZ = bounds.size.z / countZ;
            for (int x = 0; x < countX; x++)
            {
                for (int z = 0; z < countZ; z++)
                {
                    GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    Object.DestroyImmediate(tile.GetComponent<Collider>());
                    tile.name = $"Tile {x}-{z}";
                    tile.layer = floor.layer;
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.SetPositionAndRotation(
                        new Vector3(bounds.min.x + (x + 0.5f) * sizeX, bounds.max.y, bounds.min.z + (z + 0.5f) * sizeZ),
                        Quaternion.Euler(90f, 0f, 0f));
                    tile.transform.localScale = new Vector3(sizeX, sizeZ, 1f);
                    var renderer = tile.GetComponent<MeshRenderer>();
                    renderer.sharedMaterials = floorRenderer.sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = floorRenderer.receiveShadows;
                }
            }

            Undo.RecordObject(floorRenderer, "Split Floor For Lighting");
            floorRenderer.enabled = false;

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            SetupUi.Dialog("Split Floor For Lighting",
                $"Boden in {countX * countZ} Kacheln ({sizeX:0.#} x {sizeZ:0.#} m) unter {FloorTilesName} geteilt, Original-Renderer aus (Collider bleibt).\nSzene speichern (Strg+S).", "OK");
        }

        /// <summary>Switches the editor to the quality level WebGL builds use (Mobile), or back to PC, to preview lighting.</summary>
        [MenuItem("Tools/Food Production/Preview Quality: WebGL (Mobile)")]
        public static void PreviewWebGlQuality() => SetQuality("Mobile");

        [MenuItem("Tools/Food Production/Preview Quality: PC")]
        public static void PreviewPcQuality() => SetQuality("PC");

        private static void SetQuality(string levelName)
        {
            int index = System.Array.IndexOf(QualitySettings.names, levelName);
            if (index < 0)
            {
                SetupUi.Dialog("Preview Quality", $"Qualitätsstufe nicht gefunden: {levelName}", "OK");
                return;
            }
            QualitySettings.SetQualityLevel(index, true);
            Debug.Log($"[Preview Quality] {levelName} ({QualitySettings.renderPipeline?.name})");
        }

        private static GameObject LoadOrCreatePrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                return prefab;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject lamp = BuildLamp(LoadOrCreateMaterial());
            prefab = PrefabUtility.SaveAsPrefabAsset(lamp, PrefabPath);
            Object.DestroyImmediate(lamp);
            return prefab;
        }

        /// <summary>Lamp pivot is the mount point on the track underside; housing and light hang below it.</summary>
        private static GameObject BuildLamp(Material material)
        {
            var lamp = new GameObject("Hall Lamp");

            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Housing";
            Object.DestroyImmediate(housing.GetComponent<Collider>());
            housing.transform.SetParent(lamp.transform, false);
            housing.transform.localPosition = new Vector3(0f, -0.04f, 0f);
            housing.transform.localScale = new Vector3(HousingLength, 0.08f, 0.22f);
            var renderer = housing.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var lightObject = new GameObject("Spot Light");
            lightObject.transform.SetParent(lamp.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            lightObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = LampColor;
            light.intensity = 30f;
            light.range = 12f;
            light.spotAngle = 140f;
            light.innerSpotAngle = 70f;
            light.shadows = LightShadows.None;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            return lamp;
        }

        private static Material LoadOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.SetColor("_BaseColor", Color.white);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", LampColor * 6f);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
