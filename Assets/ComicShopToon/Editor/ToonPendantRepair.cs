using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ComicShop.Rendering.Editor
{
    public static class ToonPendantRepair
    {
        const string Label = "Repair existing pendant lighting";
        [MenuItem("Tools/ComicShop/Repair Existing Pendant Lighting (Undo)")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            var lamps = new List<Light>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var l in root.GetComponentsInChildren<Light>(true))
                    if (l.name == "ComicShop Pendant Spot" && l.transform.parent &&
                        l.transform.parent.gameObject.activeInHierarchy) lamps.Add(l);
            if (lamps.Count == 0) { Debug.LogError("No active pendant fixtures found. Nothing changed."); return; }
            lamps.Sort((a,b) => string.CompareOrdinal(Path(a.transform), Path(b.transform)));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(Label);
            int enabledBefore = 0, repaired = 0, skipped = 0, cards = 0;
            var seen = new HashSet<Transform>();
            try
            {
                foreach (var l in lamps)
                {
                    if (l.enabled && l.gameObject.activeInHierarchy) enabledBefore++;
                    var fixture = l.transform.parent;
                    if (!seen.Add(fixture)) { Undo.RecordObject(l, Label); l.enabled = false; Record(l); continue; }
                    Bounds bounds = default;
                    bool found = false;
                    foreach (var r in fixture.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (!r.enabled || !r.gameObject.activeInHierarchy || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                        if (!found) { bounds = r.bounds; found = true; } else bounds.Encapsulate(r.bounds);
                    }
                    // Refuse room-sized parents: never align a lamp to a whole building.
                    if (!found || bounds.size.x > 4 || bounds.size.z > 4 || bounds.size.y > 5)
                    { skipped++; Debug.LogWarning("Skipped ambiguous fixture: " + Path(fixture), fixture); continue; }
                    Undo.RecordObjects(new UnityEngine.Object[] { l, l.transform, l.gameObject }, Label);
                    l.gameObject.SetActive(true);
                    l.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.08f, bounds.center.z);
                    l.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                    l.enabled = true;
                    l.type = LightType.Spot;
                    l.lightmapBakeType = LightmapBakeType.Realtime;
                    l.color = Color.white; l.useColorTemperature = true; l.colorTemperature = 3400;
                    l.lightUnit = LightUnit.Candela; l.enableSpotReflector = false;
                    l.intensity = 24; l.range = 9; l.spotAngle = 110; l.innerSpotAngle = 85;
                    l.cullingMask = ~0;
                    // One 256px spot map per fixture fits up to 64 lamps into a 2048 atlas.
                    // Keep every enabled lamp shadowed so wall-facing cones cannot leak.
                    l.shadows = LightShadows.Hard; l.shadowStrength = 1;
                    l.shadowBias = 0.05f; l.shadowNormalBias = 0.06f; l.shadowNearPlane = 0.05f;
                    l.shadowCustomResolution = 256;
                    var so = new SerializedObject(l);
                    Set(so, "m_UseViewFrustumForShadowCasterCull", false);
                    Set(so, "m_UseBoundingSphereOverride", false);
                    Set(so, "m_UseCullingMatrixOverride", false);
                    so.ApplyModifiedProperties();
                    var extra = l.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
                    if (extra)
                    {
                        Undo.RecordObject(extra, Label);
                        var data = new SerializedObject(extra);
                        Set(data,"m_UsePipelineSettings",false);
                        Set(data,"m_AdditionalLightsShadowResolutionTier",0);
                        data.ApplyModifiedProperties(); Record(extra);
                    }
                    Record(l); Record(l.transform); Record(l.gameObject); repaired++;
                }
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                            if (material && AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(material)) == "133abf9e61ddc7840aebd209f408bd7f")
                            { Undo.RecordObject(renderer, Label); renderer.enabled = false; Record(renderer); cards++; break; }
                if (repaired == 0) throw new InvalidOperationException("No fixture could be aligned; changes reverted.");
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var light in root.GetComponentsInChildren<Light>(true))
                    {
                        Undo.RecordObject(light, Label);
                        var lightData = new SerializedObject(light);
                        Set(lightData, "m_UseViewFrustumForShadowCasterCull", false);
                        lightData.ApplyModifiedProperties(); Record(light);
                    }
                    foreach (var shop in root.GetComponentsInChildren<ComicShopV16.ShopV16Appearance>(true))
                    {
                        if (shop.sources == null || shop.parameters == null) continue;
                        for (int i = 0; i < Mathf.Min(shop.sources.Length, shop.parameters.Length); i++)
                        {
                            if (!shop.sources[i] || shop.parameters[i].x < 10) continue;
                            var detached = shop.sources[i].GetComponent<Light>();
                            if (!detached || lamps.Contains(detached)) continue;
                            Undo.RecordObject(detached, Label); detached.enabled = false; Record(detached);
                        }
                    }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid));
                    Undo.RecordObject(asset, Label); var so = new SerializedObject(asset);
                    Set(so,"m_AdditionalLightsRenderingMode",1);
                    Set(so,"m_AdditionalLightShadowsSupported",true);
                    Set(so,"m_AdditionalLightsShadowmapResolution",2048);
                    Set(so,"m_AdditionalLightsShadowResolutionTierLow",256);
                    Set(so,"m_ShadowDistance",60f);
                    Set(so,"m_GPUResidentDrawerMode",0);
                    so.ApplyModifiedProperties(); EditorUtility.SetDirty(asset);
                }
                foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid));
                    Undo.RecordObject(asset, Label); var so = new SerializedObject(asset);
                    Set(so,"m_RenderingMode",2); so.ApplyModifiedProperties(); EditorUtility.SetDirty(asset);
                }
                var style = ToonStyleController.ActiveStyle;
                Undo.RecordObject(style, Label); style.HalftoneEnabled = 0;
                style.LightFalloffScale = 1; style.ShadowLift = Mathf.Max(style.ShadowLift,0.12f);
                EditorUtility.SetDirty(style); ToonStyleController.PublishActive();
                EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets(); SceneView.RepaintAll();
                Debug.Log($"[PENDANT REPAIR] Before: {enabledBefore} enabled; repaired: {repaired}; skipped: {skipped}; old light cards hidden: {cards}. Save scene. All repaired spots face world-down. Real-time lighting: no bake required. Profile shadow cost before shipping.");
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }
        static string Path(Transform t) => t.parent ? Path(t.parent) + "/" + t.name + "[" + t.GetSiblingIndex() + "]" : t.name;
        static void Record(UnityEngine.Object o)
        { EditorUtility.SetDirty(o); if (PrefabUtility.IsPartOfPrefabInstance(o)) PrefabUtility.RecordPrefabInstancePropertyModifications(o); }
        static void Set(SerializedObject so, string name, bool value)
        { var p = so.FindProperty(name); if (p != null) p.boolValue = value; }
        static void Set(SerializedObject so, string name, int value)
        { var p = so.FindProperty(name); if (p != null) p.intValue = value; }
        static void Set(SerializedObject so, string name, float value)
        { var p = so.FindProperty(name); if (p != null) p.floatValue = value; }
    }
}
