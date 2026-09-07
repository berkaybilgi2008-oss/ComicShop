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
                "Kalici slot uretmek icin Play Mode'dan cik.",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Auto Fit aciksa kaynak slotun yeri ve izgaranin rafa dogru yonu otomatik bulunur.",
                MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying || duplicator.templateSlot == null))
        {
            if (GUILayout.Button("Olcumu Raporla", GUILayout.Height(24f)))
                duplicator.ReportPlan();

            if (GUILayout.Button("Temizle ve Yeniden Olustur", GUILayout.Height(32f)))
                duplicator.GenerateGrid();

            if (GUILayout.Button("Olusturulanlari Sil"))
                duplicator.ClearGenerated();
        }
    }
}
