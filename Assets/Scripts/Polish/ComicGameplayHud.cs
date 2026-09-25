using UnityEngine;
using UnityEngine.UI;

// Presentation only: reads the local player's inventory and existing interaction feedback.
// All geometry is in the front-end canvas's 1600 x 900 reference space.
public sealed class ComicGameplayHud
{
    static readonly Color Ink = new Color(.10f, .085f, .13f);
    static readonly Color Paper = new Color(.98f, .94f, .83f);
    static readonly Color Orange = new Color(1f, .61f, .25f);
    static readonly Color Teal = new Color(.30f, .76f, .66f);
    static readonly Color Muted = new Color(.48f, .44f, .40f);
    const int VisibleRows = 10;
    readonly Font font;
    readonly Text progress, percent, details, inventoryCount, empty, footer, prompt, promptKey;
    readonly RectTransform progressFill, inventory, instructions;
    readonly Image promptStrip;
    readonly Row[] rows = new Row[VisibleRows];
    readonly Text[] keys = new Text[4];
    readonly Text[] actions = new Text[4];
    readonly GameObject[] controls = new GameObject[4];

    sealed class Row
    {
        public RectTransform root;
        public Image background, stripe;
        public Text number, title, publisher;
    }

    public ComicGameplayHud(RectTransform parent, Font font)
    {
        this.font = font;
        var group = parent.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        var summary = Card(parent, "Shelf progress", 28, 28, 332, 158);
        Box(summary, "Chapter tab", 4, 4, 324, 29, Orange);
        Label(summary, "DÜKKÂNI TOPARLA", 17, 5, 300, 26, 15, Ink, true);
        progress = Label(summary, "0 / 0", 18, 40, 245, 43, 32, Ink, true);
        percent = Label(summary, "%0", 263, 47, 52, 27, 18, Ink, true, TextAnchor.MiddleRight);
        Label(summary, "KİTAP RAFTA", 20, 83, 280, 18, 12, Muted, true);
        var track = Box(summary, "Progress track", 18, 109, 296, 12, Ink);
        progressFill = Box(track, "Progress fill", 3, 3, 0, 6, Teal);
        details = Label(summary, "", 18, 128, 300, 21, 13, Ink);

        inventory = Card(parent, "Held comics", 0, 0, 332, 126);
        inventory.anchorMin = inventory.anchorMax = inventory.pivot = Vector2.one;
        inventory.anchoredPosition = new Vector2(-28, -28);
        Box(inventory, "Inventory tab", 4, 4, 324, 39, Orange);
        Label(inventory, "ELİNDEKİ KİTAPLAR", 16, 8, 224, 30, 16, Ink, true);
        inventoryCount = Label(inventory, "0 / 10", 241, 8, 74, 30, 18, Ink, true, TextAnchor.MiddleRight);
        empty = Label(inventory, "Ellerin boş. İlk kitabını al!", 18, 53, 296, 31, 16, Muted);
        for (int i = 0; i < rows.Length; i++)
        {
            var root = Box(inventory, "Book " + (i + 1), 10, 51 + i * 43, 312, 40, Paper);
            rows[i] = new Row {
                root = root, background = root.GetComponent<Image>(),
                stripe = Box(root, "Selection mark", 0, 0, 4, 40, Orange).GetComponent<Image>(),
                number = Label(root, "", 11, 5, 29, 29, 16, Muted, true),
                title = Label(root, "", 44, 2, 256, 22, 16, Ink, true),
                publisher = Label(root, "", 44, 23, 256, 15, 11, Muted)
            };
            rows[i].root.gameObject.SetActive(false);
        }
        footer = Label(inventory, "", 17, 91, 299, 22, 12, Muted);

        instructions = Card(parent, "Interaction guide", 0, 0, 784, 124);
        instructions.anchorMin = instructions.anchorMax = instructions.pivot = new Vector2(.5f, 0);
        instructions.anchoredPosition = new Vector2(0, 28);
        promptStrip = Box(instructions, "Context accent", 4, 4, 5, 57, Teal).GetComponent<Image>();
        var key = Box(instructions, "Context key", 18, 17, 116, 32, Ink);
        promptKey = Label(key, "SIRADAKİ", 4, 0, 108, 32, 13, Paper, true, TextAnchor.MiddleCenter);
        prompt = Label(instructions, "Bir kitaba yaklaş", 149, 9, 615, 49, 20, Ink, true);
        Box(instructions, "Guide rule", 18, 65, 748, 2, Ink);
        for (int i = 0; i < controls.Length; i++)
        {
            var item = Box(instructions, "Control " + i, 18 + i * 190, 75, 178, 39, Paper);
            controls[i] = item.gameObject;
            var cap = Box(item, "Key cap", 0, 0, 76, 30, Ink);
            keys[i] = Label(cap, "", 3, 0, 70, 30, 11, Paper, true, TextAnchor.MiddleCenter);
            actions[i] = Label(item, "", 83, 0, 95, 36, 12, Ink);
        }
        instructions.gameObject.SetActive(false);
    }

    public void Refresh(PlayerInteraction interaction, string elapsed)
    {
        int placed = GameStats.TotalPlaced, total = GameStats.TotalBooks;
        float ratio = total > 0 ? Mathf.Clamp01((float)placed / total) : 0;
        progress.text = $"{placed:N0} / {total:N0}";
        percent.text = "%" + Mathf.FloorToInt(ratio * 100);
        progressFill.sizeDelta = new Vector2(290 * ratio, 6);
        details.text = $"{elapsed}   /   {GameStats.CompletedBookGroupCount} grup tamamlandı";
        inventory.gameObject.SetActive(interaction != null);
        instructions.gameObject.SetActive(interaction != null && ShopSettings.Current.hints);
        if (interaction == null) return;

        int count = interaction.HeldBooksList.Count;
        inventoryCount.text = $"{count} / {interaction.MaxHeldBooks}";
        int shown = Mathf.Min(count, VisibleRows);
        // Keep the selected book visible if a custom scene raises the carrying limit.
        int start = Mathf.Clamp(interaction.ActiveHeldIndex - VisibleRows + 1, 0, Mathf.Max(0, count - VisibleRows));
        empty.gameObject.SetActive(count == 0);
        float footerY = 56 + Mathf.Max(1, shown) * 43;
        inventory.sizeDelta = new Vector2(332, footerY + 34);
        ((RectTransform)footer.transform).anchoredPosition = new Vector2(17, -footerY);
        footer.text = count > 1 ? (count > VisibleRows ? $"{start + 1}–{start + shown} / {count}   •   TEKERLEK: seç" : "TEKERLEK  /  Kitap değiştir") : "Kitapları aynı yayıncının rafına yerleştir.";
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            row.root.gameObject.SetActive(i < shown);
            if (i >= shown) continue;
            int index = start + i;
            var book = interaction.HeldBooksList[index];
            bool selected = index == interaction.ActiveHeldIndex;
            row.background.color = selected ? Ink : Paper;
            row.stripe.color = selected ? Orange : new Color(.84f, .80f, .71f);
            row.number.text = (index + 1).ToString("00");
            row.number.color = selected ? Orange : Muted;
            row.title.text = book != null ? book.DisplayName : "…";
            row.title.color = selected ? Paper : Ink;
            row.publisher.text = book != null ? BrandConfig.GetBrandName(book.brandID) : "";
            row.publisher.color = selected ? Orange : Muted;
        }
        if (!ShopSettings.Current.hints) return;
        bool holding = interaction.ActiveHeldBook != null;
        string hint = interaction.InteractionHint;
        string pickupPrefix = interaction.pickupKey + ": ";
        string placePrefix = interaction.dropKey + ": ";
        bool pickup = !string.IsNullOrEmpty(hint) && hint.StartsWith(pickupPrefix, System.StringComparison.Ordinal);
        bool place = !string.IsNullOrEmpty(hint) && hint.StartsWith(placePrefix, System.StringComparison.Ordinal);
        bool feedback = !string.IsNullOrEmpty(hint) && !pickup && !place;
        promptKey.text = pickup ? KeyLabel(interaction.pickupKey) : place ? KeyLabel(interaction.dropKey) : feedback ? "DİKKAT" : "SIRADAKİ";
        prompt.text = pickup ? hint.Substring(pickupPrefix.Length) : place ? hint.Substring(placePrefix.Length) : feedback ? hint : holding ? "Aynı yayıncının rafını bul" : "Bir kitaba yaklaş ve al";
        promptStrip.color = feedback ? Orange : Teal;
        Control(0, KeyLabel(interaction.pickupKey), "Kitabı al", true);
        Control(1, KeyLabel(interaction.dropKey), "Yerleştir / bırak", holding);
        Control(2, KeyLabel(interaction.throwKey), "Basılı tut / fırlat", holding && interaction.throwAbilityUnlocked);
        Control(3, "TEKERLEK", "Kitap değiştir", count > 1);
    }

    void Control(int index, string key, string action, bool visible)
    {
        controls[index].SetActive(visible);
        keys[index].text = key;
        actions[index].text = action;
    }

    static string KeyLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0: return "SOL TIK";
            case KeyCode.Mouse1: return "SAĞ TIK";
            case KeyCode.Mouse2: return "ORTA TIK";
            case KeyCode.Space: return "BOŞLUK";
            case KeyCode.LeftShift: return "SOL SHIFT";
            case KeyCode.RightShift: return "SAĞ SHIFT";
            case KeyCode.LeftControl: return "SOL CTRL";
            case KeyCode.RightControl: return "SAĞ CTRL";
            default: return key.ToString().ToUpperInvariant();
        }
    }

    RectTransform Card(Transform parent, string name, float x, float y, float width, float height)
    {
        var root = Box(parent, name, x, y, width, height, Ink);
        var shadow = root.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, .65f);
        shadow.effectDistance = new Vector2(5, -5);
        var paper = Box(root, "Paper", 0, 0, 0, 0, Paper);
        paper.anchorMin = Vector2.zero; paper.anchorMax = Vector2.one;
        paper.offsetMin = new Vector2(3, 3); paper.offsetMax = new Vector2(-3, -3);
        return root;
    }

    static RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        Position(rect, x, y, width, height);
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
        return rect;
    }

    Text Label(Transform parent, string value, float x, float y, float width, float height, int size, Color color, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Position((RectTransform)go.transform, x, y, width, height);
        var text = go.GetComponent<Text>();
        text.font = font; text.fontSize = size; text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        text.color = color; text.alignment = alignment; text.text = value;
        text.supportRichText = false; text.raycastTarget = false;
        text.resizeTextForBestFit = true; text.resizeTextMinSize = Mathf.Max(10, size - 4); text.resizeTextMaxSize = size;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    static void Position(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
}
