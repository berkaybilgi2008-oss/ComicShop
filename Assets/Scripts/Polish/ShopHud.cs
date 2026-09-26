using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Oyun ici HUD — ana menu butonlarinin gorunumuyle (kagit/turuncu panel, kalin siyah cerceve,
// baski noktalari). Sol ust: raftaki kitaplar. Sag ust: eldeki kitaplar. Alt: tus ipuclari.
// Tum yazilar Loc uzerinden gelir; dil degisince fontlar ve metinler aninda yenilenir.
// Kucuk pencerede (orn. Editor Game view) HUD okunur kalsin diye kendini buyutur.
public sealed class ShopHud
{
    // Ana menu gorsellerinden olculen renkler.
    public static readonly Color Ink = new Color32(10, 6, 5, 255), Paper = new Color32(242, 191, 121, 255),
        PaperLight = new Color32(251, 226, 178, 255), Orange = new Color32(245, 151, 2, 255),
        Cream = new Color32(255, 244, 181, 255), Brown = new Color32(96, 52, 16, 255),
        Red = new Color32(214, 58, 18, 255);
    static readonly Color PipEmpty = new Color32(214, 160, 90, 255);

    static readonly Color[] SpineColors =
    {
        new Color32(245, 151, 2, 255), new Color32(214, 58, 18, 255), new Color32(255, 214, 64, 255),
        new Color32(92, 140, 70, 255), new Color32(40, 140, 150, 255), new Color32(70, 120, 200, 255),
        new Color32(140, 90, 190, 255), new Color32(225, 100, 140, 255), new Color32(150, 80, 40, 255),
        new Color32(170, 190, 60, 255), new Color32(250, 240, 220, 255), new Color32(90, 100, 115, 255)
    };

    const float Margin = 22f;
    const float ProgressWidth = 320f, ProgressHeight = 134f;
    const float HeldWidth = 340f, RowTop = 72f, RowHeight = 28f, RowStep = 30f, RowInset = 13f;
    const int MaxVisibleRows = 8;

    readonly Font fallback;
    Font display, body;
    readonly RectTransform root;
    Canvas canvas;
    float lastBoost = -1f;

    // Metin -> hangi font ailesi (dil degisince yeniden atanir).
    readonly List<Text> displayTexts = new List<Text>();
    readonly List<Text> bodyTexts = new List<Text>();

    // Ilerleme
    RectTransform progressCard;
    Text progressEyebrow, countText, percentText, footerText;
    RectTransform barFill;
    int lastPlaced = -1, lastTotal = -1, lastSeconds = -1, lastGroups = -1, lastGroupTotal = -1;

    // Eldeki kitaplar
    RectTransform heldCard, pipsRoot, footer;
    Text heldTitle, heldCountText, emptyText, moreText, targetEyebrow, targetBrandText;
    HudShape targetSwatch;
    readonly List<HudShape> pips = new List<HudShape>();
    readonly List<Row> rows = new List<Row>();
    readonly Dictionary<int, string> brandNames = new Dictionary<int, string>();
    int lastCount = -1, lastMax = -1, lastStart = -1;
    BookItem lastTarget;
    Vector2 lastHeldSize;

    // Alt ipuclari
    RectTransform promptBar;
    CanvasGroup promptGroup;
    readonly List<Prompt> prompts = new List<Prompt>();
    string promptSignature = "";
    float promptChangedAt, promptAlpha = 1f;
    RectTransform toast;
    CanvasGroup toastGroup;
    Text toastText;
    float toastAlpha;
    string toastMessage = "";

    sealed class Row
    {
        public RectTransform rect;
        public HudShape highlight, spine;
        public Text title, brand;
        public BookItem book;
        public int state = -1;
    }

    sealed class Prompt
    {
        public RectTransform rect;
        public HudShape cap;
        public Text key, label;
        public float width;
        public bool hot;
    }

    public ShopHud(RectTransform parent, Font fallbackFont)
    {
        root = parent;
        fallback = fallbackFont;
        display = Loc.Display(fallback);
        body = Loc.Body(fallback);
        canvas = root.GetComponentInParent<Canvas>();
        var group = root.GetComponent<CanvasGroup>();
        if (group == null) group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false; group.blocksRaycasts = false;
        BuildProgress();
        BuildHeld();
        BuildPrompts();
        ApplyLanguage();
        Loc.Changed += ApplyLanguage;
    }

    // ------------------------------------------------------------------ kurulum

    void BuildProgress()
    {
        var card = Shape(root, "HUD progress", Paper, HudShape.Frame.Menu, 16);
        progressCard = card.rectTransform;
        Place(progressCard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Margin, -Margin + 2), new Vector2(ProgressWidth, ProgressHeight));
        var r = progressCard;

        progressEyebrow = Body(r, "", 15, Brown);
        TopLeft(progressEyebrow.rectTransform, 22, 15, 210, 20);
        percentText = Display(r, "%0", 22, Ink, TextAnchor.UpperRight);
        TopLeft(percentText.rectTransform, ProgressWidth - 122, 12, 100, 26);
        countText = Display(r, "", 42, Ink, TextAnchor.UpperLeft, true);
        TopLeft(countText.rectTransform, 21, 34, ProgressWidth - 42, 50);

        var track = Shape(r, "Bar track", Ink, HudShape.Frame.None, 4, false, false);
        TopLeft(track.rectTransform, 22, 88, ProgressWidth - 44, 12);
        var fill = Shape(track.rectTransform, "Bar fill", Orange, HudShape.Frame.None, 3, false, false);
        barFill = fill.rectTransform;
        barFill.anchorMin = new Vector2(0, 0); barFill.anchorMax = new Vector2(0, 1); barFill.pivot = new Vector2(0, .5f);
        barFill.anchoredPosition = new Vector2(2, 0); barFill.sizeDelta = new Vector2(0, -4);

        footerText = Body(r, "", 16, Brown);
        TopLeft(footerText.rectTransform, 22, 104, ProgressWidth - 44, 20);
    }

    void BuildHeld()
    {
        var card = Shape(root, "HUD held books", Paper, HudShape.Frame.Menu, 16);
        heldCard = card.rectTransform;
        Place(heldCard, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-Margin, -Margin + 2), new Vector2(HeldWidth, 140));

        // PLAY butonu gibi turuncu baslik seridi.
        var header = Shape(heldCard, "Header", Orange, HudShape.Frame.Thin, 8, true, false);
        TopLeft(header.rectTransform, 12, 12, HeldWidth - 24, 40);
        heldTitle = Display(header.rectTransform, "", 20, Ink, TextAnchor.MiddleLeft);
        Stretch(heldTitle.rectTransform, 12, 0, 74, 0);
        heldCountText = Display(header.rectTransform, "0/10", 22, Ink, TextAnchor.MiddleRight);
        Stretch(heldCountText.rectTransform, 12, 0, 12, 0);

        pipsRoot = Node(heldCard, "Capacity pips");
        TopLeft(pipsRoot, 14, 58, HeldWidth - 28, 7);

        emptyText = Body(heldCard, "", 16, Brown);
        TopLeft(emptyText.rectTransform, 16, RowTop + 3, HeldWidth - 32, 24);

        moreText = Body(heldCard, "", 14, Brown, TextAnchor.UpperRight);

        footer = Node(heldCard, "Target shelf");
        footer.anchorMin = new Vector2(0, 0); footer.anchorMax = new Vector2(1, 0); footer.pivot = new Vector2(.5f, 0);
        footer.offsetMin = new Vector2(0, 0); footer.offsetMax = new Vector2(0, 52);
        var rule = Shape(footer, "Rule", new Color(Ink.r, Ink.g, Ink.b, .35f), HudShape.Frame.None, 0, false, false).rectTransform;
        rule.anchorMin = new Vector2(0, 1); rule.anchorMax = new Vector2(1, 1); rule.pivot = new Vector2(.5f, 1);
        rule.offsetMin = new Vector2(16, -2); rule.offsetMax = new Vector2(-16, 0);
        targetEyebrow = Body(footer, "", 14, Brown, TextAnchor.MiddleLeft);
        TopLeft(targetEyebrow.rectTransform, 18, 6, 120, 32);
        targetSwatch = Shape(footer, "Swatch", Orange, HudShape.Frame.Thin, 2, false, false);
        targetBrandText = Display(footer, "", 21, Ink, TextAnchor.MiddleLeft);
    }

    void BuildPrompts()
    {
        var bar = Shape(root, "HUD key prompts", Paper, HudShape.Frame.Menu, 12);
        promptBar = bar.rectTransform;
        Place(promptBar, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 24), new Vector2(200, 56));
        promptGroup = promptBar.gameObject.AddComponent<CanvasGroup>();

        var shape = Shape(root, "HUD warning", Orange, HudShape.Frame.Menu, 12);
        toast = shape.rectTransform;
        Place(toast, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 94), new Vector2(200, 50));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0;
        toastText = Display(toast, "", 22, Ink, TextAnchor.MiddleCenter);
        Stretch(toastText.rectTransform, 18, 0, 18, 1);
    }

    Row CreateRow()
    {
        var row = new Row();
        row.rect = Node(heldCard, "Held book");
        row.rect.anchorMin = row.rect.anchorMax = new Vector2(0, 1); row.rect.pivot = new Vector2(0, 1);
        row.rect.sizeDelta = new Vector2(HeldWidth - RowInset * 2, RowHeight);
        row.highlight = Shape(row.rect, "Selected", Orange, HudShape.Frame.Thin, 7, false, false);
        Stretch(row.highlight.rectTransform, 0, 0, 0, 0);
        row.spine = Shape(row.rect, "Spine", Orange, HudShape.Frame.Thin, 1, false, false);
        Place(row.spine.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(8, 0), new Vector2(11, 20));
        row.title = Body(row.rect, "", 17, Ink, TextAnchor.MiddleLeft);
        row.brand = Body(row.rect, "", 13, Brown, TextAnchor.MiddleRight);
        rows.Add(row);
        return row;
    }

    Prompt CreatePrompt()
    {
        var p = new Prompt();
        p.rect = Node(promptBar, "Prompt");
        p.rect.anchorMin = p.rect.anchorMax = new Vector2(0, .5f); p.rect.pivot = new Vector2(0, .5f);
        p.cap = Shape(p.rect, "Key", Ink, HudShape.Frame.None, 6, false, false);
        p.cap.rectTransform.anchorMin = p.cap.rectTransform.anchorMax = new Vector2(0, .5f);
        p.cap.rectTransform.pivot = new Vector2(0, .5f);
        // Tus etiketi kucuk oldugu icin okunakli govde fontuyla yazilir.
        p.key = Body(p.cap.rectTransform, "", 15, Cream, TextAnchor.MiddleCenter);
        Stretch(p.key.rectTransform, 3, 0, 3, 1);
        p.label = Body(p.rect, "", 18, Ink, TextAnchor.MiddleLeft);
        p.label.rectTransform.anchorMin = p.label.rectTransform.anchorMax = new Vector2(0, .5f);
        p.label.rectTransform.pivot = new Vector2(0, .5f);
        prompts.Add(p);
        return p;
    }

    // ------------------------------------------------------------------ dil

    void ApplyLanguage()
    {
        if (root == null) { Loc.Changed -= ApplyLanguage; return; }
        display = Loc.Display(fallback);
        body = Loc.Body(fallback);
        foreach (var t in displayTexts) if (t != null) t.font = display;
        foreach (var t in bodyTexts) if (t != null) t.font = body;
        progressEyebrow.text = Loc.T("hud.shelved");
        heldTitle.text = Loc.T("hud.held");
        emptyText.text = Loc.T("hud.empty");
        targetEyebrow.text = Loc.T("hud.target");
        float eyebrowWidth = Mathf.Min(targetEyebrow.preferredWidth, 150);
        TopLeft(targetSwatch.rectTransform, 18 + eyebrowWidth + 10, 13, 12, 22);
        TopLeft(targetBrandText.rectTransform, 18 + eyebrowWidth + 30, 6, HeldWidth - (eyebrowWidth + 66), 34);
        FitWidth(heldTitle, HeldWidth - 24 - 90, 14);
        // Onbellekleri bosalt ki her sey yeni dilde/fontta yeniden olculsun.
        brandNames.Clear();
        lastPlaced = lastTotal = lastSeconds = lastGroups = lastGroupTotal = -1;
        lastCount = lastMax = lastStart = -1; lastTarget = null;
        foreach (var row in rows) { row.state = -1; row.book = null; }
        foreach (var p in prompts) p.width = 0;
        promptSignature = ""; toastMessage = "";
    }

    // ----------------------------------------------------------------- guncelleme

    public void Refresh(PlayerInteraction interaction)
    {
        UpdateScale();
        RefreshProgress();
        RefreshHeld(interaction);
        RefreshPrompts(interaction);
    }

    // 1600x900 referansli tuvalde kucuk pencerede yazilar cok kuculur; HUD'u biraz buyut.
    void UpdateScale()
    {
        if (canvas == null) canvas = root.GetComponentInParent<Canvas>();
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float boost = Mathf.Clamp(0.92f / Mathf.Max(0.1f, scale), 1f, 1.45f);
        if (Mathf.Abs(boost - lastBoost) < 0.01f) return;
        lastBoost = boost;
        var v = new Vector3(boost, boost, 1);
        progressCard.localScale = heldCard.localScale = promptBar.localScale = toast.localScale = v;
    }

    void RefreshProgress()
    {
        int placed = GameStats.TotalPlaced, total = GameStats.TotalBooks;
        if (placed != lastPlaced || total != lastTotal)
        {
            lastPlaced = placed; lastTotal = total;
            countText.text = Loc.Number(placed) + "<size=26><color=#60341094> / " + Loc.Number(total) + "</color></size>";
            float ratio = total > 0 ? Mathf.Clamp01(placed / (float)total) : 0;
            percentText.text = Loc.Current == GameLanguage.Turkish ? "%" + Mathf.FloorToInt(ratio * 100) : Mathf.FloorToInt(ratio * 100) + "%";
            float trackWidth = ProgressWidth - 44 - 4;
            barFill.sizeDelta = new Vector2(ratio > 0 ? Mathf.Max(6, trackWidth * ratio) : 0, -4);
        }
        int seconds = Mathf.FloorToInt((float)ShopRound.Elapsed);
        int groups = GameStats.CompletedBookGroupCount, groupTotal = GameStats.totalBookTypes;
        if (seconds != lastSeconds || groups != lastGroups || groupTotal != lastGroupTotal)
        {
            lastSeconds = seconds; lastGroups = groups; lastGroupTotal = groupTotal;
            footerText.text = TimeLabel(seconds) + "   ·   " + Loc.T("hud.groups", groups, groupTotal);
        }
    }

    void RefreshHeld(PlayerInteraction interaction)
    {
        var held = interaction != null ? interaction.HeldBooksList : null;
        int count = held != null ? held.Count : 0;
        int max = interaction != null ? interaction.MaxHeldBooks : 0;
        int active = interaction != null && count > 0 ? Mathf.Clamp(interaction.ActiveHeldIndex, 0, count - 1) : -1;

        if (count != lastCount || max != lastMax)
        {
            heldCountText.text = count + "/" + max;
            heldCountText.color = max > 0 && count >= max ? Red : Ink;
            RefreshPips(count, max);
        }

        int shown = Mathf.Min(count, MaxVisibleRows);
        int start = count > MaxVisibleRows ? Mathf.Clamp(active - MaxVisibleRows / 2, 0, count - MaxVisibleRows) : 0;

        while (rows.Count < shown) CreateRow();
        bool listChanged = count != lastCount || start != lastStart;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            int index = start + i;
            var book = i < shown ? held[index] : null;
            bool visible = book != null;
            if (row.rect.gameObject.activeSelf != visible) row.rect.gameObject.SetActive(visible);
            if (!visible) { row.book = null; continue; }
            if (listChanged) row.rect.anchoredPosition = new Vector2(RowInset, -(RowTop + i * RowStep));
            int state = index == active ? 1 : 0;
            if (row.book != book || row.state != state)
            {
                row.book = book; row.state = state;
                StyleRow(row, book, state == 1);
            }
        }

        bool windowed = count > MaxVisibleRows;
        if (windowed)
        {
            moreText.text = (start + 1) + "–" + (start + shown) + "  /  " + count;
            TopLeft(moreText.rectTransform, 16, RowTop + shown * RowStep + 1, HeldWidth - 32, 18);
        }
        if (moreText.gameObject.activeSelf != windowed) moreText.gameObject.SetActive(windowed);
        if (emptyText.gameObject.activeSelf != (count == 0)) emptyText.gameObject.SetActive(count == 0);
        if (footer.gameObject.activeSelf != (count > 0)) footer.gameObject.SetActive(count > 0);

        var target = active >= 0 ? held[active] : null;
        if (target != lastTarget)
        {
            lastTarget = target;
            if (target != null)
            {
                targetBrandText.text = Fit(targetBrandText, BrandName(target.brandID), targetBrandText.rectTransform.sizeDelta.x);
                targetSwatch.Fill = SpineColor(target.brandID);
            }
        }

        float bodyHeight = count == 0 ? 34 : shown * RowStep + (windowed ? 22 : 0);
        var size = new Vector2(HeldWidth, RowTop + bodyHeight + (count > 0 ? 58 : 10));
        if (size != lastHeldSize) { lastHeldSize = size; heldCard.sizeDelta = size; }

        lastCount = count; lastMax = max; lastStart = start;
    }

    void StyleRow(Row row, BookItem book, bool selected)
    {
        row.spine.Fill = SpineColor(book.brandID);
        row.highlight.gameObject.SetActive(selected);
        row.title.color = Ink;
        row.brand.color = selected ? Ink : Brown;

        // Yayinci adi sagda sabit; satirda serinin adi ve sayisi gosterilir (ad verisi degismez).
        // Sigmayan ad "…" ile kisalir, hicbir yazi digerinin ustune binmez.
        float rowWidth = HeldWidth - RowInset * 2;
        row.brand.text = Fit(row.brand, BrandName(book.brandID), 118);
        float brandWidth = row.brand.preferredWidth;
        Place(row.brand.rectTransform, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-10, 0), new Vector2(brandWidth + 2, RowHeight));
        float titleWidth = rowWidth - 28 - 10 - brandWidth - 12;
        Place(row.title.rectTransform, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, 0), new Vector2(titleWidth, RowHeight));
        row.title.text = Fit(row.title, SeriesTitle(book.DisplayName), titleWidth);
    }

    // "YAYINCI - SERI #3" -> "SERI #3". Yayinci zaten satirin saginda yaziyor.
    static string SeriesTitle(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        int separator = name.IndexOf(" - ", System.StringComparison.Ordinal);
        return separator > 0 && separator + 3 < name.Length ? name.Substring(separator + 3) : name;
    }

    void RefreshPips(int count, int max)
    {
        bool show = max > 0 && max <= 24;
        pipsRoot.gameObject.SetActive(show);
        if (!show) return;
        const float gap = 4f;
        float width = HeldWidth - 28;
        float pip = Mathf.Min(26, (width - gap * (max - 1)) / max);
        while (pips.Count < max) pips.Add(Shape(pipsRoot, "Pip", PipEmpty, HudShape.Frame.None, 2, false, false));
        for (int i = 0; i < pips.Count; i++)
        {
            bool on = i < max;
            pips[i].gameObject.SetActive(on);
            if (!on) continue;
            TopLeft(pips[i].rectTransform, i * (pip + gap), 0, pip, 7);
            bool filled = i < count;
            pips[i].Set(filled ? (count >= max ? Red : Orange) : PipEmpty, filled ? HudShape.Frame.Thin : HudShape.Frame.None, 2, false, false);
        }
    }

    void RefreshPrompts(PlayerInteraction interaction)
    {
        var kind = interaction != null ? interaction.CurrentHintKind : PlayerInteraction.HintKind.None;
        string hint = interaction != null && kind != PlayerInteraction.HintKind.None ? interaction.InteractionHint : string.Empty;
        bool lookingAtBook = kind == PlayerInteraction.HintKind.Pickup;
        bool lookingAtSlot = kind == PlayerInteraction.HintKind.Place;
        bool warning = kind == PlayerInteraction.HintKind.Warning;
        string placeLabel = Loc.T("prompt.place");
        if (lookingAtSlot)
        {
            int colon = hint.IndexOf(": ", System.StringComparison.Ordinal);
            if (colon >= 0 && colon + 2 < hint.Length) placeLabel = hint.Substring(colon + 2);
        }

        // Kisa uyari etiketi — ipuclari kapaliyken de gorunur.
        if (warning && hint != toastMessage)
        {
            toastMessage = hint;
            toastText.text = Loc.Upper(hint);
            toast.sizeDelta = new Vector2(Mathf.Min(toastText.preferredWidth + 48, 900), 50);
        }
        bool calm = ComicInterfaceOptions.ReducedMotion;
        toastAlpha = Mathf.MoveTowards(toastAlpha, warning ? 1 : 0, Time.unscaledDeltaTime * (warning ? 9f : 5f));
        toastGroup.alpha = toastAlpha;
        toast.anchoredPosition = new Vector2(0, 24 + 70 * Mathf.Max(1f, lastBoost) - (calm ? 0 : (1 - toastAlpha) * 6));

        int used = 0;
        var signature = new System.Text.StringBuilder();
        bool highlight = false;
        if (interaction != null && ShopSettings.Current.hints)
        {
            int held = interaction.HeldBooksList.Count;
            bool full = held >= interaction.MaxHeldBooks;
            if (held == 0)
            {
                Add(ref used, signature, KeyName(interaction.pickupKey), Loc.T("prompt.pickup"), lookingAtBook);
                highlight = lookingAtBook;
            }
            else
            {
                Add(ref used, signature, KeyName(interaction.dropKey), placeLabel, lookingAtSlot);
                if (lookingAtBook && !full) Add(ref used, signature, KeyName(interaction.pickupKey), Loc.T("prompt.pickup"), true);
                if (interaction.throwAbilityUnlocked) Add(ref used, signature, KeyName(interaction.throwKey), Loc.T("prompt.throw"), false);
                if (held > 1) Add(ref used, signature, Loc.T("key.wheel"), Loc.T("prompt.select"), false);
                highlight = lookingAtSlot || (lookingAtBook && !full);
            }
        }

        bool visible = used > 0;
        if (promptBar.gameObject.activeSelf != visible) promptBar.gameObject.SetActive(visible);
        if (!visible) return;

        string sig = signature.ToString();
        if (sig != promptSignature)
        {
            promptSignature = sig; promptChangedAt = Time.unscaledTime;
            LayoutPrompts(used);
        }
        // Bir sure degismeyen ipuclari solar; kitaba/rafa bakinca yeniden belirir.
        float target = highlight || Time.unscaledTime - promptChangedAt < 5f ? 1f : .82f;
        promptAlpha = Mathf.MoveTowards(promptAlpha, target, Time.unscaledDeltaTime * (target > promptAlpha ? 6f : 1.2f));
        promptGroup.alpha = promptAlpha;
    }

    void Add(ref int used, System.Text.StringBuilder signature, string key, string label, bool hot)
    {
        var p = used < prompts.Count ? prompts[used] : CreatePrompt();
        used++;
        signature.Append(key).Append('|').Append(label).Append(hot ? '*' : '-');
        if (p.key.text == key && p.label.text == label && p.hot == hot && p.width > 0) return;
        p.hot = hot;
        p.key.text = key;
        p.key.color = hot ? Ink : Cream;
        float capWidth = Mathf.Max(32, p.key.preferredWidth + 20);
        p.cap.rectTransform.sizeDelta = new Vector2(capWidth, 32);
        p.cap.rectTransform.anchoredPosition = Vector2.zero;
        // Aktif tus PLAY butonu gibi turuncu, digerleri siyah tus.
        p.cap.Set(hot ? Orange : Ink, hot ? HudShape.Frame.Thin : HudShape.Frame.None, 6, false, false);
        p.label.text = label;
        p.label.color = hot ? Ink : Brown;
        float labelWidth = Mathf.Min(p.label.preferredWidth, 360);
        p.label.text = Fit(p.label, label, 360);
        p.label.rectTransform.sizeDelta = new Vector2(labelWidth + 2, 32);
        p.label.rectTransform.anchoredPosition = new Vector2(capWidth + 10, 0);
        p.width = capWidth + 10 + labelWidth;
    }

    void LayoutPrompts(int used)
    {
        const float pad = 22f, gap = 26f;
        float x = pad;
        for (int i = 0; i < prompts.Count; i++)
        {
            var p = prompts[i];
            bool on = i < used;
            if (p.rect.gameObject.activeSelf != on) p.rect.gameObject.SetActive(on);
            if (!on) continue;
            p.rect.sizeDelta = new Vector2(p.width, 32);
            p.rect.anchoredPosition = new Vector2(x, 0);
            x += p.width + gap;
        }
        promptBar.sizeDelta = new Vector2(x - gap + pad, 56);
    }

    // ------------------------------------------------------------------ yardimcilar

    string BrandName(int brandID)
    {
        if (!brandNames.TryGetValue(brandID, out var name))
        {
            name = BrandConfig.GetBrandName(brandID);
            name = string.IsNullOrEmpty(name) ? Loc.T("hud.publisher", brandID + 1) : name.ToUpperInvariant();
            brandNames[brandID] = name;
        }
        return name;
    }

    public static Color SpineColor(int brandID) => SpineColors[Mathf.Abs(brandID) % SpineColors.Length];

    static string Fit(Text text, string value, float maxWidth)
    {
        text.text = value;
        if (text.preferredWidth <= maxWidth) return value;
        int lo = 0, hi = value.Length;
        while (lo < hi)
        {
            int mid = (lo + hi + 1) / 2;
            text.text = value.Substring(0, mid).TrimEnd() + "…";
            if (text.preferredWidth <= maxWidth) lo = mid; else hi = mid - 1;
        }
        return value.Substring(0, lo).TrimEnd() + "…";
    }

    // Uzun ceviriler kutuya sigmazsa yazi boyunu kucult (asgari boyuta kadar).
    static void FitWidth(Text text, float maxWidth, int minSize)
    {
        while (text.fontSize > minSize && text.preferredWidth > maxWidth) text.fontSize--;
    }

    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return Loc.T("key.lmb");
            case KeyCode.Mouse1: return Loc.T("key.rmb");
            case KeyCode.Mouse2: return Loc.T("key.mmb");
            case KeyCode.Space: return Loc.T("key.space");
            case KeyCode.LeftShift: case KeyCode.RightShift: return "SHIFT";
            case KeyCode.LeftControl: case KeyCode.RightControl: return "CTRL";
            case KeyCode.LeftAlt: case KeyCode.RightAlt: return "ALT";
            case KeyCode.Return: return "ENTER";
            case KeyCode.Tab: return "TAB";
            case KeyCode.CapsLock: return "CAPS";
            case KeyCode.Backspace: return Loc.T("key.backspace");
            case KeyCode.UpArrow: return Loc.T("key.up");
            case KeyCode.DownArrow: return Loc.T("key.down");
            case KeyCode.LeftArrow: return Loc.T("key.left");
            case KeyCode.RightArrow: return Loc.T("key.right");
        }
        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return ((int)(key - KeyCode.Alpha0)).ToString();
        if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return "NUM " + (int)(key - KeyCode.Keypad0);
        if (key >= KeyCode.Mouse3 && key <= KeyCode.Mouse6) return Loc.T("key.mouse", (int)(key - KeyCode.Mouse0 + 1));
        return key.ToString().ToUpperInvariant();
    }

    static string TimeLabel(int seconds)
    {
        seconds = Mathf.Max(0, seconds);
        int h = seconds / 3600, m = seconds / 60 % 60, s = seconds % 60;
        return h > 0 ? $"{h:00}:{m:00}:{s:00}" : $"{m:00}:{s:00}";
    }

    static HudShape Shape(Transform parent, string name, Color fill, HudShape.Frame frame, float cut, bool dots = true, bool shadow = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(HudShape));
        go.transform.SetParent(parent, false);
        var shape = go.GetComponent<HudShape>();
        shape.raycastTarget = false;
        shape.Set(fill, frame, cut, dots, shadow);
        return shape;
    }

    static RectTransform Node(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Basliklar ve sayilar: menu fontu. Kontur/efekt yok (kucuk boyda bulaniklastiriyordu).
    Text Display(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, bool rich = false)
    {
        var label = Txt(parent, text, display, size, color, align, rich);
        displayTexts.Add(label);
        return label;
    }

    Text Body(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
    {
        var label = Txt(parent, text, body, size, color, align, false);
        bodyTexts.Add(label);
        return label;
    }

    static Text Txt(Transform parent, string text, Font font, int size, Color color, TextAnchor align, bool rich)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>();
        label.font = font; label.fontSize = size; label.fontStyle = FontStyle.Normal; label.color = color;
        label.alignment = align; label.supportRichText = rich; label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
        label.text = text;
        return label;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
        rect.anchoredPosition = position; rect.sizeDelta = size;
    }

    static void TopLeft(RectTransform rect, float x, float y, float width, float height) =>
        Place(rect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));

    static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
}
