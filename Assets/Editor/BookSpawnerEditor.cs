#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BookSpawner))]
public class BookSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Kitap Spawn Circles", EditorStyles.boldLabel);

        BookSpawner spawner = (BookSpawner)target;

        if (spawner.spawnCircles == null || spawner.spawnCircles.Length == 0)
        {
            EditorGUILayout.HelpBox(
                "Su anda dairesel spawn alani yok. Bu nedenle kitaplar eski spawn sistemini kullanip her yerde cikabilir.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                $"Aktif dairesel spawn alani: {spawner.spawnCircles.Length}. Kitaplar bu alanlarda spawn olacak.",
                MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Create Spawn Circle", GUILayout.Height(30)))
        {
            CreateCircles(spawner, 1);
        }

        if (GUILayout.Button("Create 5 Spawn Circles", GUILayout.Height(30)))
        {
            CreateCircles(spawner, 5);
        }

        EditorGUILayout.EndHorizontal();

        if (spawner.spawnCircles != null && spawner.spawnCircles.Length > 0)
        {
            if (GUILayout.Button("Select All Spawn Circles"))
            {
                Selection.objects = GetCircleObjects(spawner.spawnCircles);
            }
        }

        if (GUI.changed)
            EditorUtility.SetDirty(spawner);
    }

    private static void CreateCircles(BookSpawner spawner, int count)
    {
        Undo.RecordObject(spawner, "Create book spawn circles");

        var circles = new List<BookSpawnCircle>();
        if (spawner.spawnCircles != null)
        {
            foreach (BookSpawnCircle existing in spawner.spawnCircles)
            {
                if (existing != null && !circles.Contains(existing))
                    circles.Add(existing);
            }
        }

        Vector3[] offsets = count == 1
            ? new[] { Vector3.zero }
            : new[]
            {
                new Vector3(-4f, 0f, -3f),
                new Vector3( 4f, 0f, -3f),
                new Vector3(-4f, 0f,  3f),
                new Vector3( 4f, 0f,  3f),
                new Vector3( 0f, 0f,  0f)
            };

        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject(
                count == 1 ? "SpawnCircle" : $"SpawnCircle_{circles.Count + 1:00}");

            Undo.RegisterCreatedObjectUndo(go, "Create book spawn circle");

            go.transform.SetParent(spawner.transform, false);
            go.transform.localPosition = offsets[i];
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            BookSpawnCircle circle = go.AddComponent<BookSpawnCircle>();
            circle.radius = 2f;
            circle.centerBias = 3f;

            circles.Add(circle);
        }

        spawner.spawnCircles = circles.ToArray();
        EditorUtility.SetDirty(spawner);
        EditorSceneManagerMarkDirty(spawner);

        Selection.activeGameObject = circles[circles.Count - count].gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    private static GameObject[] GetCircleObjects(BookSpawnCircle[] circles)
    {
        var result = new List<GameObject>();
        foreach (BookSpawnCircle circle in circles)
            if (circle != null)
                result.Add(circle.gameObject);
        return result.ToArray();
    }

    private static void EditorSceneManagerMarkDirty(BookSpawner spawner)
    {
        if (spawner.gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
    }
}
#endif
