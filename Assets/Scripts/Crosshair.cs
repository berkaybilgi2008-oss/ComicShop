using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(2f)] public float size = 14f;
    [Min(1f)] public float thickness = 3.5f;
    public Color color = Color.white;

    private Texture2D texture;

    void Awake()
    {
        texture = CreateRingTexture(96, 96, 28f, thickness * 1.35f);
    }

    void OnGUI()
    {
        if (texture == null)
            return;

        float x = (Screen.width - size) * 0.5f;
        float y = (Screen.height - size) * 0.5f;

        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, size, size), texture, ScaleMode.StretchToFill, true);
        GUI.color = oldColor;
    }

    Texture2D CreateRingTexture(int width, int height, float radius, float ringThickness)
    {
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Bilinear;
        result.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float outer = radius;
        float inner = Mathf.Max(0f, radius - ringThickness);
        float edge = 1.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float outerAlpha = 1f - Mathf.SmoothStep(outer - edge, outer + edge, distance);
                float innerAlpha = Mathf.SmoothStep(inner - edge, inner + edge, distance);
                float alpha = Mathf.Clamp01(outerAlpha * innerAlpha);
                result.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        result.Apply();
        return result;
    }

    void OnDestroy()
    {
        if (texture != null)
            Destroy(texture);
    }
}
