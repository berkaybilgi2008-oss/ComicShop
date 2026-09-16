using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ComicShopV16
{
    public static class ShopV16RecoverAmbience
    {
        struct Pane { public MeshRenderer renderer; public Bounds bounds; public Vector3 inward; public float area; }

        [MenuItem("Tools/ComicShop/Recover Ambience and Follow Pendants")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            ShopV16Appearance shop = null;
            var pendants = new List<Light>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var s in root.GetComponentsInChildren<ShopV16Appearance>(true))
                {
                    if (shop) throw new InvalidOperationException("Expected one V16 shop.");
                    shop = s;
                }
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.name == "ComicShop Pendant Spot" && l.transform.parent &&
                        l.transform.parent.gameObject.activeInHierarchy) pendants.Add(l);
            }
            if (!shop || pendants.Count == 0)
                throw new InvalidOperationException("No shop or pendant-attached lights found. Nothing changed.");
            MeshFilter floor = null;
            foreach (var f in shop.GetComponentsInChildren<MeshFilter>(true))
                if (f.name == "Mesh_1" && f.sharedMesh && f.sharedMesh.name == "G_0_1") floor = f;
            if (!floor) throw new InvalidOperationException("V16 floor reference missing.");
            var space = floor.transform.parent;
            Bounds footprint = BoundsIn(floor, space);
            var panes = new List<Pane>();
            foreach (var f in space.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = f.GetComponent<MeshRenderer>();
                if (!f.sharedMesh || !r || !r.enabled || !r.gameObject.activeInHierarchy ||
                    !Transparent(r)) continue;
                Bounds b = BoundsIn(f, space);
                if (b.size.y < .8f || b.max.y > footprint.center.y + 5f) continue;
                float xDistance = Mathf.Min(Mathf.Abs(b.center.x-footprint.min.x), Mathf.Abs(b.center.x-footprint.max.x));
                float zDistance = Mathf.Min(Mathf.Abs(b.center.z-footprint.min.z), Mathf.Abs(b.center.z-footprint.max.z));
                bool xWall = b.size.x < .4f && xDistance < .7f && b.size.z > .6f;
                bool zWall = b.size.z < .4f && zDistance < .7f && b.size.x > .6f;
                if (!xWall && !zWall) continue;
                Vector3 inward = xWall ? new Vector3(b.center.x < footprint.center.x ? 1 : -1,0,0)
                    : new Vector3(0,0,b.center.z < footprint.center.z ? 1 : -1);
                panes.Add(new Pane { renderer = r, bounds = b, inward = inward,
                    area = b.size.y * (xWall ? b.size.z : b.size.x) });
            }

            Directory.CreateDirectory("Assets/LightingBackups");
            AssetDatabase.Refresh();
            string backup = AssetDatabase.GenerateUniqueAssetPath("Assets/LightingBackups/" +
                scene.name + "-Ambience-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity");
            if (!EditorSceneManager.SaveScene(scene, backup, true))
                throw new IOException("Backup failed. Nothing changed.");
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Recover shop ambience");
            try
            {
                Light sun = null;
                if (shop.sources != null)
                    for (int i = 0; i < shop.sources.Length; i++)
                    {
                        var source = shop.sources[i];
                        if (!source) continue;
                        var l = source.GetComponent<Light>();
                        if (!l) continue;
                        if (l.type == LightType.Directional && shop.parameters != null &&
                            i < shop.parameters.Length && shop.parameters[i].z > .5f) sun = l;
                        else if (l.type == LightType.Spot)
                        {
                            Undo.RecordObject(l, "Disable detached source light");
                            l.enabled = false;
                            Record(l);
                        }
                    }
                // Positions come from current visible pendant groups, never the import source array.
                int aligned = 0;
                foreach (var l in pendants)
                {
                    Transform parent = l.transform.parent;
                    Bounds b = default;
                    bool found = false;
                    foreach (var r in parent.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (!r.enabled || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly ||
                            !r.gameObject.activeInHierarchy) continue;
                        if (!found) { b = r.bounds; found = true; } else b.Encapsulate(r.bounds);
                    }
                    if (!found) continue;
                    Undo.RecordObject(l.transform, "Align to current pendant");
                    Undo.RecordObject(l.gameObject, "Enable pendant light");
                    Undo.RecordObject(l, "Restore pendant lighting");
                    l.gameObject.SetActive(true);
                    l.transform.position = new Vector3(b.center.x, b.min.y-.06f, b.center.z);
                    l.transform.rotation = Quaternion.LookRotation(-space.up, space.forward);
                    l.enabled = true;
                    l.type = LightType.Spot;
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                    l.color = Color.white;
                    l.useColorTemperature = true;
                    l.colorTemperature = 3600;
                    l.intensity = .65f;
                    l.range = 7f;
                    l.spotAngle = 120;
                    l.innerSpotAngle = 95;
                    l.shadows = aligned < 2 ? LightShadows.Hard : LightShadows.None;
                    l.shadowNormalBias = .06f;
                    l.bounceIntensity = 0;
                    Record(l); Record(l.transform); Record(l.gameObject);
                    aligned++;
                }

                foreach (var pane in panes)
                {
                    Undo.RecordObject(pane.renderer, "Let sunlight through glass");
                    pane.renderer.shadowCastingMode = ShadowCastingMode.Off;
                    Record(pane.renderer);
                }
                if (sun && panes.Count > 0)
                {
                    Pane biggest = panes[0];
                    foreach (var p in panes) if (p.area > biggest.area) biggest = p;
                    Undo.RecordObject(sun, "Window-facing warm daylight");
                    Undo.RecordObject(sun.transform, "Aim sun through window");
                    // Approximately 10 degrees down from the horizon, from the actual glazed facade.
                    sun.transform.rotation = Quaternion.LookRotation(
                        space.TransformDirection(biggest.inward - Vector3.up*.18f).normalized, space.up);
                    sun.enabled = true;
                    sun.useColorTemperature = true;
                    sun.colorTemperature = 4600;
                    sun.color = Color.white;
                    sun.intensity = 1.15f;
                    sun.shadows = LightShadows.Hard;
                    sun.shadowNormalBias = .06f;
                    Record(sun); Record(sun.transform);
                }

                int cards = 0;
                var replacement = new Dictionary<Material, Material>();
                const string folder = "Assets/ComicShopV16/RecoveredMaterials";
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
                foreach (var r in shop.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var materials = r.sharedMaterials;
                    // Verified V16 M_193: additive, unlit floor-light decal, not a physical lamp.
                    bool oldCard = false;
                    foreach (var m in materials)
                        if (m && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(m)) ==
                            "133abf9e61ddc7840aebd209f408bd7f") oldCard = true;
                    if (oldCard)
                    {
                        Undo.RecordObject(r, "Hide fixed light decal");
                        r.enabled = false; Record(r); cards++; continue;
                    }
                    if (r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                    bool changed = false;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var m = materials[i];
                        if (!m || !m.shader || m.shader.name != "ComicShop/ToonLit") continue;
                        string path = AssetDatabase.GetAssetPath(m);
                        if (path.StartsWith(folder+"/", StringComparison.Ordinal)) continue;
                        if (!replacement.TryGetValue(m, out Material copy))
                        {
                            string id = AssetDatabase.AssetPathToGUID(path);
                            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
                            string destination = folder + "/Warm-" + id + ".mat";
                            copy = AssetDatabase.LoadAssetAtPath<Material>(destination);
                            if (!copy)
                            {
                                copy = new Material(m) { name = m.name + " Ambient" };
                                copy.SetFloat("_ShadowFloor", .55f);
                                copy.SetFloat("_BakedInfluence", .08f);
                                copy.SetFloat("_ShadowSteps", 3f);
                                AssetDatabase.CreateAsset(copy, destination);
                            }
                            replacement.Add(m, copy);
                        }
                        materials[i] = copy; changed = true;
                    }
                    if (changed)
                    {
                        Undo.RecordObject(r, "Assign readable warm shadows");
                        r.sharedMaterials = materials; Record(r);
                    }
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                SceneView.RepaintAll();
                Debug.Log("Ambience recovered. Aligned pendants: " + aligned + "; glass panes: " +
                    panes.Count + "; fixed light decals hidden: " + cards + ". Backup: " + backup);
                if (panes.Count == 0)
                    Debug.LogWarning("No boundary glass identified: sun direction was preserved. Window geometry needs inspection.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }

        static void Record(UnityEngine.Object value)
        {
            EditorUtility.SetDirty(value);
            PrefabUtility.RecordPrefabInstancePropertyModifications(value);
        }
        static bool Transparent(MeshRenderer r)
        {
            foreach (var m in r.sharedMaterials)
                if (m && ((m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < .99f) ||
                    m.GetTag("RenderType", false, "") == "Transparent")) return true;
            return false;
        }
        static Bounds BoundsIn(MeshFilter f, Transform space)
        {
            Bounds b = f.sharedMesh.bounds;
            Matrix4x4 m = space.worldToLocalMatrix * f.transform.localToWorldMatrix;
            Bounds result = new Bounds(m.MultiplyPoint3x4(b.center), Vector3.zero);
            for (int i = 0; i < 8; i++) result.Encapsulate(m.MultiplyPoint3x4(b.center +
                Vector3.Scale(b.extents, new Vector3((i&1)==0?-1:1, (i&2)==0?-1:1, (i&4)==0?-1:1))));
            return result;
        }
    }
}
