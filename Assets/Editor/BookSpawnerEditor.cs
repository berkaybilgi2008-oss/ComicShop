using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(BookSpawner))]
public sealed class BookSpawnerEditor : Editor
{
    private bool showLegacy;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        var spawner = (BookSpawner)target;
        EditorGUILayout.HelpBox("Kitaplari BookSpawner uretir. V16 Spawn Area sadece eski konum/olcek referansidir. Spawn Areas doluysa V16 ve eski Area Size kullanilmaz. Bos liste varsayilan olarak hata verir; eski genis alana gecmez.", MessageType.Info);
        var all = Object.FindObjectsByType<BookSpawner>(FindObjectsSortMode.None);
        int active = 0;
        foreach (var entry in all) if (entry.isActiveAndEnabled) active++;
        if (active > 1)
        {
            EditorGUILayout.HelpBox(active + " aktif BookSpawner var. Iki kez kitap uretmemek icin kullanmadigin BookSpawner bilesenini kapat.", MessageType.Warning);
            foreach (var entry in all)
                if (entry.isActiveAndEnabled && GUILayout.Button("Spawner sec: " + entry.name))
                    Selection.activeGameObject = entry.gameObject;
        }
        DrawPropertiesExcluding(serializedObject, "m_Script", "v16SpawnArea", "areaSize", "spawnHeight");
        bool manual = spawner.spawnAreas != null && spawner.spawnAreas.Length > 0;
        EditorGUILayout.LabelField("Kullanilan alan", manual ? "Spawn Areas (elle cizilen)" : spawner.allowLegacyArea ? "Eski alan (V16)" : "ALAN YOK - spawn engelli");
        showLegacy = EditorGUILayout.Foldout(showLegacy, "Eski V16 / alan ayarlari");
        if (showLegacy)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("v16SpawnArea"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("areaSize"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnHeight"));
        }
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || EditorUtility.IsPersistent(spawner)))
        {
            if (GUILayout.Button("Tek kutu alanla basla (eski alan listesini degistir)"))
                CreateArea(spawner, true);
            if (GUILayout.Button("Ek kutu alan ekle"))
                CreateArea(spawner, false);
        }
        if (spawner.spawnAreas != null)
            foreach (var region in spawner.spawnAreas)
                if (region != null && GUILayout.Button("Alani sec / duzenle: " + region.name))
                {
                    Selection.activeGameObject = region.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
    }

    private static void CreateArea(BookSpawner spawner, bool replace)
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Elle spawn alani olustur");
        var list = new List<BookSpawnArea>();
        if (spawner.spawnAreas != null)
            foreach (var previous in spawner.spawnAreas)
            {
                if (previous == null) continue;
                if (!replace) { list.Add(previous); continue; }
                // Preserve old tuning and objects for Undo; hide their inactive gizmos.
                Undo.RecordObject(previous, "Onceki alani kapat");
                previous.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(previous);
            }
        Transform source = spawner.v16SpawnArea != null ? spawner.v16SpawnArea : spawner.transform;
        var go = new GameObject("BookSpawnArea_Manual_" + (list.Count + 1));
        Undo.RegisterCreatedObjectUndo(go, "Spawn kutusu");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, spawner.gameObject.scene);
        go.transform.position = source.TransformPoint(new Vector3(0f, spawner.spawnHeight, 0f));
        go.transform.rotation = Quaternion.Euler(0f, source.eulerAngles.y, 0f);
        var area = Undo.AddComponent<BookSpawnArea>(go);
        area.width = 2f;
        area.depth = 2f;
        area.height = 0f;
        list.Add(area);
        Undo.RecordObject(spawner, "Spawn listesi");
        spawner.spawnAreas = list.ToArray();
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        Undo.CollapseUndoOperations(group);
        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();
    }
}
