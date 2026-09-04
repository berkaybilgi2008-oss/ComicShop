using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Gorunum")]
    [Min(2f)] public float size = 12f;
    [Min(0.5f)] public float thickness = 2f;
    public Color color = Color.white;

    private Texture2D softCircleTexture;

    void Awake()
    {
        CreateSoftCircleTexture();
    }

    void OnDestroy()
    {
        if (softCircleTexture != null)
            Destroy(softCircleTexture);
    }

    void CreateSoftCircleTexture()
    {
        int textureSize = 64;
        softCircleTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        softCircleTexture.wrapMode = TextureWrapMode.Clamp;
        softCircleTexture.filterMode = FilterMode.Bilinear;

        float center = (textureSize - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01((radius - distance) / 1.5f);
                softCircleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        softCircleTexture.Apply();
    }

    void OnGUI()
    {
        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float outerSize = size;
        float innerSize = Mathf.Max(0f, size - thickness * 2f);

        Color oldColor = GUI.color;
        GUI.color = color;

        GUI.DrawTexture(
            new Rect(centerX - outerSize * 0.5f, centerY - outerSize * 0.5f, outerSize, outerSize),
            softCircleTexture,
            ScaleMode.StretchToFill,
            true);

        if (innerSize > 0f)
        {
            Color clearColor = color;
            clearColor.a = 0f;
            GUI.color = clearColor;
            GUI.DrawTexture(
                new Rect(centerX - innerSize * 0.5f, centerY - innerSize * 0.5f, innerSize, innerSize),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill,
                true);
        }

        GUI.color = oldColor;
    }
}
