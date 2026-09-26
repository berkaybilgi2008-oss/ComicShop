using UnityEngine;

// Cizgi-roman temali nisangah: siyah cerceveli krem nokta.
// Kitaba ya da rafa bakinca yalnizca noktanin rengi turuncuya doner.
// Sarjli atista nokta cevresinde dolan boncuklar (pip) gosterilir.
public class Crosshair : MonoBehaviour
{
    [Tooltip("0 = gizli. PlayerInteraction her karede bu degeri yazar.")]
    [Range(0f, 1f)] public float chargeAmount = 0f;

    [Tooltip("Genel boyut carpani (1 = 1080p icin varsayilan).")]
    [Range(0.5f, 2f)] public float scale = 1f;

    static readonly Color Ink = new Color32(10, 6, 5, 255);
    static readonly Color Cream = new Color32(255, 244, 181, 255);
    static readonly Color Orange = new Color32(245, 151, 2, 255);
    static readonly Color Red = new Color32(214, 58, 18, 255);
    const int Pips = 12;

    static Texture2D disc;
    PlayerInteraction interaction;
    float focus, warn, charge, chargeShown;

    void Awake()
    {
        interaction = GetComponent<PlayerInteraction>();
        if (disc == null) disc = CreateDisc(64);
    }

    static Texture2D CreateDisc(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        float center = (size - 1) * 0.5f, radius = size * 0.5f - 1f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance + 0.5f)));
            }
        texture.Apply();
        return texture;
    }

    void Update()
    {
        var kind = interaction != null ? interaction.CurrentHintKind : PlayerInteraction.HintKind.None;
        bool hot = kind == PlayerInteraction.HintKind.Pickup || kind == PlayerInteraction.HintKind.Place;
        float dt = Time.unscaledDeltaTime;
        focus = Mathf.MoveTowards(focus, hot ? 1f : 0f, dt * 9f);
        warn = Mathf.MoveTowards(warn, kind == PlayerInteraction.HintKind.Warning ? 1f : 0f, dt * 9f);
        charge = Mathf.Clamp01(chargeAmount);
        chargeShown = charge <= 0.001f ? 0f : Mathf.MoveTowards(chargeShown, charge, dt * 6f);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || disc == null || ShopLoadingScreen.IsVisible) return;
        Color oldColor = GUI.color;
        float s = scale * Mathf.Clamp(Screen.height / 1080f, 0.85f, 2f);
        Vector2 c = new Vector2(Mathf.Round(Screen.width * 0.5f), Mathf.Round(Screen.height * 0.5f));
        float ease = 1f - (1f - focus) * (1f - focus);

        Color fill = Color.Lerp(Color.Lerp(Cream, Orange, ease), Red, warn);

        // Nokta: yumusak golge, kalin siyah cerceve, dolgu.
        float r = 3.2f * s;
        Disc(c + new Vector2(1.2f, 1.6f) * s, r + 2.2f * s, new Color(0f, 0f, 0f, 0.35f));
        Disc(c, r + 2f * s, Ink);
        Disc(c, r, fill);

        // Sarjli atis: nokta cevresinde dolan boncuklar.
        if (chargeShown > 0.001f)
        {
            bool full = charge >= 0.999f;
            float pulse = full ? 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
            float ring = 14f * s * pulse, pip = 1.5f * s;
            float filled = chargeShown * Pips;
            for (int i = 0; i < Pips; i++)
            {
                float angle = (i / (float)Pips) * Mathf.PI * 2f;
                Vector2 p = c + new Vector2(Mathf.Sin(angle), -Mathf.Cos(angle)) * ring;
                float amount = Mathf.Clamp01(filled - i);
                Color on = Color.Lerp(Orange, Red, full ? 1f : chargeShown * chargeShown);
                Disc(p, pip + 1f * s, Ink);
                Disc(p, pip, Color.Lerp(new Color(Cream.r, Cream.g, Cream.b, 0.55f), on, amount));
            }
        }

        GUI.color = oldColor;
    }

    static void Disc(Vector2 center, float radius, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), disc, ScaleMode.StretchToFill, true);
    }

}
