#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(BookSpawner))]
public class BookSpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Kitaplar atanmis uc koridor alaninda olusur. Yigin/kule ayarlari BookSpawner uzerindedir.", MessageType.Info);
    }
}
#endif
