using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scene-local UI bootstrap. No scene rewrite, light setup or per-object installation.
[DefaultExecutionOrder(900)]
public sealed class ShopFrontEnd : MonoBehaviour
{
    public static bool IsActive { get; private set; }
    enum Page { Session, Settings, Results, Leave, Display }
    Page page;
    int tab;
    bool menuOpen, oldDebug, oldHudEnabled, connectedBefore, dismissedResults;
    ConnectionManager connection;
    GameHUD oldHud;
    Canvas canvas;
    CanvasScaler canvasScaler;
    RectTransform overlay, card, content, hud;
    Text statusText, progressText, hintText, heldText, timerText;
    Font font;
    AudioListener menuListener;
    int lastRevision = -1;
    ConnectionManager.SessionState lastState;
    ShopAction? waitingKey;
    int listenAfterFrame;
    float nextTick, stepDistance;
    Vector3 previousPlayerPosition;
    Transform trackedPlayer;
    int priorWidth, priorHeight;
    FullScreenMode priorMode;
    float displayDeadline;
    int proposedWidth, proposedHeight, proposedMode;
    static readonly Color Ink = new Color(.12f, .10f, .16f, 1);
    static readonly Color Paper = new Color(.96f, .91f, .78f, 1);
    static readonly Color Accent = new Color(.96f, .59f, .25f, 1);
    static readonly Color Muted = new Color(.42f, .39f, .38f, 1);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => IsActive = false;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (FindFirstObjectByType<ShopFrontEnd>() != null || FindFirstObjectByType<BookSpawner>() == null) return;
        new GameObject("ComicShop Session UI", typeof(ShopAudio), typeof(ShopFrontEnd));
    }
    void Awake()
    {
        IsActive = true;
        menuListener = gameObject.AddComponent<AudioListener>();
        foreach (var listener in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
            if (listener != menuListener && listener.enabled) { menuListener.enabled = false; break; }
        connection = FindFirstObjectByType<ConnectionManager>();
        if (connection != null) { oldDebug = connection.showDebugUI; connection.showDebugUI = false; }
        oldHud = FindFirstObjectByType<GameHUD>();
        if (oldHud != null) { oldHudEnabled = oldHud.enabled; oldHud.enabled = false; if (oldHud.hudText != null) oldHud.hudText.text = ""; }
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        var root = new GameObject("ComicShop Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 110;
        var scaler = root.GetComponent<CanvasScaler>(); canvasScaler = scaler; scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null)
        {
            var events = new GameObject("ComicShop Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            events.transform.SetParent(transform, false);
        }
        overlay = Panel(root.transform, "Menu shade", new Color(.04f, .03f, .06f, .83f)); Stretch(overlay);
        card = Panel(overlay, "ComicShop menu", Paper); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(1080, 760); card.anchoredPosition = Vector2.zero;
        hud = Panel(root.transform, "Gameplay HUD", Color.clear); Stretch(hud); hud.GetComponent<Image>().raycastTarget = false;
        var summary = Panel(hud, "Round summary", new Color(Ink.r, Ink.g, Ink.b, .9f)); Rect(summary, 28, 24, 315, 108);
        progressText = Label(summary, "", 20, 16, 275, 30, 25, Paper);
        timerText = Label(summary, "", 20, 57, 275, 26, 17, Accent);
        hintText = Label(hud, "", 320, 795, 960, 75, 23, Paper, TextAnchor.MiddleCenter);
        var hintRect = (RectTransform)hintText.transform; hintRect.anchorMin = hintRect.anchorMax = new Vector2(.5f, 0);
        hintRect.pivot = new Vector2(.5f, 0); hintRect.anchoredPosition = new Vector2(0, 28);
        hintText.gameObject.AddComponent<Shadow>().effectColor = Color.black;
        heldText = Label(hud, "", 1140, 28, 430, 140, 21, Paper, TextAnchor.UpperRight);
        var heldRect = (RectTransform)heldText.transform; heldRect.anchorMin = heldRect.anchorMax = Vector2.one;
        heldRect.pivot = Vector2.one; heldRect.anchoredPosition = new Vector2(-28, -28);
        heldText.gameObject.AddComponent<Shadow>().effectColor = Color.black;
        ShopSettings.Apply();
        if (!Application.isEditor && ShopSettings.Current.width > 0)
            Screen.SetResolution(ShopSettings.Current.width, ShopSettings.Current.height,
                ShopSettings.Current.windowMode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        menuOpen = connection != null;
        Build();
    }
    bool Connected => connection != null ? connection.State == ConnectionManager.SessionState.Connected : ShopRound.State.Active;
    void Update()
    {
        var localAudio = NetworkPlayerSetup.LocalPlayer;
        if (connection != null)
            menuListener.enabled = localAudio == null || localAudio.audioListener == null || !localAudio.audioListener.enabled;
        canvasScaler.matchWidthOrHeight = Screen.width / (float)Mathf.Max(1, Screen.height) >= 1600f / 900f ? 1 : 0;
        if (connection != null && connection.showDebugUI) connection.showDebugUI = false;
        if (waitingKey.HasValue)
        {
            if (Time.frameCount > listenAfterFrame && Input.anyKeyDown)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) { waitingKey = null; Build(); }
                else foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (!Input.GetKeyDown(key)) continue;
                    if (ShopSettings.Rebind(waitingKey.Value, key)) { waitingKey = null; Build(); }
                    else if (statusText != null) statusText.text = "Bu tuş kullanımda. Başka bir tuş seç; Esc iptal eder.";
                    break;
                }
            }
        }
        else if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (page == Page.Display) RevertDisplay();
            else if (page == Page.Settings) { if (Connected) Resume(); else { ShopSettings.Save(); page = Page.Session; Build(); } }
            else if (page == Page.Leave) { page = Page.Session; Build(); }
            else if (Connected) { menuOpen = !menuOpen; page = Page.Session; Build(); }
        }
        if (page == Page.Display && displayDeadline > 0)
        {
            if (Time.unscaledTime >= displayDeadline) RevertDisplay();
            else if (statusText != null) statusText.text = $"{Mathf.CeilToInt(displayDeadline - Time.unscaledTime)} saniye içinde onaylanmazsa eski görüntü geri gelir.";
        }
        bool connected = Connected;
        if (connected && !connectedBefore) { menuOpen = false; dismissedResults = false; page = Page.Session; Build(); }
        if (!connected && connectedBefore) { menuOpen = true; dismissedResults = false; page = Page.Session; Build(); }
        connectedBefore = connected;
        if (connection != null && connection.State != lastState)
        { lastState = connection.State; if (page == Page.Session) Build(); }
        if (Time.unscaledTime >= nextTick)
        {
            nextTick = Time.unscaledTime + .2f;
            ShopRound.TickAuthority();
            var manager = NetworkManager.Singleton;
            if (manager != null && manager.IsServer)
                foreach (var peer in manager.ConnectedClientsList)
                    if (peer.PlayerObject != null && peer.PlayerObject.TryGetComponent<NetworkPlayerSetup>(out var player)) player.PublishRound();
            UpdateHud();
            if (page == Page.Session && statusText != null && connection != null) statusText.text = connection.Status;
        }
        if (lastRevision != ShopRound.Revision)
        {
            lastRevision = ShopRound.Revision;
            if (ShopRound.State.Completed && !dismissedResults)
            { menuOpen = true; page = Page.Results; Build(); }
        }
        bool visible = menuOpen || !connected;
        overlay.gameObject.SetActive(visible); hud.gameObject.SetActive(connected && !visible);
        if (visible && Cursor.lockState == CursorLockMode.Locked) ConnectionManager.SetCursor(false);
        UpdateSteps(connected && !visible);
    }
    void UpdateSteps(bool gameplay)
    {
        var player = NetworkPlayerSetup.LocalPlayer;
        if (player == null || !gameplay || player.IsDown) { trackedPlayer = null; stepDistance = 0; return; }
        if (trackedPlayer != player.transform) { trackedPlayer = player.transform; previousPlayerPosition = trackedPlayer.position; return; }
        float delta = Vector3.ProjectOnPlane(trackedPlayer.position - previousPlayerPosition, Vector3.up).magnitude;
        previousPlayerPosition = trackedPlayer.position;
        var body = player.GetComponent<CharacterController>();
        if (body == null || !body.isGrounded || delta > 1f) { stepDistance = 0; return; }
        stepDistance += delta;
        if (stepDistance >= 1.8f) { stepDistance = 0; ShopAudio.Play(ShopCue.Step, trackedPlayer.position); }
    }
    void UpdateHud()
    {
        progressText.text = $"{GameStats.TotalPlaced:N0} / {GameStats.TotalBooks:N0} kitap";
        timerText.text = $"{TimeLabel(ShopRound.Elapsed)}  •  {GameStats.CompletedBookGroupCount} grup tamam";
        var player = NetworkPlayerSetup.LocalPlayer;
        var interaction = player != null ? player.GetComponent<PlayerInteraction>() : oldHud != null ? oldHud.playerInteraction : null;
        if (interaction == null) { heldText.text = ""; hintText.text = ""; return; }
        var book = interaction.ActiveHeldBook;
        heldText.text = $"ELDE  {interaction.HeldBooksList.Count}/{interaction.MaxHeldBooks}" +
            (book != null ? "\n" + book.DisplayName + "\nTEKERLEK • kitap değiştir" : "");
        if (!ShopSettings.Current.hints) { hintText.text = ""; return; }
        hintText.text = interaction.InteractionHint;
        if (string.IsNullOrEmpty(hintText.text)) hintText.text = book == null
            ? $"Bir kitaba yaklaş  •  {ShopSettings.Key(ShopAction.Pickup)} ile al"
            : $"Aynı yayıncının rafını bul  •  {ShopSettings.Key(ShopAction.Place)} ile yerleştir\n{ShopSettings.Key(ShopAction.Throw)} basılı tut → bırak: şarjlı atış";
    }
    static string TimeLabel(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";
    }
    void Build()
    {
        foreach (Transform child in card) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        statusText = null;
        var rail = Panel(card, "Ink sidebar", Ink); Rect(rail, 0, 0, 300, 760);
        Label(rail, "COMIC\nSHOP", 28, 35, 248, 120, 48, Paper);
        Label(rail, "TIDY UP TOGETHER", 30, 175, 250, 26, 16, Accent);
        var rule = Panel(rail, "Rule", Accent); Rect(rule, 30, 225, 238, 4);
        Label(rail, "Bir kitap.\nDoğru raf.\nBirlikte biten bir tur.", 30, 257, 240, 125, 24, Paper);
        Label(rail, "HER OTURUM YENİ BİR TUR\nKitap düzeni oturum sonunda sıfırlanır. Kişisel ayarların saklanır.", 30, 575, 240, 130, 17, Paper);
        content = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(card, false); Rect(content, 336, 30, 708, 700);
        switch (page)
        {
            case Page.Settings: BuildSettings(); break;
            case Page.Results: BuildResults(); break;
            case Page.Leave: BuildLeave(); break;
            case Page.Display: BuildDisplayConfirmation(); break;
            default: BuildSession(); break;
        }
    }
    void Title(string eyebrow, string title, string subtitle)
    {
        Label(content, eyebrow, 0, 0, 708, 28, 16, Muted);
        Label(content, title, 0, 35, 708, 60, 36, Ink);
        Label(content, subtitle, 0, 103, 708, 62, 19, Muted);
    }
    void BuildSession()
    {
        Title("BİR TUR DAHA?", Connected ? "Dükkân açık." : "Hoş geldin.", Connected ? "Mola verebilirsin; çevrimiçi oda çalışmaya devam eder." : "Yerdeki kitapları yayıncılarına göre raflara yerleştirin.");
        statusText = Label(content, connection != null ? connection.Status : "", 0, 164, 690, 70, 19, Ink);
        if (Connected)
        {
            Label(content, "ODA KODU  " + (connection != null && !string.IsNullOrEmpty(connection.relayJoinCode) ? connection.relayJoinCode : "Yerel oturum"), 0, 246, 708, 35, 25, Ink);
            Button(content, "OYUNA DÖN", 0, 303, 708, 58, Resume, true);
            if (connection != null && !string.IsNullOrEmpty(connection.relayJoinCode)) Button(content, "Oda kodunu kopyala", 0, 380, 340, 48, () => GUIUtility.systemCopyBuffer = connection.relayJoinCode);
            Button(content, "AYARLAR", 0, 456, 708, 52, OpenSettings);
            Button(content, "Oturumdan ayrıl", 0, 531, 708, 52, () => { page = Page.Leave; Build(); });
        }
        else if (connection != null && connection.IsRunning)
        {
            Label(content, "Bağlantı hazırlanıyor…", 0, 290, 708, 65, 28, Ink);
            Button(content, "İPTAL", 0, 410, 708, 58, connection.Disconnect);
        }
        else if (connection != null)
        {
            Button(content, "TEK BAŞINA BAŞLA", 0, 242, 708, 54, () => { connection.maxPlayers = 1; connection.StartHost(); }, true);
            Button(content, "ARKADAŞLARINLA • ODA KUR", 0, 311, 708, 54, () => { connection.maxPlayers = 4; connection.StartRelayHost(); });
            var code = InputField(content, "ODA KODU", connection.relayJoinCode, 0, 388, 450, 54, value => connection.relayJoinCode = value.Trim().ToUpperInvariant());
            code.characterLimit = 16;
            Button(content, "KATIL", 470, 388, 238, 54, connection.StartRelayClient);
            Button(content, "AYARLAR", 0, 481, 340, 54, OpenSettings);
            Button(content, "OYUNDAN ÇIK", 368, 481, 340, 54, () => { page = Page.Leave; Build(); });
            var ip = InputField(content, "Yerel IP", connection.address, 0, 578, 450, 46, value => connection.address = value.Trim());
            ip.characterLimit = 64;
            Button(content, "YEREL AĞA KATIL", 470, 578, 238, 46, () => connection.StartClient());
            Button(content, "YEREL ODA KUR • 4 OYUNCU", 0, 642, 708, 42, () => { connection.maxPlayers = 4; connection.StartHost(); });
        }
    }
    void Resume()
    {
        displayDeadline = 0;
        menuOpen = false; dismissedResults |= ShopRound.State.Completed; page = Page.Session;
        ShopSettings.Save(); ConnectionManager.SetCursor(true); Build();
    }
    void OpenSettings() { page = Page.Settings; tab = 0; Build(); }
    void BuildSettings()
    {
        Title("KENDİ RİTMİNİ BUL", "Ayarlar", "Değişiklikler kişisel; diğer oyuncuların ayarlarını etkilemez.");
        string[] tabs = { "SES & KONFOR", "GÖRÜNTÜ", "KONTROLLER" };
        for (int i = 0; i < tabs.Length; i++) { int choice = i; Button(content, tabs[i], i * 240, 174, 228, 44, () => { tab = choice; waitingKey = null; Build(); }, tab == i); }
        var p = ShopSettings.Current;
        if (tab == 0)
        {
            Slider(content, "Ana ses", 242, 0, 1, p.master, value => p.master = value);
            Slider(content, "Kitaplar, adımlar & arayüz", 320, 0, 1, p.effects, value => p.effects = value);
            Slider(content, "Dükkân ortamı", 398, 0, 1, p.ambience, value => p.ambience = value);
            Button(content, "Etkileşim ipuçları: " + (p.hints ? "AÇIK" : "KAPALI"), 0, 485, 708, 46, () => { p.hints = !p.hints; ShopSettings.Apply(); Build(); });
            Button(content, "Sesi dene", 0, 548, 708, 42, () => ShopAudio.Play(ShopCue.Place, Vector3.zero, false));
        }
        else if (tab == 1)
        {
            Slider(content, "Görüş alanı (FOV)", 233, 55, 105, p.fov, value => { p.fov = value; p.fovChosen = true; });
            Button(content, "VSync: " + (p.vsync ? "AÇIK" : "KAPALI"), 0, 314, 340, 45, () => { p.vsync = !p.vsync; ShopSettings.Apply(); Build(); });
            Button(content, "FPS sınırı: " + p.fps, 368, 314, 340, 45, () => { p.fps = p.fps < 60 ? 60 : p.fps < 120 ? 120 : p.fps < 144 ? 144 : p.fps < 240 ? 240 : 30; ShopSettings.Apply(); Build(); });
            Label(content, "VSync açıkken ekranın yenileme hızı kullanılır.", 0, 367, 708, 30, 16, Muted);
            Button(content, $"Çözünürlük: {Screen.width} × {Screen.height}  →", 0, 414, 708, 46, CycleResolution);
            Button(content, "Ekran: " + (Screen.fullScreenMode == FullScreenMode.Windowed ? "PENCERELİ" : "KENARLIKSIZ") + "  →", 0, 480, 708, 46,
                () => PreviewDisplay(Screen.width, Screen.height, Screen.fullScreenMode == FullScreenMode.Windowed ? 1 : 0));
            Label(content, Application.isEditor ? "Ekran modu/çözünürlük Windows buildde denenir; Editor Game View değiştirilmez." : "Görüntü değişiklikleri 15 saniyelik geri alma korumasıyla uygulanır.", 0, 548, 708, 52, 17, Muted);
        }
        else
        {
            Slider(content, "Fare hassasiyeti", 231, .1f, 10f, p.sensitivity, value => p.sensitivity = value);
            Button(content, "Dikey bakışı ters çevir: " + (p.invertY ? "AÇIK" : "KAPALI"), 0, 308, 708, 38, () => { p.invertY = !p.invertY; ShopSettings.Apply(); Build(); });
            for (int i = 0; i < 10; i++)
            {
                var action = (ShopAction)i;
                Button(content, ShopSettings.Label(action) + "  •  " + ShopSettings.Key(action), (i % 2) * 360, 361 + (i / 2) * 44, 348, 37,
                    () => { waitingKey = action; listenAfterFrame = Time.frameCount + 1; if (statusText != null) statusText.text = ShopSettings.Label(action) + ": yeni tuşa bas. Esc iptal."; });
            }
        }
        statusText = Label(content, "", 0, 590, 708, 37, 17, Ink);
        Button(content, "VARSAYILANLAR", 0, 644, 340, 48, () => { ShopSettings.ResetDefaults(); Build(); });
        Button(content, "KAYDET & GERİ", 368, 644, 340, 48, () => { waitingKey = null; ShopSettings.Save(); page = Page.Session; Build(); }, true);
    }
    void BuildResults()
    {
        Title("TUR TAMAMLANDI", "Her kitap yerini buldu.", "Dükkânı birlikte toparladınız. Biraz dolaşın, eserinizin tadını çıkarın.");
        Label(content, ShopRound.State.Total.ToString("N0"), 0, 226, 708, 92, 76, Ink, TextAnchor.MiddleCenter);
        Label(content, "KİTAP RAFTA", 0, 330, 708, 35, 22, Muted, TextAnchor.MiddleCenter);
        Label(content, "TUR SÜRESİ  " + TimeLabel(ShopRound.Elapsed), 0, 397, 708, 40, 27, Ink, TextAnchor.MiddleCenter);
        Button(content, "DÜKKÂNDA KAL", 0, 500, 708, 58, Resume, true);
        Button(content, "OTURUM MENÜSÜ", 0, 582, 708, 52, () => { dismissedResults = true; page = Page.Session; Build(); });
    }
    void BuildLeave()
    {
        bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Title("AYRILMADAN ÖNCE", Connected ? "Bu turdan ayrıl?" : "Dükkânı kapat?", Connected
            ? host ? "Ev sahibi ayrıldığında herkesin oturumu kapanır. Kitap düzeni saklanmaz; yeni oturum yeni turdur." : "Diğer oyuncular devam edebilir. Elindeki kitaplar dükkânda kalır."
            : "Kişisel ayarların kaydedilecek.");
        Button(content, "VAZGEÇ", 0, 298, 708, 58, () => { page = Page.Session; Build(); }, true);
        Button(content, Connected ? "EVET, OTURUMDAN AYRIL" : "EVET, OYUNDAN ÇIK", 0, 389, 708, 58, () =>
        {
            ShopSettings.Save();
            if (Connected && connection != null) { connection.Disconnect(); page = Page.Session; Build(); }
            else Application.Quit();
        });
    }
    void CycleResolution()
    {
        if (Application.isEditor) return;
        var modes = new List<Vector2Int>();
        foreach (var resolution in Screen.resolutions)
        {
            var mode = new Vector2Int(resolution.width, resolution.height);
            if (mode.x >= 640 && mode.y >= 480 && !modes.Contains(mode)) modes.Add(mode);
        }
        if (modes.Count == 0) return;
        int index = modes.IndexOf(new Vector2Int(Screen.width, Screen.height));
        var next = modes[(index + 1) % modes.Count];
        PreviewDisplay(next.x, next.y, Screen.fullScreenMode == FullScreenMode.Windowed ? 0 : 1);
    }
    void PreviewDisplay(int width, int height, int mode)
    {
        if (Application.isEditor) return;
        priorWidth = Screen.width; priorHeight = Screen.height; priorMode = Screen.fullScreenMode;
        proposedWidth = width; proposedHeight = height; proposedMode = mode;
        Screen.SetResolution(width, height, mode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        displayDeadline = Time.unscaledTime + 15; page = Page.Display; Build();
    }
    void BuildDisplayConfirmation()
    {
        Title("GÖRÜNTÜ KONTROLÜ", "Bu ayarı koru?", $"{proposedWidth} × {proposedHeight}");
        statusText = Label(content, "", 0, 214, 708, 65, 23, Ink);
        Button(content, "EVET, KORU", 0, 330, 708, 58, () =>
        {
            var p = ShopSettings.Current; p.width = proposedWidth; p.height = proposedHeight; p.windowMode = proposedMode;
            displayDeadline = 0; ShopSettings.Save(); page = Page.Settings; Build();
        }, true);
        Button(content, "ESKİ AYARA DÖN", 0, 416, 708, 54, RevertDisplay);
    }
    void RevertDisplay()
    {
        if (displayDeadline > 0) Screen.SetResolution(priorWidth, priorHeight, priorMode);
        displayDeadline = 0; page = Page.Settings; Build();
    }
    void OnApplicationFocus(bool focus)
    {
        if (!focus && IsActive && Connected) { menuOpen = true; ConnectionManager.SetCursor(false); }
    }
    void OnApplicationQuit() => ShopSettings.Save();
    void OnDestroy()
    {
        IsActive = false;
        if (displayDeadline > 0) Screen.SetResolution(priorWidth, priorHeight, priorMode);
        if (connection != null) connection.showDebugUI = oldDebug;
        if (oldHud != null) oldHud.enabled = oldHudEnabled;
    }
    RectTransform Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color; return (RectTransform)go.transform;
    }
    static void Rect(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
    static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    Text Label(Transform parent, string text, float x, float y, float w, float h, int size, Color color, TextAnchor align = TextAnchor.UpperLeft)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var label = go.GetComponent<Text>(); label.font = font; label.fontSize = size; label.color = color;
        label.text = text; label.alignment = align; label.raycastTarget = false; label.supportRichText = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
        Rect((RectTransform)go.transform, x, y, w, h); return label;
    }
    void Button(Transform parent, string text, float x, float y, float w, float h, Action action, bool primary = false)
    {
        var panel = Panel(parent, text, primary ? Accent : Ink); Rect(panel, x, y, w, h);
        var button = panel.gameObject.AddComponent<Button>(); button.targetGraphic = panel.GetComponent<Image>();
        var colors = button.colors; colors.highlightedColor = new Color(.85f, .85f, .85f); colors.pressedColor = new Color(.65f, .65f, .65f); button.colors = colors;
        Label(panel, text, 12, 0, w - 24, h, h < 44 ? 16 : 19, primary ? Ink : Paper, TextAnchor.MiddleCenter);
        button.onClick.AddListener(() => { ShopAudio.Play(ShopCue.Click, Vector3.zero, false); action(); });
    }
    InputField InputField(Transform parent, string placeholder, string value, float x, float y, float w, float h, Action<string> changed)
    {
        var box = Panel(parent, placeholder, new Color(.87f, .81f, .67f)); Rect(box, x, y, w, h);
        var input = box.gameObject.AddComponent<InputField>(); input.targetGraphic = box.GetComponent<Image>();
        input.textComponent = Label(box, "", 15, 0, w - 30, h, 23, Ink, TextAnchor.MiddleLeft);
        input.placeholder = Label(box, placeholder, 15, 0, w - 30, h, 20, Muted, TextAnchor.MiddleLeft);
        input.text = value; input.onValueChanged.AddListener(text => changed(text)); return input;
    }
    void Slider(Transform parent, string title, float y, float min, float max, float value, Action<float> changed)
    {
        var text = Label(parent, title + "  " + value.ToString("0.0"), 0, y, 708, 28, 19, Ink);
        var track = Panel(parent, title, new Color(.75f, .7f, .59f)); Rect(track, 0, y + 39, 708, 12);
        var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
        var handle = Panel(track, "Handle", Ink); handle.sizeDelta = new Vector2(22, 28);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.value = value;
        slider.onValueChanged.AddListener(next => { changed(next); text.text = title + "  " + next.ToString("0.0"); ShopSettings.Apply(); });
    }
}
