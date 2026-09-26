using UnityEngine;
using UnityEngine.UI;

// Acilis ekrani: ana menunun gorseli ve buton dili (krem/turuncu kagit, kalin siyah cerceve).
// Kitaplar arkada olusup yere yerlesene kadar acik kalir; kapaninca oyun ve sayac baslar.
public sealed class ShopLoadingScreen : MonoBehaviour
{
    static ShopLoadingScreen instance;
    public static bool IsVisible => instance != null;

    // Ana menu gorseli 1672 x 941; yerlesim ayni koordinatlarla (logonun alti, sag kolon).
    const float ArtW = 1672f, ArtH = 941f, ColX = 1146f, ColW = 478f;
    const float SpawnShare = 0.8f;
    const float TipSeconds = 4.5f;

    float target, shown;
    bool settling;
    Text title, phase, percent, tip;
    RectTransform fill;
    float trackWidth;
    int tipIndex;
    float nextTip;

    public static void Show()
    {
        if (instance == null)
        {
            var go = new GameObject("Shop opening card");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ShopLoadingScreen>();
            instance.Build();
        }
        instance.target = instance.shown = 0f;
        instance.settling = false;
    }

    // Kitaplarin olusturulmasi (0..1).
    public static void Progress(float value)
    {
        if (instance == null) return;
        instance.settling = false;
        instance.target = Mathf.Max(instance.target, Mathf.Clamp01(value) * SpawnShare);
    }

    // Kitaplarin yere inip durmasi (0..1). Ekran bu asama bitince kapanir.
    public static void Settling(float value)
    {
        if (instance == null) return;
        instance.settling = true;
        instance.target = Mathf.Max(instance.target, SpawnShare + Mathf.Clamp01(value) * (1f - SpawnShare));
    }

    public static void Hide()
    {
        if (instance == null) return;
        var old = instance;
        instance = null;
        old.enabled = false;
        Destroy(old.gameObject);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    // ------------------------------------------------------------------ kurulum

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ArtW, ArtH);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();

        // Tum ekrani kaplayan koyu zemin; tiklamalari da arkadaki menuye gecirmez.
        var backdrop = Node("Backdrop", transform);
        backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
        backdrop.gameObject.AddComponent<Image>().color = new Color32(13, 6, 8, 255);

        var art = Node("Art", transform);
        art.anchorMin = art.anchorMax = art.pivot = new Vector2(.5f, .5f);
        art.sizeDelta = new Vector2(ArtW, ArtH);
        var image = art.gameObject.AddComponent<RawImage>();
        image.texture = Resources.Load<Texture2D>("ComicShopMenu/menu_background");
        image.raycastTarget = false;
        if (image.texture == null) image.color = new Color32(13, 6, 8, 255);

        Font display = Loc.Display(null), body = Loc.Body(null);
        Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (display == null) display = builtin;
        if (body == null) body = builtin;

        // Baslik: logodaki gibi turuncu, kalin siyah kontur ve golge.
        title = Label(art, Loc.Upper(Loc.T("load.title")), display, 56, ShopHud.Orange, ColX, 452, ColW, 80, TextAnchor.MiddleCenter);
        var outline = title.gameObject.AddComponent<Outline>();
        outline.effectColor = HudShape.Ink; outline.effectDistance = new Vector2(3, -3);
        var drop = title.gameObject.AddComponent<Shadow>();
        drop.effectColor = HudShape.Ink; drop.effectDistance = new Vector2(5, -6);

        // Ilerleme cubugu: SETTINGS butonu gibi krem kagit kart.
        var card = Shape(art, ShopHud.PaperLight, ColX, 548, ColW, 76, 18, false, true, HudShape.Frame.Menu);
        const float trackX = 20, trackY = 22, trackH = 32;
        trackWidth = ColW - trackX - 118;
        Shape(card, HudShape.Ink, trackX, trackY, trackWidth, trackH, 9, false, false, HudShape.Frame.None);
        fill = Shape(card, ShopHud.Orange, trackX + 3, trackY + 3, 0, trackH - 6, 7, true, false, HudShape.Frame.None);
        percent = Label(card, "0%", display, 38, HudShape.Ink, ColW - 112, 6, 92, 64, TextAnchor.MiddleRight);

        phase = Label(art, "", body, 24, ShopHud.Cream, ColX + 4, 634, ColW - 8, 40, TextAnchor.MiddleLeft);
        var phaseShadow = phase.gameObject.AddComponent<Shadow>();
        phaseShadow.effectColor = new Color32(14, 8, 12, 220); phaseShadow.effectDistance = new Vector2(2, -2);

        // Ipucu karti: turuncu "!" rozeti ve kisa bir oyun ipucu.
        var tipCard = Shape(art, ShopHud.Paper, ColX, 696, ColW, 150, 18, true, true, HudShape.Frame.Menu);
        var badge = Shape(tipCard, ShopHud.Orange, 20, 22, 52, 52, 12, false, false, HudShape.Frame.Menu);
        Label(badge, "!", display, 40, HudShape.Ink, 0, 0, 52, 52, TextAnchor.MiddleCenter);
        tip = Label(tipCard, "", body, 23, HudShape.Ink, 88, 16, ColW - 108, 118, TextAnchor.MiddleLeft);
        tip.horizontalOverflow = HorizontalWrapMode.Wrap;
        tip.resizeTextForBestFit = true; tip.resizeTextMinSize = 14; tip.resizeTextMaxSize = 23;

        tipIndex = Random.Range(0, 3);
        ShowTip();
        Refresh();
    }

    void ShowTip()
    {
        tip.text = Loc.T("load.tip" + (tipIndex % 3 + 1));
        nextTip = Time.unscaledTime + TipSeconds;
    }

    void Update()
    {
        if (Time.unscaledTime >= nextTip) { tipIndex++; ShowTip(); }
        shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 0.9f);
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
        Refresh();
    }

    void Refresh()
    {
        fill.sizeDelta = new Vector2(Mathf.Max(0f, (trackWidth - 6f) * shown), fill.sizeDelta.y);
        percent.text = Loc.Current == GameLanguage.Turkish
            ? "%" + Mathf.FloorToInt(shown * 100f)
            : Mathf.FloorToInt(shown * 100f) + "%";
        float spawn = target / SpawnShare;
        phase.text = settling ? Loc.T("load.p4")
            : spawn < 0.45f ? Loc.T("load.p1") : spawn < 0.85f ? Loc.T("load.p2") : Loc.T("load.p3");
    }

    // ------------------------------------------------------------------ yardimcilar

    static RectTransform Node(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    static RectTransform Shape(Transform parent, Color color, float x, float y, float w, float h, float cut, bool dots, bool shadow, HudShape.Frame frame)
    {
        var go = new GameObject("Shape", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudShape));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Place(rect, x, y, w, h);
        var shape = go.GetComponent<HudShape>();
        shape.raycastTarget = false;
        shape.Set(color, frame, cut, dots, shadow);
        return rect;
    }

    static Text Label(Transform parent, string value, Font font, int size, Color color, float x, float y, float w, float h, TextAnchor align)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, x, y, w, h);
        var text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.color = color; text.alignment = align;
        text.raycastTarget = false; text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value;
        // Uzun ceviriler kutuya sigsin.
        while (text.fontSize > 12 && text.preferredWidth > w) text.fontSize--;
        return text;
    }
}
