using UnityEditor;
using UnityEngine;

public class BookMeasurementTool : EditorWindow
{
    [MenuItem("Tools/Comic Shop/Book Measurement")]
    public static void ShowWindow()
    {
        GetWindow<BookMeasurementTool>("Book Measurement");
    }

    private void OnGUI()
    {
        GUILayout.Label("Kitap Ölçüm Aracı", EditorStyles.boldLabel);

        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorGUILayout.HelpBox("Ölçmek istediğin kitabı Hierarchy'den seç.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Seçilen Obje", selected.name);
        EditorGUILayout.Space();

        // ÖNEMLİ: Bu araç artık Transform/Renderer.bounds/World AABB ölçmüyor.
        // Doğrudan MeshFilter.sharedMesh.bounds değerini, mesh'in kendi lokal
        // koordinat sisteminde gösteriyor. Böylece Rotation hiçbir şekilde
        // ölçüyü değiştiremez.
        MeshFilter[] filters = selected.GetComponentsInChildren<MeshFilter>(true);
        MeshFilter firstValid = null;

        foreach (MeshFilter filter in filters)
        {
            if (filter.sharedMesh != null)
            {
                firstValid = filter;
                break;
            }
        }

        if (firstValid == null)
        {
            EditorGUILayout.HelpBox("MeshFilter/sharedMesh bulunamadı.", MessageType.Warning);
            return;
        }

        Mesh mesh = firstValid.sharedMesh;
        Vector3 rawMeshSize = mesh.bounds.size;

        // Mesh'in kendi lokal ölçüsünü göster.
        EditorGUILayout.LabelField("FBX Mesh Bounds", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("X / Genişlik", FormatMeters(rawMeshSize.x));
        EditorGUILayout.LabelField("Y / Yükseklik", FormatMeters(rawMeshSize.y));
        EditorGUILayout.LabelField("Z / Derinlik", FormatMeters(rawMeshSize.z));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mesh", mesh.name);
        EditorGUILayout.LabelField("MeshFilter", firstValid.name);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Transform Scale", EditorStyles.boldLabel);
        Vector3 scale = firstValid.transform.localScale;
        EditorGUILayout.LabelField("X", scale.x.ToString("F4"));
        EditorGUILayout.LabelField("Y", scale.y.ToString("F4"));
        EditorGUILayout.LabelField("Z", scale.z.ToString("F4"));

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Bu değerler mesh'in kendi lokal bounds verisidir. " +
            "Rotation'dan etkilenmez. Renderer.bounds kullanılmaz.",
            MessageType.Info
        );
    }

    private static string FormatMeters(float value)
    {
        return $"{value:F4} m  ({value * 100f:F2} cm)";
    }
}
