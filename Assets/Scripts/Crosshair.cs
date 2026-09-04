using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(2f)] public float size = 14f;
    [Min(1f)] public float thickness = 2f;
    public Color color = Color.white;

    void OnGUI()
    {
        float half = size * 0.5f;
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        Color oldColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(centerX - half, centerY - thickness * 0.5f, size, thickness),
            Texture2D.whiteTexture,
            ScaleMode.StretchToFill,
            true);

        GUI.DrawTexture(
            new Rect(centerX - thickness * 0.5f, centerY - half, thickness, size),
            Texture2D.whiteTexture,
            ScaleMode.StretchToFill,
            true);

        GUI.color = oldColor;
    }
}
