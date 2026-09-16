using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ComicShopV16
{
    public static class ShopV16SealLighting
    {
        const string ShellName = "ComicShop Shadow Shell";
        const string MaterialPath = "Assets/ComicShopV16/ShadowShell.mat";

        [MenuItem("Tools/ComicShop/Seal Light Leaks and Warm Sunset")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var scene = SceneManager.GetActiveScene();
            ShopV16Appearance shop = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<ShopV16Appearance>(true))
                {
                    if (shop) throw new InvalidOperationException("Expected one V16 shop.");
                    shop = candidate;
                }
            if (!shop) throw new InvalidOperationException("No V16 shop in the active scene.");

            // These two authored nodes are the full floor and ceiling planes in the V16 import.
            MeshFilter floor = null, ceiling = null;
            foreach (var filter in shop.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "Mesh_1" && filter.sharedMesh &&
                    filter.sharedMesh.name == "G_0_1") floor = filter;
            if (!floor) throw new InvalidOperationException("V16 floor anchor not found; nothing changed.");
            Transform space = floor.transform.parent;
            foreach (var filter in space.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name == "Mesh_2" && filter.transform.parent == space)
                    ceiling = filter;
            if (!ceiling || !ceiling.sharedMesh)
                throw new InvalidOperationException("V16 ceiling anchor not found; nothing changed.");
            Bounds footprint = InSpace(floor, space);
            Bounds roof = InSpace(ceiling, space);
            if (footprint.size.x < 5 || footprint.size.z < 5 || roof.center.y <= footprint.center.y + 2)
                throw new InvalidOperationException("Unexpected V16 architecture dimensions; nothing changed.");

            Shader shader = Shader.Find("ComicShop/V16 Source Toon");
            if (!shader) throw new InvalidOperationException("V16 shader is missing.");
            Directory.CreateDirectory("Assets/LightingBackups");
            AssetDatabase.Refresh();
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/LightingBackups/" +
                scene.name + "-Seal-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Scene backup failed; nothing changed.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Seal shop lighting");
            Transform previous = space.Find(ShellName);
            if (previous) Undo.DestroyObjectImmediate(previous.gameObject);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "Shadow Shell" };
                material.SetFloat("_Cull", 0);
                material.SetFloat("_Cutoff", 0);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var shell = new GameObject(ShellName);
            Undo.RegisterCreatedObjectUndo(shell, "Create shadow shell");
            shell.transform.SetParent(space, false);

            // Closed boxes block sunlight from either side but never enter the camera color pass.
            Box(shell.transform, material, "Roof",
                new Vector3(footprint.center.x, roof.center.y + .10f, footprint.center.z),
                new Vector3(footprint.size.x + .4f, .2f, footprint.size.z + .4f), floor.gameObject.layer);
            int walls = 0;
            foreach (var filter in space.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || filter.transform.IsChildOf(shell.transform) ||
                    filter.sharedMesh.vertexCount > 24) continue;
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !filter.gameObject.activeInHierarchy ||
                    !Solid(renderer)) continue;
                Bounds b = InSpace(filter, space);
                if (b.min.y < footprint.center.y - .2f || b.max.y > roof.center.y + .25f ||
                    b.size.y < .15f) continue;
                bool side = b.size.x < .35f && b.size.z > .8f &&
                    Mathf.Min(Mathf.Abs(b.center.x-footprint.min.x),
                              Mathf.Abs(b.center.x-footprint.max.x)) < .2f;
                bool end = b.size.z < .35f && b.size.x > .8f &&
                    Mathf.Min(Mathf.Abs(b.center.z-footprint.min.z),
                              Mathf.Abs(b.center.z-footprint.max.z)) < .2f;
                if (!side && !end) continue;
                Vector3 size = b.size;
                // Only add thickness perpendicular to the wall. Never expand across a window opening.
                if (side) size.x = Mathf.Max(.18f, size.x);
                if (end) size.z = Mathf.Max(.18f, size.z);
                Box(shell.transform, material, "Wall " + walls++, b.center, size, filter.gameObject.layer);
            }

            Light sun = null;
            if (shop.sources != null && shop.directional != null && shop.parameters != null)
                for (int i = 0; i < shop.sources.Length && i < shop.directional.Length &&
                    i < shop.parameters.Length; i++)
                    if (shop.sources[i] && shop.directional[i] && shop.parameters[i].z > .5f)
                        sun = shop.sources[i].GetComponent<Light>();
            if (sun)
            {
                Undo.RecordObject(sun, "Warm sunset");
                sun.useColorTemperature = true;
                sun.colorTemperature = 4200f;
                sun.color = Color.white;
                sun.intensity = .95f;
                sun.shadows = LightShadows.Hard;
                sun.shadowStrength = 1;
                // Keep the authored low window-facing sun direction; do not rotate through solid walls.
                PrefabUtility.RecordPrefabInstancePropertyModifications(sun);
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            Debug.Log("Shadow-only roof and " + walls + " wall panels created. Backup: " + backup +
                ". Window glass was excluded. Save after checking Game view.", shell);
            if (walls == 0)
                Debug.LogWarning("No matching boundary wall panels found. Roof sealed; wall leaks need geometry inspection.", shell);
        }

        static bool Solid(MeshRenderer renderer)
        {
            if (renderer.name == "Ink") return false;
            foreach (var m in renderer.sharedMaterials)
            {
                if (!m || !m.shader || m.renderQueue >= 2500 ||
                    m.GetTag("RenderType", false, "") == "Transparent") return false;
                if (m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < .99f) return false;
                if (m.HasProperty("_ZWrite") && m.GetFloat("_ZWrite") < .5f) return false;
                if (m.HasProperty("_Unlit") && m.GetFloat("_Unlit") > .5f) return false;
            }
            return renderer.sharedMaterials.Length > 0;
        }

        static Bounds InSpace(MeshFilter filter, Transform space)
        {
            Bounds source = filter.sharedMesh.bounds;
            Matrix4x4 matrix = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(source.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = source.center + Vector3.Scale(source.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return result;
        }

        static void Box(Transform parent, Material material, string name, Vector3 position,
            Vector3 size, int layer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create light blocker");
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
