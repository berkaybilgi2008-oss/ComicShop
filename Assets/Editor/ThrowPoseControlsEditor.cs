using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ThrowPoseControls))]
public sealed class ThrowPoseControlsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var controls = (ThrowPoseControls)target;
        EditorGUILayout.HelpBox("Play'de kitap al ve Preview Pose'u ac. Scene'de ElHedefi'ni tasi/dondur; DirsekHedefi ile bukulme yonunu, KitapHedefi ile kitabin ele gore acisini ayarla. Omuz sabit kalir, kol boyu uzamaz. Kaydet dugmesi Play'den cikinca da ayarlari korur.", MessageType.Info);
        if (!Application.isPlaying) return;
        if (GUILayout.Button("El hedefini sec")) Selection.activeGameObject = controls.handTarget.gameObject;
        if (GUILayout.Button("Dirsek hedefini sec")) Selection.activeGameObject = controls.elbowTarget.gameObject;
        if (GUILayout.Button("Kitap hedefini sec")) Selection.activeGameObject = controls.bookTarget.gameObject;
        if (GUILayout.Button("Ayarlari kalici kaydet")) Save(controls);
        if (GUILayout.Button("Kayitli ayarlara don")) controls.ApplySaved();
    }

    [MenuItem("ComicShop/Q Tutus/Ayar Panelini Ac (Play)")]
    public static void Open()
    {
        var controls = LocalControls();
        if (controls) Selection.activeGameObject = controls.gameObject;
    }

    [MenuItem("ComicShop/Q Tutus/Ayarlari Kalici Kaydet (Play)")]
    public static void SaveLocal()
    {
        var controls = LocalControls();
        if (controls) Save(controls);
    }

    private static ThrowPoseControls LocalControls()
    {
        if (Application.isPlaying)
            foreach (var controls in Object.FindObjectsByType<ThrowPoseControls>(FindObjectsSortMode.None))
                if (controls.IsLocal) return controls;
        Debug.LogWarning("Once Play'i ac ve oyuncunun olusmasini bekle.");
        return null;
    }

    private static void Save(ThrowPoseControls controls)
    {
        if (!controls.settings || !AssetDatabase.Contains(controls.settings))
        {
            Debug.LogError("Kalici ThrowPoseSettings asseti bulunamadi.", controls);
            return;
        }
        Undo.RecordObject(controls.settings, "Q tutus ayarlari");
        controls.settings.handPosition = controls.handTarget.localPosition;
        controls.settings.handEuler = controls.handTarget.localEulerAngles;
        controls.settings.elbowPosition = controls.elbowTarget.localPosition;
        controls.settings.bookPosition = controls.bookTarget.localPosition;
        controls.settings.bookEuler = controls.bookTarget.localEulerAngles;
        EditorUtility.SetDirty(controls.settings);
        AssetDatabase.SaveAssets();
        Debug.Log("Q tutus ayarlari kaydedildi. Play'den cikinca ve sonraki build'de korunur.", controls.settings);
    }
}
