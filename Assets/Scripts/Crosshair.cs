using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(2f)] public float size = 10f;
    [Min(1f)] public float thickness = 2f;
    public Color color = Color.white;

    private Texture2D texture;

    void Awake()
    {
        texture = CreateRingTexture(32, 32, 10f, 2f);
    }

    void OnGUI()
    {
        if (texture == null)
            return;

        float x = (Screen.width - size) * 0.5f;
        float y = (Screen.height - size) * 0.5f;

        Color oldColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, size, size), texture);
        GUI.color = oldColor;
    }

    Texture2D CreateRingTexture(int width, int height, float radius, float ringThickness)
    {
        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.filterMode = FilterMode.Point;
        result.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float outer = radius;
        float inner = Mathf.Max(0f, radius - ringThickness);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                bool insideRing = distance <= outer && distance >= inner;
                result.SetPixel(x, y, insideRing ? Color.white : Color.clear);
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
