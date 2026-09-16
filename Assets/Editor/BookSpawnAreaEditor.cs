using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BookSpawnArea))]
public sealed class BookSpawnAreaEditor : Editor
{
    private readonly BoxBoundsHandle bounds = new BoxBoundsHandle();
    private bool editArea = true;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Bu alan kitaplarin dogacagi yuzeydir. Edit Area acikken Scene'deki kenar tutamaclarini collider gibi surukle. W ile tasi, E ile Y ekseninde dondur. Fizik collider'i eklemek gerekmez.", MessageType.Info);
        DrawDefaultInspector();
        var area = (BookSpawnArea)target;
        Vector3 scale = area.transform.lossyScale;
        EditorGUILayout.LabelField("Gercek alan (Unity birimi)",
            $"{Mathf.Abs(area.width * scale.x):0.##} x {Mathf.Abs(area.depth * scale.z):0.##}");
        if (Mathf.Abs(Mathf.Abs(scale.x) - 1f) > 0.001f || Mathf.Abs(Mathf.Abs(scale.z) - 1f) > 0.001f)
            EditorGUILayout.HelpBox("Transform veya ust nesne Scale degeri Width/Depth ile carpiliyor. Yukaridaki gercek boyutu kontrol et.", MessageType.Warning);
        editArea = GUILayout.Toggle(editArea, "Edit Area - kenarlardan boyutlandir", "Button");
        if (GUILayout.Button("Scene'de alana odaklan"))
            SceneView.lastActiveSceneView?.FrameSelected();
    }
    private void OnSceneGUI()
    {
        if (!editArea || EditorApplication.isPlaying) return;
        var area = (BookSpawnArea)target;
        using (new Handles.DrawingScope(area.transform.localToWorldMatrix))
        {
            bounds.center = new Vector3(0f, area.height, 0f);
            bounds.size = new Vector3(area.width, 0.02f, area.depth);
            bounds.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z;
            EditorGUI.BeginChangeCheck();
            bounds.DrawHandle();
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObjects(new Object[] { area, area.transform }, "Spawn alanini boyutlandir");
            area.transform.position += area.transform.TransformVector(new Vector3(bounds.center.x, 0f, bounds.center.z));
            area.width = Mathf.Max(0.05f, bounds.size.x);
            area.depth = Mathf.Max(0.05f, bounds.size.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);
            PrefabUtility.RecordPrefabInstancePropertyModifications(area.transform);
            EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
        }
    }

    [MenuItem("Tools/ComicShop/Spawn/Mevcut Alandan Guvenli Bolgeler Olustur")]
    private static void Build()
    {
        if (EditorApplication.isPlaying) return;
        var spawner = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<BookSpawner>() : null;
        if (spawner == null)
        {
            Debug.LogWarning("Hierarchy'de BookSpawner bileseni olan nesneyi secin.");
            return;
        }
        if (spawner.spawnAreas != null && spawner.spawnAreas.Length > 0)
        {
            Debug.LogWarning("Mevcut alanlar korunuyor. Yeniden uretmek icin once Spawn Areas listesini bosaltin.");
            return;
        }
        Physics.SyncTransforms();
        Transform source = spawner.v16SpawnArea != null ? spawner.v16SpawnArea : spawner.transform;
        Vector3 scale = source.lossyScale;
        float width = Mathf.Abs(spawner.areaSize.x * scale.x);
        float depth = Mathf.Abs(spawner.areaSize.y * scale.z);
        if (width < 0.1f || depth < 0.1f) return;
        // World-upright grid: each accepted cell has floor and empty clearance.
        int nx = Mathf.Clamp(Mathf.CeilToInt(width / 1.5f), 1, 64);
        int nz = Mathf.Clamp(Mathf.CeilToInt(depth / 1.5f), 1, 64);
        float dx = width / nx, dz = depth / nz;
        Quaternion rotation = Quaternion.Euler(0f, source.eulerAngles.y, 0f);
        var positions = new List<Vector3>();
        for (int x = 0; x < nx; x++)
        for (int z = 0; z < nz; z++)
        {
            Vector3 center = source.position + rotation * new Vector3(
                -width * 0.5f + (x + 0.5f) * dx, 0f, -depth * 0.5f + (z + 0.5f) * dz);
            Vector3 start = center + Vector3.up * Mathf.Max(0.5f, spawner.spawnHeight * Mathf.Abs(scale.y));
            if (!Physics.Raycast(start, Vector3.down, out var floor, 20f, Physics.AllLayers,
                QueryTriggerInteraction.Ignore) || floor.normal.y < 0.95f ||
                floor.collider.attachedRigidbody != null || floor.collider.GetComponentInParent<ShelfSlot>() != null)
                continue;
            center.y = floor.point.y;
            bool fullFloor = true;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 probe = center + rotation * new Vector3(
                    (corner % 2 == 0 ? -1f : 1f) * dx * 0.5f, 0.2f,
                    (corner < 2 ? -1f : 1f) * dz * 0.5f);
                if (!Physics.Raycast(probe, Vector3.down, out var edge, 0.3f, Physics.AllLayers,
                    QueryTriggerInteraction.Ignore) || edge.normal.y < 0.95f ||
                    Mathf.Abs(edge.point.y - center.y) > 0.03f) fullFloor = false;
            }
            if (!fullFloor) continue;
            // Reject shelves, walls and objects through the full falling volume.
            if (Physics.CheckBox(center + Vector3.up * 0.55f,
                new Vector3(dx * 0.5f + 0.2f, 0.5f, dz * 0.5f + 0.2f),
                rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore)) continue;
            positions.Add(center);
        }
        if (positions.Count == 0)
        {
            Debug.LogWarning("Bos zemin bulunamadi. Eski alanin konumunu/boyutunu ve zemin collider'ini kontrol edin.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Spawn bolgeleri olustur");
        var root = new GameObject("BookSpawnAreas");
        Undo.RegisterCreatedObjectUndo(root, "Spawn bolgeleri");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, spawner.gameObject.scene);
        var regions = new List<BookSpawnArea>();
        foreach (Vector3 position in positions)
        {
            var go = new GameObject("SpawnArea_" + (regions.Count + 1).ToString("00"));
            Undo.RegisterCreatedObjectUndo(go, "Spawn bolgesi");
            go.transform.SetParent(root.transform);
            go.transform.SetPositionAndRotation(position, rotation);
            var region = Undo.AddComponent<BookSpawnArea>(go);
            region.width = dx;
            region.depth = dz;
            regions.Add(region);
        }
        Undo.RecordObject(spawner, "Spawn listesi");
        spawner.spawnAreas = regions.ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = root;
        Debug.Log(regions.Count + " spawn bolgesi olusturuldu. Scene'de kontrol edin ve sahneyi kaydedin.");
    }
}
