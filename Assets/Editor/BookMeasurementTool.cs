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
            EditorGUILayout.HelpBox("Ölçmek istediğin kitabı Hierarchy'den seç.", MessageType.Info);
            return;
        }

        GameObject selected = Selection.activeGameObject;
        Vector3 size;

        if (!TryMeasureWithoutRootRotation(selected.transform, out size))
        {
            EditorGUILayout.HelpBox("Seçilen objede ölçülebilecek Renderer bulunamadı.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Kitap Boyutu (gerçek model sınırı)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("X / Genişlik", $"{size.x:F4} m  ({size.x * 100:F2} cm)");
        EditorGUILayout.LabelField("Y / Yükseklik", $"{size.y:F4} m  ({size.y * 100:F2} cm)");
        EditorGUILayout.LabelField("Z / Derinlik", $"{size.z:F4} m  ({size.z * 100:F2} cm)");

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Bu araç kitabın root Rotation değerini geçici olarak sıfırlayıp Renderer sınırını ölçer. " +
            "Sonra Rotation aynen geri yüklenir. Böylece döndürme yüzünden X/Y/Z ölçüleri değişmez.",
            MessageType.Info
        );
    }

    private static bool TryMeasureWithoutRootRotation(Transform root, out Vector3 size)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            size = Vector3.zero;
            return false;
        }

        Quaternion originalRotation = root.localRotation;
        Vector3 originalPosition = root.localPosition;

        // Sadece seçilen objenin root dönüşünü nötrleştiriyoruz.
        // FBX içindeki child transformları ve modelin gerçek geometrisi değişmiyor.
        root.localRotation = Quaternion.identity;

        Physics.SyncTransforms();

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        size = bounds.size;

        root.localRotation = originalRotation;
        root.localPosition = originalPosition;
        Physics.SyncTransforms();

        return true;
    }
}
