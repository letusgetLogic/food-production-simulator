using System.IO;
using System.Linq;
using Game.Production;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.EditorTools
{
    /// <summary>
    /// Adds a closed frozen-pizza carton to the pizza prefab and registers it in <see cref="PizzaStateVisual"/> as the
    /// visual of <see cref="ProductState.PackagedPizza"/>.
    ///
    /// The carton is built from primitives (the Kenney "pizza-box" model is an open take-away box with the lid up):
    /// cardboard body + round print on the lid. Footprint = baked pizza + 6 %, height 12 % of the footprint, standing
    /// on the bottom of the baked pizza. The root "PackagedBox" carries a solid BoxCollider - like every state visual,
    /// because the pizza visuals and their MeshColliders are switched off while packaged. The ContactCollider stays.
    ///
    /// Creates Assets/Materials/Packaging/M_Carton*.mat on first run.
    /// Menu: Tools / Food Production / Setup Pizza Box Visual (repeatable - an existing carton is replaced).
    /// </summary>
    public static class PizzaBoxVisualSetup
    {
        private const string PizzaPrefabPath = "Assets/Prefabs/Objects/pizza.prefab";
        private const string MaterialFolder = "Assets/Materials/Packaging";
        private const string BoxName = "PackagedBox";
        private const float FootprintMargin = 1.06f;
        private const float HeightRatio = 0.12f;

        [MenuItem("Tools/Food Production/Setup Pizza Box Visual")]
        public static void Setup()
        {
            Material cardboard = LoadOrCreateMaterial("M_Carton", new Color(0.78f, 0.60f, 0.40f), 0.1f);
            Material print = LoadOrCreateMaterial("M_CartonPrint", new Color(0.78f, 0.16f, 0.13f), 0.35f);
            Material printLight = LoadOrCreateMaterial("M_CartonPrintLight", new Color(0.96f, 0.90f, 0.76f), 0.35f);

            GameObject root = PrefabUtility.LoadPrefabContents(PizzaPrefabPath);
            try
            {
                var visual = root.GetComponent<PizzaStateVisual>();
                var serialized = new SerializedObject(visual);
                SerializedProperty entries = serialized.FindProperty("_stateVisuals");

                // Remove a previous run (carton object + its list entry).
                for (int i = entries.arraySize - 1; i >= 0; i--)
                {
                    if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("State").intValue == (int)ProductState.PackagedPizza)
                    {
                        entries.DeleteArrayElementAtIndex(i);
                    }
                }
                Transform oldBox = root.transform.Find(BoxName);
                if (oldBox != null)
                {
                    Object.DestroyImmediate(oldBox.gameObject);
                }

                GameObject[] baked = GetVisuals(entries, ProductState.BakedPizza);
                if (baked.Length == 0 || !TryGetLocalBounds(root.transform, baked, out Bounds pizza))
                {
                    SetupUi.Dialog("Setup Pizza Box Visual", "Kein BakedPizza-Visual mit Mesh im Pizza-Prefab gefunden.", "OK");
                    return;
                }

                float side = Mathf.Max(pizza.size.x, pizza.size.z) * FootprintMargin;
                float height = side * HeightRatio;

                var box = new GameObject(BoxName);
                box.transform.SetParent(root.transform, false);
                box.transform.localPosition = new Vector3(pizza.center.x, pizza.min.y, pizza.center.z);

                var solid = box.AddComponent<BoxCollider>();
                solid.center = new Vector3(0f, height * 0.5f, 0f);
                solid.size = new Vector3(side, height, side);

                // Body, then the lid print slightly above the lid (round label + light inner disc).
                AddPart(box.transform, PrimitiveType.Cube, "Carton", cardboard,
                    new Vector3(0f, height * 0.5f, 0f), new Vector3(side, height, side));
                AddPart(box.transform, PrimitiveType.Cylinder, "Print", print,
                    new Vector3(0f, height + 0.0015f, 0f), new Vector3(side * 0.70f, 0.001f, side * 0.70f));
                AddPart(box.transform, PrimitiveType.Cylinder, "PrintInner", printLight,
                    new Vector3(0f, height + 0.003f, 0f), new Vector3(side * 0.52f, 0.001f, side * 0.52f));
                AddPart(box.transform, PrimitiveType.Cube, "Band", print,
                    new Vector3(0f, height * 0.5f, 0f), new Vector3(side * 1.004f, height * 0.35f, side * 1.004f));
                box.SetActive(false);

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("State").intValue = (int)ProductState.PackagedPizza;
                SerializedProperty visuals = entry.FindPropertyRelative("Visuals");
                visuals.arraySize = 1;
                visuals.GetArrayElementAtIndex(0).objectReferenceValue = box;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PizzaPrefabPath);
                SetupUi.Dialog("Setup Pizza Box Visual",
                    $"Karton im Pizza-Prefab ergänzt (Zustand PackagedPizza).\n" +
                    $"Pizza {Fmt(pizza.size)} → Karton {side:0.000} × {height:0.000} × {side:0.000} (Prefab-Einheiten).", "OK");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddPart(Transform parent, PrimitiveType type, string name, Material material, Vector3 localPosition, Vector3 size)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            // Unity's cylinder is 2 units high - halve Y so "size" is the real height.
            part.transform.localScale = type == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (name != "Carton")
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Material LoadOrCreateMaterial(string name, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }
            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static GameObject[] GetVisuals(SerializedProperty entries, ProductState state)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("State").intValue != (int)state)
                {
                    continue;
                }

                SerializedProperty visuals = entry.FindPropertyRelative("Visuals");
                return Enumerable.Range(0, visuals.arraySize)
                    .Select(v => visuals.GetArrayElementAtIndex(v).objectReferenceValue as GameObject)
                    .Where(go => go != null)
                    .ToArray();
            }
            return new GameObject[0];
        }

        /// <summary>Mesh bounds in <paramref name="space"/> coordinates - works for inactive objects (Renderer.bounds does not).</summary>
        private static bool TryGetLocalBounds(Transform space, GameObject[] objects, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (MeshFilter filter in objects.SelectMany(o => o.GetComponentsInChildren<MeshFilter>(true)))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                    Vector3 point = toSpace.MultiplyPoint3x4(local);
                    if (!any)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(point);
                    }
                }
            }
            return any;
        }

        private static string Fmt(Vector3 v) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.000} × {1:0.000} × {2:0.000}", v.x, v.y, v.z);
    }
}
