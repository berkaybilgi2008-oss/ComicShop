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

        if (Selection.activeGameObject == null)
        {
            EditorGUILayout.HelpBox(
                "Ölçmek istediğin kitabı Hierarchy'den seç.",
                MessageType.Info
            );
            return;
        }

        GameObject selected = Selection.activeGameObject;

        Renderer[] renderers = selected.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Seçilen objede Renderer bulunamadı.",
                MessageType.Warning
            );
            return;
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 size = bounds.size;

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Kitap Boyutu (Unity)",
            EditorStyles.boldLabel
        );

        EditorGUILayout.LabelField(
            "X / Genişlik",
            $"{size.x:F4} m  ({size.x * 100:F2} cm)"
        );

        EditorGUILayout.LabelField(
            "Y / Yükseklik",
            $"{size.y:F4} m  ({size.y * 100:F2} cm)"
        );

        EditorGUILayout.LabelField(
            "Z / Derinlik",
            $"{size.z:F4} m  ({size.z * 100:F2} cm)"
        );
    }
}
