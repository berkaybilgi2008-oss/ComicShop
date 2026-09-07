using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShelfSlotDuplicator))]
public class ShelfSlotDuplicatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        ShelfSlotDuplicator duplicator = (ShelfSlotDuplicator)target;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("Raf Slot Araci", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Slotlari kalici olusturmak icin once Play Mode'dan cik.",
                MessageType.Warning);
        }
        else if (duplicator.TryGetValidationError(out string error))
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Template + iki komsu araligiyla eksik gozler olusturulur. Yon Duzeltme aciksa " +
                "izgaranin raf bosluklarina dogru buyuyen yonu otomatik secilir.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying || duplicator.templateSlot == null))
        {
            if (GUILayout.Button("Eksik Gozleri Olustur", GUILayout.Height(30f)))
                duplicator.GenerateGrid();

            if (GUILayout.Button("Temizle ve Yeniden Olustur", GUILayout.Height(30f)))
                duplicator.RegenerateGrid();

            if (GUILayout.Button("Olusturulanlari Sil"))
                duplicator.ClearGenerated();
        }
    }
}
