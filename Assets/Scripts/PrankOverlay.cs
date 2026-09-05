using UnityEngine;

/// <summary>
/// Ekranin ortasina kapatilamayan bir gorsel basar. Sirf saka icin.
///
/// Oyun icinden kapanmaz -- kurtulmanin tek yolu bu bileseni objeden
/// kaldirmak ya da Inspector'dan devre disi birakmak. Zaten espri de bu.
///
/// Oyunu OYNANMAZ hale getirmez: girdiler engellenmez, oyuncu Q'ya basip
/// kitap firlatabilir. Sadece kenarlardan oyunun devam ettigi gorunur.
///
/// KURULUM:
/// 1) Sahnede herhangi bir objeye (orn. Player) bu script'i ekle.
/// 2) Image alanina saka gorselini surukle.
/// </summary>
public class PrankOverlay : MonoBehaviour
{
    [Header("Gorsel")]
    public Texture2D image;

    [Tooltip("Gorselin ekrani kaplama orani. 0.8 = ekranin %80'i, kenarlarda oyun gorunur.")]
    [Range(0.3f, 1f)] public float screenCoverage = 0.8f;

    [Tooltip("Gorselin arkasindaki karartma. 0 = yok.")]
    [Range(0f, 0.9f)] public float backdropDarkness = 0.35f;

    [Header("Yazilar")]
    public string topText = "(q ya basmayi denemelisin brom)";
    public string bottomText = "(gpt den istedigim tasarim kesinlikle bu degildi)";
    public Color textColor = new Color(1f, 0.95f, 0.2f);
    [Tooltip("Yazi boyutu ekran yuksekligine gore olceklenir.")]
    [Range(0.015f, 0.08f)] public float textScale = 0.035f;

    private Texture2D solid;

    void Awake()
    {
        solid = Texture2D.whiteTexture;
    }

    void OnGUI()
    {
        if (image == null)
            return;

        // Negatif depth = en ustte cizilir, nisangahin ve her seyin onunde.
        GUI.depth = -1000;

        float screenW = Screen.width;
        float screenH = Screen.height;

        // Arka karartma
        if (backdropDarkness > 0f)
        {
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, backdropDarkness);
            GUI.DrawTexture(new Rect(0f, 0f, screenW, screenH), solid);
            GUI.color = old;
        }

        // Gorseli en-boy oranini bozmadan kaplama oranina sigdir
        float maxW = screenW * screenCoverage;
        float maxH = screenH * screenCoverage;
        float aspect = (float)image.width / image.height;

        float drawW = maxW;
        float drawH = drawW / aspect;

        if (drawH > maxH)
        {
            drawH = maxH;
            drawW = drawH * aspect;
        }

        Rect imageRect = new Rect(
            (screenW - drawW) * 0.5f,
            (screenH - drawH) * 0.5f,
            drawW,
            drawH);

        GUI.DrawTexture(imageRect, image, ScaleMode.StretchToFill, true);

        // Yazilar
        int fontSize = Mathf.Max(10, Mathf.RoundToInt(screenH * textScale));

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };

        float textHeight = fontSize * 2.4f;

        DrawOutlinedText(
            new Rect(imageRect.x, imageRect.y - textHeight * 0.9f, imageRect.width, textHeight),
            topText, style);

        DrawOutlinedText(
            new Rect(imageRect.x, imageRect.yMax - textHeight * 0.1f, imageRect.width, textHeight),
            bottomText, style);
    }

    /// <summary>Yaziyi once siyah cerceveyle, sonra renkli olarak cizer -- her zeminde okunur.</summary>
    private void DrawOutlinedText(Rect rect, string text, GUIStyle style)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Color old = GUI.color;
        float o = Mathf.Max(1f, style.fontSize * 0.09f);

        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x - o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x + o, rect.y, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y - o, rect.width, rect.height), text, style);
        GUI.Label(new Rect(rect.x, rect.y + o, rect.width, rect.height), text, style);

        GUI.color = textColor;
        GUI.Label(rect, text, style);

        GUI.color = old;
    }
}
