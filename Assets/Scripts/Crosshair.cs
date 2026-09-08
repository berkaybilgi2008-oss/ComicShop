using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(4f)] public float size = 14f;
    [Min(0.5f)] public float thickness = 2f;
    public Color color = Color.white;

    [Header("Sarj Bari")]
    [Tooltip("0 = gizli. PlayerInteraction her karede bu degeri yazar.")]
    [Range(0f, 1f)] public float chargeAmount = 0f;
    [Min(20f)] public float barWidth = 170f;
    [Min(3f)] public float barHeight = 9f;
    [Tooltip("Barin nisangahin ne kadar altinda duracagi.")]
    public float barOffsetY = 34f;
    public Color barBackColor = new Color(0f, 0f, 0f, 0.45f);
    public Color barLowColor = new Color(1f, 0.82f, 0.35f, 0.95f);
    public Color barFullColor = new Color(1f, 0.42f, 0.2f, 1f);

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

        DrawChargeBar(centerX, centerY);

        GUI.color = oldColor;
    }

    void DrawChargeBar(float centerX, float centerY)
    {
        if (chargeAmount <= 0.001f)
            return;

        float fill = Mathf.Clamp01(chargeAmount);
        float left = centerX - barWidth * 0.5f;
        float top = centerY + barOffsetY;

        // Zemin
        GUI.color = barBackColor;
        GUI.DrawTexture(new Rect(left, top, barWidth, barHeight), Texture2D.whiteTexture);

        // Dolan kisim -- doldukca sariden turuncuya doner
        GUI.color = Color.Lerp(barLowColor, barFullColor, fill);
        GUI.DrawTexture(new Rect(left + 1f, top + 1f, (barWidth - 2f) * fill, barHeight - 2f),
            Texture2D.whiteTexture);

        // Bar dolunca ince bir cerceve ile belli et
        if (fill >= 0.999f)
        {
            GUI.color = barFullColor;
            float t = 1f;
            GUI.DrawTexture(new Rect(left - t, top - t, barWidth + t * 2f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left - t, top + barHeight, barWidth + t * 2f, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left - t, top - t, t, barHeight + t * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(left + barWidth, top - t, t, barHeight + t * 2f), Texture2D.whiteTexture);
        }
    }
}