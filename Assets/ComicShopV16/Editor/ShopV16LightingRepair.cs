using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ComicShopV16
{
    public static class ShopV16LightingRepair
    {
        [MenuItem("Tools/ComicShop/Rebuild Active Scene Lighting")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before rebuilding lighting.");
            var scene = SceneManager.GetActiveScene();
            var shops = new System.Collections.Generic.List<ShopV16Appearance>();
            foreach (var root in scene.GetRootGameObjects())
                shops.AddRange(root.GetComponentsInChildren<ShopV16Appearance>(true));
            if (shops.Count != 1 || shops[0].sources == null || shops[0].sources.Length == 0)
                throw new InvalidOperationException("Expected one imported ShopV16Appearance in the active scene.");
            var shop = shops[0];
            if (shop.directional == null || shop.parameters == null || shop.colors == null ||
                shop.directional.Length < shop.sources.Length ||
                shop.parameters.Length < shop.sources.Length || shop.colors.Length < shop.sources.Length)
                throw new InvalidOperationException("Incomplete imported light metadata.");

            // Save a copy including unsaved scene edits. Never overwrite the source scene.
            const string backupFolder = "Assets/LightingBackups";
            Directory.CreateDirectory(backupFolder);
            AssetDatabase.Refresh();
            string backup = AssetDatabase.GenerateUniqueAssetPath(
                backupFolder + "/" + scene.name + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Could not back up the scene; no lighting was changed.");

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Rebuild ComicShop lighting");
            // Rebuilding intentionally retires both duplicate suns and prior auto-generated lamps.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var light in root.GetComponentsInChildren<Light>(true))
                {
                    bool imported = light.transform.IsChildOf(shop.transform);
                    bool generated = light.name == "ComicShop Pendant Spot";
                    bool duplicateSun = light.type == LightType.Directional && light.name == "Directional Light";
                    if (!imported && !generated && !duplicateSun) continue;
                    Undo.RecordObject(light, "Disable previous light");
                    light.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                }

            Light sun = null;
            int count = 0;
            for (int i = 0; i < shop.sources.Length; i++)
            {
                var source = shop.sources[i];
                if (!source || source.gameObject.scene != scene) continue;
                bool directional = shop.directional[i];
                if (directional && shop.parameters[i].z < .5f) continue;
                var light = source.GetComponent<Light>();
                if (!light) light = Undo.AddComponent<Light>(source.gameObject);
                Undo.RecordObject(light, "Configure shop light");
                Undo.RecordObject(source, "Orient pendant");
                Undo.RecordObject(source.gameObject, "Enable source");
                source.gameObject.SetActive(true);
                bool pendant = !directional && shop.parameters[i].x >= 10f;
                bool neon = !directional && shop.colors[i].x > shop.colors[i].y * 2f;
                ShopV16Appearance.ConfigureLight(light, directional, pendant, neon,
                    directional || i == 3 || i == 6);
                if (directional) sun = light;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                PrefabUtility.RecordPrefabInstancePropertyModifications(source);
                PrefabUtility.RecordPrefabInstancePropertyModifications(source.gameObject);
                count++;
            }

            // RenderSettings is scene-scoped. Save-copy above also backs these values up.
            RenderSettings.sun = sun;
            RenderSettings.fog = false;
            // Clone material assets so the backup scene keeps its original preset values.
            string materialFolder = "Assets/ComicShopV16/RelitMaterials";
            Directory.CreateDirectory(materialFolder);
            AssetDatabase.Refresh();
            var replacements = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var renderer in shop.GetComponentsInChildren<MeshRenderer>(true))
            {
                var assigned = renderer.sharedMaterials;
                bool changed = false;
                for (int j = 0; j < assigned.Length; j++)
                {
                    Material original = assigned[j];
                    if (!original || !original.shader || original.shader.name != "ComicShop/ToonLit") continue;
                    if (!replacements.TryGetValue(original, out Material replacement))
                    {
                        string path = AssetDatabase.GetAssetPath(original);
                        if (path.StartsWith(materialFolder + "/", StringComparison.Ordinal)) continue;
                        string guid = AssetDatabase.AssetPathToGUID(path);
                        if (string.IsNullOrEmpty(guid)) guid = original.GetInstanceID().ToString();
                        string destination = materialFolder + "/Warm-" + guid + ".mat";
                        replacement = AssetDatabase.LoadAssetAtPath<Material>(destination);
                        if (!replacement)
                        {
                            replacement = new Material(original);
                            replacement.name = original.name + " Relit";
                            replacement.SetFloat("_ShadowFloor", .32f);
                            replacement.SetFloat("_BakedInfluence", .08f);
                            AssetDatabase.CreateAsset(replacement, destination);
                        }
                        replacements.Add(original, replacement);
                    }
                    assigned[j] = replacement;
                    changed = true;
                }
                if (!changed) continue;
                Undo.RecordObject(renderer, "Assign relit material");
                renderer.sharedMaterials = assigned;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(scene);
            SceneView.RepaintAll();
            Debug.Log("ComicShop lighting rebuilt: " + count +
                " real lights. Scene backup: " + backup +
                ". Toon materials received separate warm lighting presets. Save the scene after checking Game view.", shop);
        }
    }
}
