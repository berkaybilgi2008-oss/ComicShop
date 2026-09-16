using System;
using UnityEditor;
using UnityEngine;

namespace ComicShop.Rendering.Editor
{
    [InitializeOnLoad]
    public static class ToonStyleEditor
    {
        static double nextRefresh;
        static string lastState;
        static ToonStyleEditor()
        {
            EditorApplication.update += Refresh;
            Undo.undoRedoPerformed += ToonStyleController.PublishActive;
        }
        static void Refresh()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.1;
            string state = EditorJsonUtility.ToJson(ToonStyleController.ActiveStyle);
            if (state == lastState) return;
            lastState = state;
            ToonStyleController.PublishActive();
            // Asset edits affect every Scene/Game view without requiring a scene edit.
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }
        [MenuItem("Tools/ComicShop/Toon Style/Select Global Style")]
        static void SelectStyle() => Selection.activeObject = ToonStyleController.ActiveStyle;

        [MenuItem("Tools/ComicShop/Toon Style/Reset All ToonLit Materials to Follow Global")]
        public static void ResetAll()
        {
            int count = 0;
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is Material material) || material.shader == null || material.shader.name != "ComicShop/ToonLit") continue;
                    if (!AssetDatabase.IsOpenForEdit(material)) continue;
                    Undo.RecordObject(material, "Reset Toon Style Overrides");
                    foreach (string parameter in ToonLitGUI.Parameters) material.SetFloat("_Override" + parameter, 0);
                    ToonLitGUI.SyncKeywords(material);
                    EditorUtility.SetDirty(material);
                    count++;
                }
            }
            Undo.CollapseUndoOperations(group);
            AssetDatabase.SaveAssets();
            Debug.Log($"{count} ToonLit materials follow the global style. Albedo, normal and surface toggles preserved.");
        }
        [MenuItem("Tools/ComicShop/Toon Style/Validate Shader Import")]
        public static void ValidateShaderImport()
        {
            var shader = Shader.Find("ComicShop/ToonLit");
            if (shader == null) throw new InvalidOperationException("ToonLit shader not found.");
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(shader), ImportAssetOptions.ForceUpdate);
            bool error = false;
            foreach (var message in ShaderUtil.GetShaderMessages(shader))
            {
                Debug.Log($"{message.severity}: {message.message} ({message.file}:{message.line})");
                error |= message.severity.ToString() == "Error";
            }
            if (error) throw new InvalidOperationException("ToonLit import contains shader errors.");
            Debug.Log("ToonLit import has no reported errors for the active editor graphics API. Render/build validation is still required.");
        }
    }
}
