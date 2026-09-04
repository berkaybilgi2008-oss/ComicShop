using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(4f)] public float size = 14f;
    [Min(0.5f)] public float thickness = 2f;
    public Color color = Color.white;

    private Texture2D ringTexture;

    void Awake()
    {
        CreateRingTexture();
    }

    void OnDestroy()
    {
        if (ringTexture != null)
            Destroy(ringTexture);
    }

    void CreateRingTexture()
    {
        int textureSize = 64;
        ringTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        ringTexture.wrapMode = TextureWrapMode.Clamp;
        ringTexture.filterMode = FilterMode.Bilinear;

        float center = (textureSize - 1) * 0.5f;
        float outerRadius = center - 1f;
        float innerRadius = outerRadius - 3f;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float outerAlpha = Mathf.Clamp01(outerRadius - distance + 1f);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius + 1f);
                float alpha = outerAlpha * innerAlpha;

                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        ringTexture.Apply();
    }

    void OnGUI()
    {
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;

        Color oldColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(centerX - size * 0.5f, centerY - size * 0.5f, size, size),
            ringTexture,
            ScaleMode.StretchToFill,
            true);

        GUI.color = oldColor;
    }
}
