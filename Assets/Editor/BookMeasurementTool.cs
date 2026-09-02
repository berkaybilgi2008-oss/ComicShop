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
        Bounds bounds;

        if (!TryCalculateLocalBounds(selected.transform, out bounds))
        {
            EditorGUILayout.HelpBox(
                "Seçilen objede ölçülebilecek Mesh Renderer bulunamadı.",
                MessageType.Warning
            );
            return;
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

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Ölçüm seçili objenin kendi lokal eksenlerinde yapılır. " +
            "Bu nedenle objenin Rotation değeri değişse bile gerçek X/Y/Z model boyutu değişmez.",
            MessageType.Info
        );
    }

    private static bool TryCalculateLocalBounds(Transform root, out Bounds bounds)
    {
        bool initialized = false;
        bounds = new Bounds();

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
                continue;

            Bounds meshBounds = mesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;

            Vector3[] corners =
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x,  extents.y,  extents.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldPoint = meshFilter.transform.TransformPoint(corner);
                Vector3 localPoint = root.InverseTransformPoint(worldPoint);
                Encapsulate(ref bounds, ref initialized, localPoint);
            }
        }

        // Skinned meshler için mesh bounds fallback'i.
        SkinnedMeshRenderer[] skinnedRenderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
                continue;

            Bounds meshBounds = mesh.bounds;
            Vector3 center = meshBounds.center;
            Vector3 extents = meshBounds.extents;

            Vector3[] corners =
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x,  extents.y,  extents.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldPoint = renderer.transform.TransformPoint(corner);
                Vector3 localPoint = root.InverseTransformPoint(worldPoint);
                Encapsulate(ref bounds, ref initialized, localPoint);
            }
        }

        return initialized;
    }

    private static void Encapsulate(ref Bounds bounds, ref bool initialized, Vector3 point)
    {
        if (!initialized)
        {
            bounds = new Bounds(point, Vector3.zero);
            initialized = true;
        }
        else
        {
            bounds.Encapsulate(point);
        }
    }
}
