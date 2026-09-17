using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ComicShop.Rendering.Editor
{
    public static class ToonOutlineSetup
    {
        [MenuItem("Tools/ComicShop/Step 2/Install Screen Outlines on Project URP Renderers")]
        public static void Install()
        {
            Shader shader = Shader.Find("Hidden/ComicShop/ScreenSpaceOutline");
            if (shader == null) { Debug.LogError("Import ScreenSpaceOutline.shader first."); return; }
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                ScreenSpaceOutline feature = null;
                foreach (var candidate in data.rendererFeatures)
                    if (candidate is ScreenSpaceOutline outline) { feature = outline; break; }
                Undo.RecordObject(data, "Install screen outlines");
                if (feature == null)
                {
                    feature = ScriptableObject.CreateInstance<ScreenSpaceOutline>();
                    feature.name = "ScreenSpaceOutline";
                    AssetDatabase.AddObjectToAsset(feature, data);
                    Undo.RegisterCreatedObjectUndo(feature, "Create outline feature");
                    data.rendererFeatures.Add(feature);
                }
                Undo.RecordObject(feature, "Configure outline feature");
                feature.settings.outlineShader = shader;
                feature.SetActive(true); feature.Create();
                EditorUtility.SetDirty(feature); data.SetDirty(); EditorUtility.SetDirty(data); count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Installed/refreshed screen outlines on {count} URP renderer assets. Color, width and thresholds are in ToonStyleAsset; layer mask is in the feature. Render Graph must be enabled. Existing MSAA is not changed.");
        }
    }
}
