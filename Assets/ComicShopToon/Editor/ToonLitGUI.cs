using UnityEditor;
using UnityEngine;

namespace ComicShop.Rendering.Editor
{
    public sealed class ToonLitGUI : ShaderGUI
    {
        public static readonly string[] Parameters = {
            "ShadowSteps",
            "RampSmoothness",
            "ShadowTint",
            "BakedSteps",
            "BakedInfluence",
            "LightFalloffScale",
            "SpecEnabled",
            "SpecThreshold",
            "SpecColor",
            "SpecStrength",
            "RimEnabled",
            "RimThreshold",
            "RimLitOnly",
            "RimColor",
            "RimStrength",
            "HalftoneEnabled",
            "HalftoneScale",
            "HalftoneStrength",
            "HalftoneAngle",
            "HalftoneRadius",
            "HalftoneColor"
        };
        bool expanded;
        public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox("Görünüm tek ToonStyleAsset üzerinden yönetilir. Materyalde yalnız albedo, normal ve iki yüzey anahtarı bulunur.", MessageType.Info);
            foreach (string name in new[] { "_BaseMap", "_BaseColor", "_BumpMap", "_HalftoneEnabled", "_OutlineEnabled" })
                editor.ShaderProperty(FindProperty(name, properties), FindProperty(name, properties).displayName);
            expanded = EditorGUILayout.Foldout(expanded, "Advanced — optional local overrides", true);
            if (expanded)
            {
                EditorGUILayout.HelpBox("Normal kullanım: tüm ayarlar Follow Global. Her override görünüm tutarlılığını bilinçli olarak bozar.", MessageType.Warning);
                foreach (string name in Parameters)
                {
                    var toggle = FindProperty("_Override" + name, properties);
                    EditorGUI.showMixedValue = toggle.hasMixedValue;
                    EditorGUI.BeginChangeCheck();
                    bool enabled = EditorGUILayout.ToggleLeft("Override " + name, toggle.floatValue > .5f);
                    if (EditorGUI.EndChangeCheck())
                    {
                        editor.RegisterPropertyChangeUndo("Toggle Toon Override");
                        toggle.floatValue = enabled ? 1 : 0;
                    }
                    EditorGUI.showMixedValue = false;
                    if (enabled) editor.ShaderProperty(FindProperty("_Local" + name, properties), name);
                }
            }
            foreach (Object target in editor.targets) SyncKeywords((Material)target);
            editor.EnableInstancingField();
        }
        public override void ValidateMaterial(Material material) => SyncKeywords(material);
        public static void SyncKeywords(Material m)
        {
            bool any = false;
            foreach (string name in Parameters) any |= m.GetFloat("_Override" + name) > .5f;
            m.SetFloat("_UseLocalStyle", any ? 1 : 0);
            SetKeyword(m, "_TOON_LOCAL_STYLE", any);
            SetKeyword(m, "_TOON_HALFTONE", m.GetFloat("_HalftoneEnabled") > .5f);
            SetKeyword(m, "_NORMALMAP", m.GetTexture("_BumpMap") != null);
        }
        static void SetKeyword(Material m, string keyword, bool enabled)
        {
            if (enabled) m.EnableKeyword(keyword); else m.DisableKeyword(keyword);
        }
    }
}
