#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ComicShop.Rendering.Editor
{
    public static class ComicShopPendantSetup
    {
        const string Menu = "Tools/ComicShop/Light Matching Pendants";
        const string LightName = "ComicShop Pendant Spot";
        [MenuItem(Menu, true)]
        static bool Validate() => Selection.activeGameObject != null && !EditorApplication.isPlayingOrWillChangePlaymode
            && PrefabStageUtility.GetCurrentPrefabStage() == null;

        static Mesh[] Meshes(GameObject go) => go.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null).Select(f => f.sharedMesh).ToArray();
        static string Signature(Mesh[] meshes)
        {
            // No CPU vertex reads; works when imported meshes have Read/Write disabled.
            return string.Join("|", meshes.Select(m =>
            {
                Vector3 b = m.bounds.size;
                return m.vertexCount + ":" + m.subMeshCount + ":" +
                    Mathf.RoundToInt(b.x * 100) + ":" + Mathf.RoundToInt(b.y * 100) + ":" + Mathf.RoundToInt(b.z * 100);
            }).OrderBy(s => s, StringComparer.Ordinal));
        }
        static bool BoundsOf(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }
        [MenuItem(Menu)]
        static void Apply()
        {
            GameObject sample = Selection.activeGameObject;
            if (sample == null || !sample.scene.IsValid()) return;
            Mesh[] sampleMeshes = Meshes(sample);
            if (sampleMeshes.Length == 0 || !BoundsOf(sample, out Bounds sampleBounds))
            {
                EditorUtility.DisplayDialog("ComicShop", "Select the complete pendant group, for example Group_2278.", "OK");
                return;
            }
            string signature = Signature(sampleMeshes);
            var matches = new List<GameObject>();
            // Exact structural/mesh-size matching, not a guess based on Group_* names.
            foreach (GameObject root in sample.scene.GetRootGameObjects())
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (!candidate.gameObject.activeInHierarchy) continue;
                Mesh[] meshes = Meshes(candidate.gameObject);
                if (meshes.Length != sampleMeshes.Length || Signature(meshes) != signature) continue;
                if (!BoundsOf(candidate.gameObject, out Bounds bounds)) continue;
                if ((bounds.size - sampleBounds.size).magnitude > Mathf.Max(.03f, sampleBounds.size.magnitude * .08f)) continue;
                matches.Add(candidate.gameObject);
            }
            // Ignore wrapper parents around the same visual model.
            matches = matches.Where(a => !matches.Any(b => a != b && b.transform.IsChildOf(a.transform))).ToList();
            if (matches.Count == 0) matches.Add(sample);
            if (matches.Count > 32)
            {
                EditorUtility.DisplayDialog("ComicShop", "More than 32 matches: selection is too generic. Select the whole pendant group. Nothing changed.", "OK");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Light ComicShop Pendants");
            int created = 0;
            try
            {
                foreach (GameObject pendant in matches)
                {
                    if (!BoundsOf(pendant, out Bounds bounds)) continue;
                    Transform child = pendant.transform.Find(LightName);
                    GameObject go;
                    if (child == null)
                    {
                        go = new GameObject(LightName);
                        SceneManager.MoveGameObjectToScene(go, sample.scene);
                        Undo.RegisterCreatedObjectUndo(go, "Create Pendant Light");
                        Undo.SetTransformParent(go.transform, pendant.transform, "Parent Pendant Light");
                    }
                    else go = child.gameObject;
                    Undo.RecordObject(go.transform, "Position Pendant Light");
                    go.transform.position = new Vector3(bounds.center.x, bounds.min.y - .08f, bounds.center.z);
                    go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    Light light = go.GetComponent<Light>();
                    if (light == null) light = Undo.AddComponent<Light>(go);
                    Undo.RecordObject(light, "Configure Pendant Light");
                    light.enabled = true;
                    light.type = LightType.Spot;
                    light.color = Color.white;
                    light.useColorTemperature = true;
                    light.colorTemperature = 3200;
                    light.intensity = 1.2f;
                    light.range = 7;
                    light.spotAngle = 110;
                    light.innerSpotAngle = 80;
                    light.lightmapBakeType = LightmapBakeType.Realtime;
                    light.shadows = LightShadows.None;
                    light.bounceIntensity = 0;
                    light.cullingMask = -1;
                    EditorUtility.SetDirty(light);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    created++;
                }
                // Keep the strongest enabled sun and its direction; remove competing direct fill.
                var directionals = sample.scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Light>(true))
                    .Where(l => l.enabled && l.gameObject.activeInHierarchy && l.type == LightType.Directional
                        && l.lightmapBakeType == LightmapBakeType.Realtime)
                    .OrderByDescending(l => l.intensity).ToArray();
                for (int i = 0; i < directionals.Length; i++)
                {
                    Light light = directionals[i];
                    Undo.RecordObject(light, "Remove Competing Sun Fill");
                    if (i == 0) light.shadows = LightShadows.Hard;
                    else light.enabled = false;
                    EditorUtility.SetDirty(light);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                }
                EditorSceneManager.MarkSceneDirty(sample.scene);
                Selection.objects = matches.ToArray();
                SceneView.RepaintAll();
                Debug.Log($"ComicShop: {created} pendants lit. Matching models selected for inspection. Ctrl+Z restores changes. Realtime-only: no bake required. Existing Mixed/Baked suns were preserved.");
                EditorUtility.DisplayDialog("ComicShop", $"{created} pendant lights configured.\n\nMatching models are selected. Check the Game view.\nCtrl+Z restores everything. Scene has not been saved.", "OK");
            }
            catch (Exception error)
            {
                Undo.RevertAllDownToGroup(group);
                Debug.LogException(error);
            }
            finally { Undo.CollapseUndoOperations(group); }
        }
    }
}
#endif
