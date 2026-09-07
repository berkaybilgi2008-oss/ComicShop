using System.IO;
using UnityEditor;
using UnityEngine;

// Explicit menu action. Does not alter scene objects, prefab components or import scale.
public static class ComicShopRafSetup
{
    static readonly string[] Names = { "CS_Ink", "CS_Walnut", "CS_Honey", "CS_Brass", "CS_Cream", "CS_Grain" };
    static readonly Color[] Colors = {
        new Color(.075f,.044f,.027f), new Color(.40f,.22f,.10f),
        new Color(.56f,.31f,.115f), new Color(.70f,.47f,.18f),
        new Color(.81f,.72f,.49f), new Color(.20f,.084f,.035f)
    };
    [MenuItem("Tools/Comic Shop/Secili RAF icin Cel Shaded malzemeleri kur")]
    public static void Setup()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null || Path.GetExtension(path).ToLowerInvariant() != ".fbx") {
            EditorUtility.DisplayDialog("Comic Shop", "Project panelinde paketteki RAF.fbx dosyasini sec.", "Tamam"); return;
        }
        bool isOurModel = false;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is Mesh mesh && mesh.name == "RAF_CelShaded") isOurModel = true;
        if (!isOurModel) {
            EditorUtility.DisplayDialog("Comic Shop", "Bu arac yalnizca paketteki RAF_CelShaded mesh icin calisir.", "Tamam"); return;
        }
        Shader shader = Shader.Find("ComicShop/Raf Cel");
        if (shader == null) { Debug.LogError("ComicShopCel.shader bulunamadi. Paketin tamamini Assets'e kopyala."); return; }
        string folder = Path.GetDirectoryName(path).Replace('\\','/') + "/RafMaterials";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'), "RafMaterials");
        for (int i=0;i<Names.Length;i++) {
            string materialPath = folder+"/"+Names[i]+".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null) { material=new Material(shader); AssetDatabase.CreateAsset(material,materialPath); }
            else { Undo.RecordObject(material,"Setup shelf cel materials"); material.shader=shader; }
            material.SetColor("_BaseColor",Colors[i]); material.SetFloat("_ShadowTone",.48f); material.SetFloat("_MidTone",.78f);
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),Names[i]), material);
        }
        AssetDatabase.SaveAssets(); importer.SaveAndReimport();
        Debug.Log("Raf: 6 cel materyal baglandi. Olcek, sahne, collider ve raf slotlari degistirilmedi.");
    }
}
