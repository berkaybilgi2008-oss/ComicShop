using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ComicShop.Rendering.Editor
{
    public static class ToonArchitectureRepair
    {
        const string Menu = "Tools/ComicShop/Step 2/";
        const string RootName = "LightBlockers";
        sealed class Analysis { public int boundary, nonManifold; public bool failed, planar; public string reason; }
        [MenuItem(Menu + "Create or Select Room Repair Setup")]
        public static void Setup()
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var room in root.GetComponentsInChildren<ToonRoom>(true))
            { Selection.activeGameObject = room.gameObject; return; }
            var go = new GameObject("Toon Room Repair");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Create room repair settings");
            go.AddComponent<ToonRoom>();
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(scene);
        }
        static ToonRoom Room()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run room tools outside Play mode.");
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var room in root.GetComponentsInChildren<ToonRoom>(true)) return room;
            throw new InvalidOperationException("Run Create or Select Room Repair Setup first.");
        }
        [MenuItem(Menu + "Mark Selected Architecture Hierarchies")]
        static void Mark()
        {
            foreach (var go in Selection.gameObjects)
                if (go.scene.IsValid() && go.GetComponent<ToonArchitecture>() == null)
                { Undo.AddComponent<ToonArchitecture>(go); EditorSceneManager.MarkSceneDirty(go.scene); }
        }
        internal static bool IsArchitecture(MeshRenderer r, ToonRoom room)
        {
            if (r.transform.IsChildOf(room.transform)) return false;
            for (Transform t = r.transform; t != null; t = t.parent)
            {
                if (t.GetComponent<ToonArchitecture>() != null) return true;
                if ((room.architectureLayers.value & (1 << t.gameObject.layer)) != 0) return true;
                if (!string.IsNullOrEmpty(room.architectureTag) && t.tag == room.architectureTag) return true;
                string name = t.name.ToLowerInvariant();
                if (room.includeArchitectureNames && (name.Contains("wall") || name.Contains("ceiling") || name.Contains("roof") || name.Contains("duvar") || name.Contains("tavan"))) return true;
            }
            return false;
        }
        static List<MeshRenderer> FindArchitecture(ToonRoom room)
        {
            var found = new List<MeshRenderer>();
            foreach (var root in room.gameObject.scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                if (IsArchitecture(r, room) && r.TryGetComponent<MeshFilter>(out var f) && f.sharedMesh != null) found.Add(r);
            return found;
        }
        static Analysis Analyze(Mesh mesh)
        {
            var result = new Analysis();
            try
            {
                using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                using (var vertices = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
                {
                    var md = data[0]; md.GetVertices(vertices);
                    float epsilon = Mathf.Max(1e-6f, mesh.bounds.size.magnitude * 1e-6f);
                    if (vertices.Length >= 3)
                    {
                        Vector3 planeNormal = Vector3.zero;
                        for (int j = 2; j < vertices.Length && planeNormal.sqrMagnitude < 1e-12f; j++)
                            planeNormal = Vector3.Cross(vertices[1] - vertices[0], vertices[j] - vertices[0]);
                        result.planar = planeNormal.sqrMagnitude > 1e-12f;
                        planeNormal.Normalize();
                        for (int j = 0; j < vertices.Length && result.planar; j++)
                            if (Mathf.Abs(Vector3.Dot(planeNormal, vertices[j] - vertices[0])) > epsilon * 2) result.planar = false;
                    }
                    var welded = new Dictionary<Vector3Int, int>();
                    var ids = new int[vertices.Length];
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 p = vertices[i];
                        var key = new Vector3Int(Mathf.RoundToInt(p.x / epsilon), Mathf.RoundToInt(p.y / epsilon), Mathf.RoundToInt(p.z / epsilon));
                        if (!welded.TryGetValue(key, out int id)) { id = welded.Count; welded.Add(key, id); }
                        ids[i] = id;
                    }
                    var edges = new Dictionary<ulong, int>();
                    void Edge(int a, int b)
                    {
                        a = ids[a]; b = ids[b]; if (a == b) return;
                        ulong key = ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                        edges.TryGetValue(key, out int count); edges[key] = count + 1;
                    }
                    for (int sub = 0; sub < md.subMeshCount; sub++)
                    {
                        var desc = md.GetSubMesh(sub);
                        if (desc.topology != MeshTopology.Triangles) continue;
                        using (var indices = new NativeArray<int>(desc.indexCount, Allocator.Temp))
                        {
                            md.GetIndices(indices, sub, true);
                            for (int j = 0; j + 2 < indices.Length; j += 3)
                            { Edge(indices[j], indices[j + 1]); Edge(indices[j + 1], indices[j + 2]); Edge(indices[j + 2], indices[j]); }
                        }
                    }
                    foreach (var count in edges.Values) { if (count == 1) result.boundary++; else if (count != 2) result.nonManifold++; }
                }
            }
            catch (Exception e) { result.failed = true; result.reason = e.Message; }
            return result;
        }
        [MenuItem(Menu + "1 - Report Architecture and Open Meshes")]
        public static void Report() => Scan(false);
        [MenuItem(Menu + "2 - Repair Architecture Shadow Casting (Undo)")]
        public static void Repair() => Scan(true);
        static void Scan(bool repair)
        {
            var room = Room(); var renderers = FindArchitecture(room);
            var cache = new Dictionary<Mesh, Analysis>();
            int open = 0, failed = 0, changed = 0, before = 0, noCaster = 0;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            foreach (var r in renderers)
            {
                bool missingCaster = false;
                foreach (var material in r.sharedMaterials)
                    if (material != null && material.FindPass("ShadowCaster") < 0) missingCaster = true;
                if (missingCaster) { noCaster++; Debug.LogWarning($"{r.name} has a material without a named ShadowCaster pass; TwoSided alone cannot add a missing pass.", r); }
                Mesh mesh = r.GetComponent<MeshFilter>().sharedMesh;
                if (!cache.TryGetValue(mesh, out Analysis a)) { a = Analyze(mesh); cache.Add(mesh, a); }
                if (r.shadowCastingMode == ShadowCastingMode.TwoSided) before++;
                if (a.failed) { failed++; Debug.LogWarning($"Architecture analysis failed: {r.name}: {a.reason}", r); }
                else if (a.planar || a.boundary > 0 || a.nonManifold > 0)
                { open++; Debug.Log($"Open/single-skin candidate: {r.name}, planar={a.planar}, boundary edges={a.boundary}, non-manifold={a.nonManifold}, cast={r.shadowCastingMode}", r); }
                // All identified architecture is repaired conservatively: topology cannot
                // prove normal orientation, material culling or intent on arbitrary meshes.
                if (!repair || r.shadowCastingMode == ShadowCastingMode.ShadowsOnly || r.shadowCastingMode == ShadowCastingMode.TwoSided) continue;
                Undo.RecordObject(r, "Two-sided architecture shadows");
                r.shadowCastingMode = ShadowCastingMode.TwoSided;
                PrefabUtility.RecordPrefabInstancePropertyModifications(r); EditorUtility.SetDirty(r); changed++;
            }
            Undo.CollapseUndoOperations(group);
            if (changed > 0) EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log($"Architecture summary: scanned={renderers.Count}, open/single-skin candidates={open}, unreadable/failed={failed}, missing caster={noCaster}; TwoSided before={before}, changed={changed}, after={before + changed}. ShadowsOnly preserved. Open topology is evidence, not a proof of one-sided material culling.");
        }
        [MenuItem(Menu + "Fit Room Bounds to Identified Architecture")]
        static void Fit()
        {
            var room = Room(); var renderers = FindArchitecture(room);
            if (renderers.Count == 0) { Debug.LogWarning("No architecture identified. Mark architecture hierarchy roots or set a layer/tag."); return; }
            Matrix4x4 inverse = Matrix4x4.TRS(room.transform.position, room.transform.rotation, Vector3.one).inverse;
            bool first = true; Bounds bounds = default;
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                var b = mesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = inverse.MultiplyPoint3x4(renderer.localToWorldMatrix.MultiplyPoint3x4(p));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            Undo.RecordObject(room, "Fit room bounds"); room.roomBounds = bounds; EditorUtility.SetDirty(room);
            EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log($"Room bounds fitted from {renderers.Count} renderers. Verify cyan bounds and yellow shopfront opening before generating the shell.", room);
        }
        [MenuItem(Menu + "3 - Generate or Rebuild LightBlockers (Undo)")]
        public static void GenerateShell()
        {
            var room = Room();
            if (room.transform.lossyScale != Vector3.one) throw new InvalidOperationException("Room setup must have world scale 1,1,1. Rotation is supported.");
            Bounds b = room.roomBounds; Vector3 min = b.min, max = b.max;
            if (b.size.x < 0.5f || b.size.y < 0.5f || b.size.z < 0.5f) throw new InvalidOperationException("Room bounds are degenerate.");
            float gap = Mathf.Max(0.001f, room.shellOffset), thickness = Mathf.Max(0.05f, room.shellThickness);
            float reach = gap + thickness;
            bool alongX = room.shopfront == ToonRoom.Facade.NegativeZ || room.shopfront == ToonRoom.Facade.PositiveZ;
            float facadeWidth = alongX ? b.size.x : b.size.z;
            float left = room.openingCenter.x - room.openingSize.x * 0.5f, right = room.openingCenter.x + room.openingSize.x * 0.5f;
            float bottom = min.y + room.openingCenter.y - room.openingSize.y * 0.5f, top = bottom + room.openingSize.y;
            if (room.preserveShopfrontOpening && (room.openingSize.x <= 0 || room.openingSize.y <= 0 || left < -facadeWidth/2 || right > facadeWidth/2 || bottom < min.y || top > max.y))
                throw new InvalidOperationException("The shopfront opening must fit inside the room facade. Check its yellow gizmo.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ComicShopToon/Resources/ComicShopToon/LightBlocker.mat");
            if (material == null) throw new InvalidOperationException("LightBlocker.mat is missing.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Transform existing = room.transform.Find(RootName);
            int before = existing != null ? existing.GetComponentsInChildren<MeshRenderer>(true).Length : 0;
            if (existing != null) Undo.DestroyObjectImmediate(existing.gameObject);
            var root = new GameObject(RootName); root.transform.SetParent(room.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create blocker shell");
            int count = 0;
            void Box(string name, Vector3 center, Vector3 size)
            {
                if (size.x < 0.0001f || size.y < 0.0001f || size.z < 0.0001f) return;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
                go.transform.SetParent(root.transform, false); go.transform.localPosition = center; go.transform.localScale = size;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.renderingLayerMask = uint.MaxValue;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
                Undo.RegisterCreatedObjectUndo(go, "Create blocker slab"); count++;
            }
            Box("Ceiling", new Vector3(b.center.x, max.y + gap + thickness/2, b.center.z), new Vector3(b.size.x + 2*reach, thickness, b.size.z + 2*reach));
            if (room.blockFloor) Box("Foundation", new Vector3(b.center.x, min.y - gap - thickness/2, b.center.z), new Vector3(b.size.x + 2*reach, thickness, b.size.z + 2*reach));
            foreach (ToonRoom.Facade side in Enum.GetValues(typeof(ToonRoom.Facade)))
            {
                bool x = side == ToonRoom.Facade.NegativeZ || side == ToonRoom.Facade.PositiveZ;
                bool positive = side == ToonRoom.Facade.PositiveZ || side == ToonRoom.Facade.PositiveX;
                float width = x ? b.size.x : b.size.z;
                void Wall(string suffix, float lo, float hi, float lowY, float highY)
                {
                    Vector3 center = b.center; center.y = (lowY + highY)/2;
                    if (x) { center.x += (lo + hi)/2; center.z = (positive ? max.z : min.z) + (positive ? 1 : -1)*(gap + thickness/2); }
                    else { center.z += (lo + hi)/2; center.x = (positive ? max.x : min.x) + (positive ? 1 : -1)*(gap + thickness/2); }
                    Box(side + suffix, center, x ? new Vector3(hi-lo, highY-lowY, thickness) : new Vector3(thickness, highY-lowY, hi-lo));
                }
                if (side != room.shopfront || !room.preserveShopfrontOpening) Wall("", -width/2-reach, width/2+reach, min.y-reach, max.y+reach);
                else
                {
                    Wall(" Left", -width/2-reach, left, min.y-reach, max.y+reach);
                    Wall(" Right", right, width/2+reach, min.y-reach, max.y+reach);
                    Wall(" Below", left, right, min.y-reach, bottom);
                    Wall(" Above", left, right, top, max.y+reach);
                }
            }
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log($"LightBlockers before={before}, after={count}; gap={gap}m, thickness={thickness}m. Shopfront opening preserved={room.preserveShopfrontOpening}. Re-bake lightmaps/APV; real-time blockers cannot remove previously baked light.", root);
        }
        [MenuItem(Menu + "4 - Apply Room Shadow Baseline (Undo)")]
        static void ShadowBaseline()
        {
            var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null) throw new InvalidOperationException("Active render pipeline is not URP.");
            Undo.RecordObject(asset, "Room shadow baseline");
            asset.shadowDepthBias = 0.1f; asset.shadowNormalBias = 0.2f;
            asset.shadowDistance = 35f; asset.shadowCascadeCount = 2; asset.cascade2Split = 0.35f;
            // URP 17.5 exposes an internal setter, so edit the verified serialized field.
            var serialized = new SerializedObject(asset);
            var softShadows = serialized.FindProperty("m_SoftShadowsSupported");
            if (softShadows == null) throw new InvalidOperationException("URP soft-shadow serialized field not found.");
            softShadows.boolValue = false; serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            int changed = 0; var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.shadows == LightShadows.None) continue;
                Undo.RecordObject(light, "Use hard shadows"); light.shadows = LightShadows.Hard;
                var data = light.GetComponent<UniversalAdditionalLightData>();
                if (data != null) { Undo.RecordObject(data, "Use URP shadow bias"); data.usePipelineSettings = true; EditorUtility.SetDirty(data); PrefabUtility.RecordPrefabInstancePropertyModifications(data); }
                light.shadowBias = 0.1f; light.shadowNormalBias = 0.2f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(light); changed++;
            }
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
            Debug.Log($"URP baseline: bias=0.1/0.2, distance=35m, 2 cascades at 0.35, soft shadows disabled; updated {changed} existing shadow-casting lights. Geometry gaps still require the shell.");
        }
        [MenuItem(Menu + "5 - Disable Directional Lights for Interior Test (Undo)")]
        static void DisableSun()
        {
            int changed = 0; var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var light in root.GetComponentsInChildren<Light>(true))
                if (light.type == LightType.Directional && light.enabled)
                { Undo.RecordObject(light, "Disable interior sun"); light.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(light); changed++; }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"Disabled {changed} directional lights. A darker room is expected. Existing baked sun/GI requires re-baking; this does not erase baked illumination.");
        }
        [MenuItem(Menu + "6 - Create Shopfront Daylight Test Rig (Undo)")]
        static void DaylightRig()
        {
            var room = Room();
            if (!room.preserveShopfrontOpening) throw new InvalidOperationException("Enable and verify the shopfront opening first.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            const string name = "Shopfront Daylight Test";
            Transform old = room.transform.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject(name); root.transform.SetParent(room.transform, false);
            Undo.RegisterCreatedObjectUndo(root, "Create daylight test rig");
            bool x = room.shopfront == ToonRoom.Facade.NegativeZ || room.shopfront == ToonRoom.Facade.PositiveZ;
            bool positive = room.shopfront == ToonRoom.Facade.PositiveZ || room.shopfront == ToonRoom.Facade.PositiveX;
            Vector3 inward = x ? Vector3.forward : Vector3.right;
            if (positive) inward = -inward;
            Bounds b = room.roomBounds;
            float boundary = x ? (positive ? b.max.z : b.min.z) : (positive ? b.max.x : b.min.x);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("Shopfront Spot " + (i + 1)); go.transform.SetParent(root.transform, false);
                Vector3 pos = b.center;
                pos.y = b.min.y + room.openingCenter.y + room.openingSize.y * 0.3f;
                float horizontal = room.openingCenter.x + (i == 0 ? -1 : 1) * room.openingSize.x * 0.25f;
                if (x) { pos.x += horizontal; pos.z = boundary; } else { pos.z += horizontal; pos.x = boundary; }
                go.transform.localPosition = pos - inward * 0.3f;
                go.transform.localRotation = Quaternion.LookRotation((inward + Vector3.down * 0.15f).normalized, Vector3.up);
                var light = go.AddComponent<Light>();
                light.type = LightType.Spot; light.lightmapBakeType = LightmapBakeType.Realtime;
                light.color = Color.white; light.useColorTemperature = true; light.colorTemperature = 6500;
                light.intensity = 3; light.range = 18; light.spotAngle = 70; light.innerSpotAngle = 50;
                light.shadows = LightShadows.Hard; light.shadowNearPlane = 0.05f; light.shadowBias = 0.1f; light.shadowNormalBias = 0.2f;
                light.cullingMask = -1; light.renderingLayerMask = -1;
                go.AddComponent<UniversalAdditionalLightData>().usePipelineSettings = true;
                Undo.RegisterCreatedObjectUndo(go, "Create shopfront spot");
            }
            Undo.CollapseUndoOperations(group); EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
            Debug.Log("Created two optional 6500K realtime shadowed spotlights outside the opening: intensity 3 each, 70/50 degree cone, 18m range. This is a leak-test starting rig, not a finished lighting setup. The visible exterior/window luminance is a separate material/exposure decision.");
        }
    }
}
