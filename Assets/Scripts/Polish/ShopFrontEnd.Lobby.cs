using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

// Oturum ekranlari — ana menu butonlarinin gorunumuyle (krem/turuncu kagit, kalin siyah cerceve,
// baski noktalari, siyah italik yazi). Ana menunun kendisine dokunulmaz.
// - Baglanti yokken (PLAY sonrasi giris ekrani, cikis onayi) secenekler logonun altinda belirir.
// - Oyun icinde (mola, ayrilma onayi, tur sonu) karartilmis oyunun ustunde ayni butonlar belirir.
// Butonlarin davranisi eski ekranlarla birebir aynidir; yalnizca gorunum degisti.
public sealed partial class ShopFrontEnd
{
    RectTransform lobbyRoot;
    bool lobbyLanOpen;
    Font lobbyDisplay, lobbyBody;

    static readonly Color LobbyCream = new Color32(255, 244, 181, 255);
    static readonly Color LobbyCreamDim = new Color32(255, 234, 190, 190);
    static readonly Color LobbyLogo = new Color32(255, 138, 20, 255);

    // Ana menu gorselindeki (1672 x 941) buton kolonu: logonun alti, sag taraf.
    const float ArtW = 1672f, ArtH = 941f, ColumnX = 1146f, ColumnY = 440f, ColumnRight = 1646f, ColumnBottom = 905f;
    const float ColumnWidth = 478f;

    void ClearLobby()
    {
        if (lobbyRoot == null) return;
        lobbyRoot.gameObject.SetActive(false);
        Destroy(lobbyRoot.gameObject);
        lobbyRoot = null;
    }

    // Build() icinden cagrilir; sayfayi burada kurduysa true doner.
    bool BuildLobbyPage()
    {
        if (connection == null) return false;
        // Dile gore font (Latin / Kiril / CJK); dil degisince Build() yeniden cagrilir.
        lobbyDisplay = Loc.Display(font);
        lobbyBody = Loc.Body(font);

        bool connected = Connected;
        if (!connected && (page == Page.Session || page == Page.Leave))
        {
            card.gameObject.SetActive(false);
            lobbyRoot = ArtColumn();
            if (page == Page.Leave) BuildArtLeave(lobbyRoot);
            else if (connection.IsRunning) BuildArtConnecting(lobbyRoot);
            else BuildArtSession(lobbyRoot);
            return true;
        }
        if (connected && (page == Page.Session || page == Page.Leave || page == Page.Results))
        {
            card.gameObject.SetActive(false);
            if (page == Page.Results) BuildResultsPanel();
            else if (page == Page.Leave) BuildLeavePanel();
            else BuildPausePanel();
            return true;
        }
        return false;
    }

    // ----------------------------------------------------------- ana menu kolonu

    void BuildArtSession(RectTransform col)
    {
        float y = 6;
        LobbyButton(col, Loc.T("lobby.solo"), 0, y, ColumnWidth, 84, () => { connection.maxPlayers = 1; connection.StartHost(); }, true, 42);
        y += 96;
        LobbyButton(col, Loc.T("lobby.host_online"), 0, y, ColumnWidth, 58, () => { connection.maxPlayers = 4; connection.StartRelayHost(); }, false, 28);
        y += 70;
        var code = LobbyInput(col, Loc.T("lobby.room_code"), connection.relayJoinCode, 0, y, 306, 56, 28, true,
            value => connection.relayJoinCode = value.Trim().ToUpperInvariant());
        code.characterLimit = 16;
        LobbyButton(col, Loc.T("lobby.join"), 318, y, ColumnWidth - 318, 56, connection.StartRelayClient, false, 30);
        y += 70;

        if (lobbyLanOpen)
        {
            var ip = LobbyInput(col, Loc.T("lobby.local_ip"), connection.address, 0, y, 306, 48, 24, false, value => connection.address = value.Trim());
            ip.characterLimit = 64;
            LobbyButton(col, Loc.T("lobby.connect"), 318, y, ColumnWidth - 318, 48, () => connection.StartClient(), false, 24);
            y += 58;
            LobbyButton(col, Loc.T("lobby.host_local"), 0, y, ColumnWidth, 46, () => { connection.maxPlayers = 4; connection.StartHost(); }, false, 22);
            y += 58;
        }

        LobbyButton(col, Loc.T("lobby.back"), 0, y, 170, 46, () => { page = Page.Title; Build(); }, false, 22);
        LobbyLink(col, lobbyLanOpen ? Loc.T("lobby.lan_hide") : Loc.T("lobby.lan_show"), 190, y, ColumnWidth - 190, 46,
            () => { lobbyLanOpen = !lobbyLanOpen; Build(); });
        y += 56;
        statusText = LobbyText(col, connection.Status, lobbyBody, 16, LobbyCreamDim, 2, y, ColumnWidth - 4, 40, TextAnchor.UpperLeft, true);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    void BuildArtConnecting(RectTransform col)
    {
        LobbyHeading(col, Loc.T("lobby.connecting"), 52, 0, 14, ColumnWidth, 64, TextAnchor.MiddleLeft);
        statusText = LobbyText(col, connection.Status, lobbyBody, 18, LobbyCreamDim, 2, 82, ColumnWidth - 4, 60, TextAnchor.UpperLeft, true);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        LobbyButton(col, Loc.T("lobby.cancel"), 0, 160, ColumnWidth, 60, connection.Disconnect, false, 30);
    }

    void BuildArtLeave(RectTransform col)
    {
        LobbyHeading(col, Loc.T("lobby.close_shop"), 52, 0, 10, ColumnWidth, 64, TextAnchor.MiddleLeft);
        LobbyText(col, Loc.T("lobby.save_note"), lobbyBody, 19, LobbyCreamDim, 2, 74, ColumnWidth, 28, TextAnchor.UpperLeft, true);
        LobbyButton(col, Loc.T("lobby.stay"), 0, 124, ColumnWidth, 76, () => { page = Page.Title; Build(); }, true, 38);
        LobbyButton(col, Loc.T("lobby.quit_game"), 0, 214, ColumnWidth, 56, LobbyQuit, false, 26);
    }

    // --------------------------------------------------------- oyun ici paneller

    void BuildPausePanel()
    {
        bool relay = !string.IsNullOrEmpty(connection.relayJoinCode);
        var col = CenterPanel(480, 500);
        LobbyText(col, Loc.T("pause.eyebrow"), lobbyBody, 16, LobbyCreamDim, 2, 0, 476, 22, TextAnchor.UpperLeft, true);
        LobbyHeading(col, Loc.T("pause.title"), 84, 0, 18, 480, 96, TextAnchor.UpperLeft);

        var codeBox = LobbyShape(col, ShopHud.PaperLight, 0, 124, 480, 72, false);
        LobbyText(codeBox, Loc.T("lobby.room_code"), lobbyBody, 13, ShopHud.Brown, 22, 12, 200, 18);
        LobbyText(codeBox, relay ? connection.relayJoinCode : Loc.T("pause.local"), lobbyDisplay, 30, ComicInk, 21, 28, 300, 36, TextAnchor.UpperLeft, false, true);
        if (relay) LobbyButton(codeBox, Loc.T("pause.copy"), 480 - 150, 14, 136, 44, () => GUIUtility.systemCopyBuffer = connection.relayJoinCode, true, 22);

        LobbyButton(col, Loc.T("pause.resume"), 0, 212, 480, 84, Resume, true, 44);
        LobbyButton(col, Loc.T("pause.settings"), 0, 308, 480, 62, OpenSettings, false, 30);
        LobbyButton(col, Loc.T("pause.leave"), 0, 382, 480, 62, () => { page = Page.Leave; Build(); }, false, 30);
        statusText = LobbyText(col, connection.Status, lobbyBody, 15, LobbyCreamDim, 2, 456, 476, 40, TextAnchor.UpperLeft, true);
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    void BuildLeavePanel()
    {
        bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        var col = CenterPanel(480, 340);
        LobbyText(col, Loc.T("leave.eyebrow"), lobbyBody, 16, LobbyCreamDim, 2, 0, 476, 22, TextAnchor.UpperLeft, true);
        LobbyHeading(col, Loc.T("leave.title"), 58, 0, 18, 480, 70, TextAnchor.UpperLeft);
        var note = LobbyText(col, host
                ? Loc.T("leave.host_note")
                : Loc.T("leave.client_note"),
            lobbyBody, 18, LobbyCreamDim, 2, 94, 476, 52, TextAnchor.UpperLeft, true);
        note.horizontalOverflow = HorizontalWrapMode.Wrap;
        LobbyButton(col, Loc.T("lobby.stay"), 0, 164, 480, 80, () => { page = Page.Session; Build(); }, true, 40);
        LobbyButton(col, Loc.T("leave.confirm"), 0, 258, 480, 62, LobbyQuit, false, 28);
    }

    void BuildResultsPanel()
    {
        var col = CenterPanel(520, 470);
        LobbyText(col, Loc.T("results.eyebrow"), lobbyBody, 16, LobbyCreamDim, 0, 0, 520, 22, TextAnchor.UpperCenter, true);
        LobbyHeading(col, Loc.T("results.title"), 50, 0, 20, 520, 62, TextAnchor.UpperCenter);
        LobbyHeading(col, Loc.Number(ShopRound.State.Total), 110, 0, 84, 520, 120, TextAnchor.MiddleCenter);
        LobbyText(col, Loc.T("results.summary", TimeLabel(ShopRound.Elapsed)), lobbyBody, 18, LobbyCream, 0, 206, 520, 26, TextAnchor.UpperCenter, true);
        LobbyButton(col, Loc.T("results.stay"), 20, 262, 480, 84, Resume, true, 42);
        LobbyButton(col, Loc.T("results.menu"), 20, 360, 480, 62, () => { dismissedResults = true; page = Page.Session; Build(); }, false, 30);
    }

    void LobbyQuit()
    {
        ShopSettings.Save();
        if (Connected && connection != null) { connection.Disconnect(); page = Page.Session; Build(); }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // ------------------------------------------------------------------ yapi taslari

    RectTransform ArtColumn()
    {
        var rect = new GameObject("Lobby column", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(titleArt, false);
        rect.anchorMin = new Vector2(ColumnX / ArtW, 1f - ColumnBottom / ArtH);
        rect.anchorMax = new Vector2(ColumnRight / ArtW, 1f - ColumnY / ArtH);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    // Ana menudeki gibi: panel yok, butonlar dogrudan karartilmis arka planin ustunde.
    RectTransform CenterPanel(float width, float height)
    {
        var holder = new GameObject("Lobby column", typeof(RectTransform)).GetComponent<RectTransform>();
        holder.SetParent(overlay, false);
        holder.anchorMin = holder.anchorMax = holder.pivot = new Vector2(.5f, .5f);
        holder.sizeDelta = new Vector2(width, height); holder.anchoredPosition = Vector2.zero;
        lobbyRoot = holder;
        return holder;
    }

    RectTransform LobbyShape(Transform parent, Color fill, float x, float y, float w, float h, bool dots)
    {
        var go = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(HudShape));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; Rect(r, x, y, w, h);
        var g = go.GetComponent<HudShape>(); g.raycastTarget = false;
        g.Set(fill, HudShape.Frame.Menu, 16, dots, true);
        return r;
    }

    // Ana menu butonunun aynisi: turuncu (PLAY gibi) ya da krem kagit (SETTINGS gibi).
    Button LobbyButton(Transform parent, string title, float x, float y, float w, float h, Action click, bool primary, int size)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(HudShape));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; Rect(r, x, y, w, h);
        var shape = go.GetComponent<HudShape>();
        shape.Set(primary ? ShopHud.Orange : ShopHud.Paper, HudShape.Frame.Menu, Mathf.Min(20, h * .3f), true, true);
        var button = go.AddComponent<Button>();
        button.targetGraphic = shape; button.transition = Selectable.Transition.None;
        go.AddComponent<ComicShopMenuFeedback>();
        var text = LobbyText(r, title, lobbyDisplay, size, ComicInk, 12, 0, w - 24, h, TextAnchor.MiddleCenter, false, true);
        text.rectTransform.anchoredPosition += new Vector2(0, -1);
        button.onClick.AddListener(() => { ShopAudio.Play(ShopCue.Click, Vector3.zero, false); click(); });
        return button;
    }

    // Logodaki gibi: turuncu, kalin siyah konturlu, golgeli baslik.
    Text LobbyHeading(Transform parent, string value, int size, float x, float y, float w, float h, TextAnchor align)
    {
        var text = LobbyText(parent, value, lobbyDisplay, size, LobbyLogo, x, y, w, h, align);
        var ink = text.gameObject.AddComponent<Outline>();
        ink.effectColor = ComicInk; ink.effectDistance = new Vector2(3, -3);
        var drop = text.gameObject.AddComponent<Shadow>();
        drop.effectColor = ComicInk; drop.effectDistance = new Vector2(5, -6);
        return text;
    }

    // Kutusuz, alti cizgili metin butonu (ikincil secenekler icin).
    void LobbyLink(Transform parent, string title, float x, float y, float w, float h, Action click)
    {
        var go = new GameObject(title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; Rect(r, x, y, w, h);
        var hit = go.GetComponent<Image>(); hit.color = Color.clear;
        var text = LobbyText(r, title, lobbyBody, 16, LobbyCreamDim, 0, 0, w, h, TextAnchor.MiddleRight, true);
        var underline = new GameObject("Underline", typeof(RectTransform), typeof(Image));
        underline.transform.SetParent(r, false);
        var u = (RectTransform)underline.transform;
        u.anchorMin = u.anchorMax = u.pivot = new Vector2(1, .5f);
        u.sizeDelta = new Vector2(text.preferredWidth, 1); u.anchoredPosition = new Vector2(0, -12);
        var line = underline.GetComponent<Image>(); line.color = new Color32(255, 234, 190, 90); line.raycastTarget = false;
        var button = go.GetComponent<Button>();
        button.targetGraphic = text; button.transition = Selectable.Transition.None;
        go.AddComponent<ComicShopMenuFeedback>();
        button.onClick.AddListener(() => { ShopAudio.Play(ShopCue.Click, Vector3.zero, false); click(); });
    }

    InputField LobbyInput(Transform parent, string placeholder, string value, float x, float y, float w, float h, int size, bool display, Action<string> changed)
    {
        var go = new GameObject(placeholder, typeof(RectTransform), typeof(CanvasRenderer), typeof(HudShape));
        go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; Rect(r, x, y, w, h);
        var graphic = go.GetComponent<HudShape>();
        graphic.Set(ShopHud.PaperLight, HudShape.Frame.Menu, Mathf.Min(16, h * .3f), false, true);
        var input = go.AddComponent<InputField>();
        input.targetGraphic = graphic;
        var f = display ? lobbyDisplay : lobbyBody;
        input.textComponent = LobbyText(r, "", f, size, ComicInk, 22, 0, w - 44, h, TextAnchor.MiddleLeft);
        input.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        var hint = LobbyText(r, placeholder, f, size, new Color(ComicInk.r, ComicInk.g, ComicInk.b, .38f), 22, 0, w - 44, h, TextAnchor.MiddleLeft);
        input.placeholder = hint;
        input.customCaretColor = true; input.caretColor = ComicInk; input.caretWidth = 2;
        input.selectionColor = new Color(ComicOrange.r, ComicOrange.g, ComicOrange.b, .45f);
        input.text = value ?? "";
        input.onValueChanged.AddListener(text => changed(text));
        return input;
    }

    static void FitSize(Text text, float maxWidth, int minSize)
    {
        if (text.horizontalOverflow != HorizontalWrapMode.Overflow) return;
        while (text.fontSize > minSize && text.preferredWidth > maxWidth) text.fontSize--;
    }

    Text LobbyText(Transform parent, string value, Font f, int size, Color color, float x, float y, float w, float h,
        TextAnchor align = TextAnchor.UpperLeft, bool shadow = false, bool bold = false)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Rect((RectTransform)go.transform, x, y, w, h);
        var text = go.GetComponent<Text>();
        text.font = f; text.fontSize = size; text.fontStyle = FontStyle.Normal; text.color = color;
        text.alignment = align; text.supportRichText = false; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        text.text = value ?? "";
        // Uzun ceviriler kutusuna sigmazsa yazi boyu kuculur; hicbir yazi tasmaz.
        if (align != TextAnchor.UpperLeft || h <= size * 1.6f) FitSize(text, w, Mathf.Max(11, Mathf.RoundToInt(size * .6f)));
        if (shadow)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color32(14, 8, 12, 200); s.effectDistance = new Vector2(2, -2);
        }
        return text;
    }
}
