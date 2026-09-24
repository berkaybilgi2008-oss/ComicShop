using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[CustomEditor(typeof(ShelfLogoBinding))]
public sealed class ShelfLogoBindingEditor : Editor
{
    const string Folder = "Assets/ShelfLogoGenerated";

    [InitializeOnLoadMethod]
    static void WatchLogoEdits()
    {
        EditorApplication.update -= Refresh;
        EditorApplication.update += Refresh;
    }

    static double nextRefresh;
    static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + .25;
        ShelfLogoBinding.RefreshAll();
    }

    public override void OnInspectorGUI()
    {
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            DrawDefaultInspector();
            var binding = (ShelfLogoBinding)target;
            var oldLogo = binding.ReadLogo();
            var logo = (Texture2D)EditorGUILayout.ObjectField("Logo", oldLogo, typeof(Texture2D), false);
            if (logo != oldLogo) SetLogo(binding, logo);
            if (GUILayout.Button("Raf gozlerini bu logoya bagla")) BindSlots(binding);
            EditorGUILayout.HelpBox(binding.Status, binding.PublisherID >= 0 ? MessageType.Info : MessageType.Warning);
        }
        EditorGUILayout.HelpBox("Logolari Play kapaliyken degistir. BrandCatalog icindeki Logo Texture alanlarini bir kez doldur.", MessageType.Info);
    }

    static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "ShelfLogoGenerated");
    }

    static void SetLogo(ShelfLogoBinding binding, Texture2D logo)
    {
        if (binding.logoRenderer == null) { Debug.LogWarning("Once logo Renderer alanini ata.", binding); return; }
        if (binding.logoRenderer is SpriteRenderer)
        {
            Debug.LogWarning("SpriteRenderer icin Sprite alanini degistir; katalogda sprite'in kaynak dokusunu kullan.", binding);
            return;
        }
        var materials = binding.logoRenderer.sharedMaterials;
        if (binding.materialIndex < 0 || binding.materialIndex >= materials.Length) return;
        var source = materials[binding.materialIndex];
        if (source == null || !source.HasProperty(binding.textureProperty)) return;
        // Copy on change: another bookcase may share the original sign material.
        EnsureFolder();
        var material = new Material(source) { name = "ShelfLogo_" + (logo != null ? logo.name : "Empty") };
        material.SetTexture(binding.textureProperty, logo);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/ShelfLogo.mat"));
        Undo.RecordObject(binding.logoRenderer, "Raf logosunu degistir");
        materials[binding.materialIndex] = material;
        binding.logoRenderer.sharedMaterials = materials;
        PrefabUtility.RecordPrefabInstancePropertyModifications(binding.logoRenderer);
        EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
        ShelfLogoBinding.RefreshAll();
    }

    [MenuItem("ComicShop/Shelf Logos/Connect All Bookcases")]
    static void ConnectAll()
    {
        if (Application.isPlaying) return;
        // Include the seven inactive sets of slots from the earlier numbered assignment.
        var slots = UnityEngine.Object.FindObjectsByType<ShelfSlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var roots = new HashSet<Transform>(slots.Where(x => x.gameObject.scene.IsValid())
            .Select(x => x.transform.parent).Where(x => x != null));
        var catalog = Resources.Load<BrandCatalog>("BrandCatalog");
        if (catalog == null) throw new InvalidOperationException("Resources/BrandCatalog bulunamadi.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Raflari logolarina bagla");
        foreach (var root in roots)
        {
            var binding = root.GetComponent<ShelfLogoBinding>();
            if (binding == null) binding = Undo.AddComponent<ShelfLogoBinding>(root.gameObject);
            Undo.RecordObject(binding, "Logo kaynagini bagla");
            binding.catalog = catalog;
            if (binding.logoRenderer == null)
            {
                var anchors = root.GetComponentsInChildren<Transform>(true)
                    .Where(x => x.name.StartsWith("LOGO_BOS_KARE_", StringComparison.Ordinal)).ToArray();
                if (anchors.Length == 1)
                {
                    var renderers = anchors[0].GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 1) binding.logoRenderer = renderers[0];
                    else if (renderers.Length == 0) binding.logoRenderer = CreatePanel(anchors[0]);
                }
                if (binding.logoRenderer == null)
                    Debug.LogWarning(root.name + ": Logo alani tekil degil veya yok; ShelfLogoBinding icinde dogru Logo Renderer'i sec.", root);
            }
            BindSlots(binding);
            EditorUtility.SetDirty(binding);
            PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
        }
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        ShelfLogoBinding.RefreshAll();
        Debug.Log(roots.Count + " kitaplik logoya baglandi. BrandCatalog logo eslesmelerini doldur ve her kitaplikta Logo sec; sahneyi kaydet.");
    }

    public static void BindSlots(ShelfLogoBinding binding)
    {
        if (Application.isPlaying) return;
        foreach (var slot in binding.GetComponentsInChildren<ShelfSlot>(true))
        {
            // Nested bookcases belong to their own binding.
            if (slot.GetComponentInParent<ShelfLogoBinding>(true) != binding) continue;
            Undo.RecordObject(slot, "Raf logo baglantisi");
            slot.publisherLogo = binding;
            PrefabUtility.RecordPrefabInstancePropertyModifications(slot);
            Undo.RecordObject(slot.gameObject, "Raf gozunu etkinlestir");
            slot.gameObject.SetActive(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(slot.gameObject);
            EditorUtility.SetDirty(slot);
        }
        EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
    }

    static Renderer CreatePanel(Transform anchor)
    {
        string sizeText = anchor.name.Substring("LOGO_BOS_KARE_".Length).TrimEnd('m');
        if (!float.TryParse(sizeText, NumberStyles.Float, CultureInfo.InvariantCulture, out float side) || side <= 0)
            throw new InvalidOperationException("Logo alaninin metre olcusu okunamadi: " + anchor.name);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) throw new InvalidOperationException("URP Unlit shader bulunamadi.");
        EnsureFolder();
        // Construct in world axes, then transform back: RAF has non-uniform ~200x scale.
        Vector3 right = anchor.right * side * .5f, up = anchor.up * side * .5f;
        Vector3 center = anchor.position + anchor.forward * .001f;
        var mesh = new Mesh { name = "ShelfLogoSquare" };
        mesh.vertices = new[] {
            anchor.InverseTransformPoint(center - right - up), anchor.InverseTransformPoint(center + right - up),
            anchor.InverseTransformPoint(center + right + up), anchor.InverseTransformPoint(center - right + up) };
        mesh.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Folder + "/LogoSquare.asset"));
        var material = new Material(shader) { name = "ShelfLogo_Empty" };
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(Folder + "/ShelfLogo.mat"));
        var panel = new GameObject("PublisherLogo");
        Undo.RegisterCreatedObjectUndo(panel, "Logo paneli olustur");
        panel.transform.SetParent(anchor, false);
        panel.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = panel.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }
}
