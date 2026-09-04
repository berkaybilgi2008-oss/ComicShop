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

        // Etkilesim raycast'inin kitap/shelf layer ayarina bagli kalmasini engelle.
        // PlayerInteraction kendi hitlerini BookItem/ShelfSlot tipine gore filtreler.
        PlayerInteraction interaction = FindFirstObjectByType<PlayerInteraction>();
        if (interaction != null)
            interaction.interactMask = ~0;
    }

    void OnDestroy()
    {
        if (ringTexture != null)
            Destroy(ringTexture);
    }

    void CreateRingTexture()
    {
        const int textureSize = 64;
        ringTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        ringTexture.wrapMode = TextureWrapMode.Clamp;
        ringTexture.filterMode = FilterMode.Bilinear;

        float center = (textureSize - 1) * 0.5f;
        float outerRadius = center - 1f;
        float ringWidth = Mathf.Clamp(thickness, 0.5f, 4f);
        float innerRadius = outerRadius - ringWidth;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float outerAlpha = Mathf.Clamp01(outerRadius + 1f - distance);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius + 1f);
                float alpha = outerAlpha * innerAlpha;
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        ringTexture.Apply();
    }

    void OnGUI()
    {
        if (ringTexture == null)
            return;

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
