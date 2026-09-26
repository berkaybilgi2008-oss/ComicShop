using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scene-local UI bootstrap. No scene rewrite, light setup or per-object installation.
[DefaultExecutionOrder(900)]
public sealed partial class ShopFrontEnd : MonoBehaviour
{
    public static bool IsActive { get; private set; }
    enum Page { Title, Session, Settings, Results, Leave, Display, Credits }
    Page page;
    Page settingsReturn = Page.Title;
    RectTransform titleArt, titleButtons;
    AudioSource menuMusic;
    int tab;
    bool menuOpen, oldDebug, oldHudEnabled, connectedBefore, dismissedResults;
    bool gameplayStarted, pendingGameplayStart;
    ConnectionManager connection;
    GameHUD oldHud;
    Canvas canvas;
    CanvasScaler canvasScaler;
    RectTransform overlay, card, content, hud;
    Text statusText;
    ShopHud gameplayHud;
    Font font, baseFont;
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
    static readonly Color Ink = new Color(.055f, .035f, .065f, 1);
    static readonly Color Paper = new Color(1f, .85f, .57f, 1);
    static readonly Color Accent = new Color(1f, .59f, .035f, 1);
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
        font = Resources.Load<Font>("ComicShopMenu/ComicNeue-Bold");
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        baseFont = font;
        font = Loc.Body(baseFont);
        Loc.Changed += OnLanguageChanged;
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
        CreateTitleArt(overlay);
        card = Panel(overlay, "ComicShop menu", Paper); card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.pivot = new Vector2(.5f, .5f); card.sizeDelta = new Vector2(1080, 760); card.anchoredPosition = Vector2.zero;
        hud = Panel(root.transform, "Gameplay HUD", Color.clear); Stretch(hud); hud.GetComponent<Image>().raycastTarget = false;
        gameplayHud = new ShopHud(hud, font);
        ShopSettings.Apply();
        if (!Application.isEditor && ShopSettings.Current.width > 0)
            Screen.SetResolution(ShopSettings.Current.width, ShopSettings.Current.height,
                ShopSettings.Current.windowMode == 0 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
        menuOpen = connection != null;
        page = menuOpen ? Page.Title : Page.Session;
        menuMusic = gameObject.AddComponent<AudioSource>();
        menuMusic.playOnAwake = false; menuMusic.loop = true; menuMusic.spatialBlend = 0;
        menuMusic.clip = Resources.Load<AudioClip>("ComicShopMenu/Sunday_Morning_Vinyl");
        menuMusic.volume = 0;
        if (menuMusic.clip != null) menuMusic.Play();
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
                    else if (statusText != null) statusText.text = Loc.T("rebind.in_use");
                    break;
                }
            }
        }
        else if (!ShopLoadingScreen.IsVisible && !pendingGameplayStart && Input.GetKeyDown(KeyCode.Escape))
        {
            if (page == Page.Display) RevertDisplay();
            else if (page == Page.Settings) { if (Connected) Resume(); else BackFromSettings(); }
            else if (page == Page.Leave || page == Page.Credits) { page = Connected ? Page.Session : Page.Title; Build(); }
            else if (Connected)
            {
                if (menuOpen) Resume();
                else { menuOpen = true; page = Page.Session; ConnectionManager.SetCursor(false); Build(); }
            }
            else if (page == Page.Session && (connection == null || !connection.IsRunning)) { page = Page.Title; Build(); }
        }
        if (page == Page.Display && displayDeadline > 0)
        {
            if (Time.unscaledTime >= displayDeadline) RevertDisplay();
            else if (statusText != null) statusText.text = Loc.T("display.countdown", Mathf.CeilToInt(displayDeadline - Time.unscaledTime));
        }
        bool connected = Connected;
        if (connected && !connectedBefore)
        {
            dismissedResults = false;
            gameplayStarted = false;
            pendingGameplayStart = true;
            menuOpen = false;
            page = Page.Session;
            Build();
        }
        // Do not treat focus callbacks during spawning/loading as a pause request.
        // Acquire the cursor once the local player and the game window are ready.
        if (pendingGameplayStart && connected && ShopRound.State.Active && !ShopLoadingScreen.IsVisible && Application.isFocused &&
            (connection == null || NetworkPlayerSetup.LocalPlayer != null))
        {
            pendingGameplayStart = false;
            Resume();
            gameplayStarted = true;
        }
        if (!connected && connectedBefore) { gameplayStarted = pendingGameplayStart = false; menuOpen = true; dismissedResults = false; page = Page.Title; Build(); }
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
        if (menuMusic != null)
        {
            float target = connected ? 0f : ShopSettings.Current.master * ShopSettings.Current.music;
            menuMusic.volume = Mathf.MoveTowards(menuMusic.volume, target, Time.unscaledDeltaTime * .7f);
            if (menuMusic.volume <= 0 && connected && menuMusic.isPlaying) menuMusic.Pause();
            else if (!connected && !menuMusic.isPlaying && menuMusic.clip != null) menuMusic.UnPause();
        }
        UpdateSteps(connected && !visible);
        if (connected && !visible) UpdateHud();
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
        var player = NetworkPlayerSetup.LocalPlayer;
        var interaction = player != null ? player.GetComponent<PlayerInteraction>() : oldHud != null ? oldHud.playerInteraction : null;
        gameplayHud.Refresh(interaction);
    }
    static string TimeLabel(double seconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return time.TotalHours >= 1 ? $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}" : $"{time.Minutes:00}:{time.Seconds:00}";
    }
    // Dil degisince acik sayfa yeni dilde ve dile uygun fontla yeniden kurulur (HUD kendini yeniler).
    void OnLanguageChanged() { if (this != null && card != null) Build(); }

    void Build()
    {
        font = Loc.Body(baseFont != null ? baseFont : font);
        foreach (Transform child in card) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        ClearLobby();
        PrepareComicFrame();
        statusText = null;
        bool title = page == Page.Title;
        titleArt.gameObject.SetActive(!Connected);
        titleButtons.gameObject.SetActive(title);
        card.gameObject.SetActive(!title);
        overlay.GetComponent<Image>().color = title ? Color.black : new Color(.02f, .01f, .03f, .88f);
        if (title) return;
        if (BuildLobbyPage()) return;
        var border = card.GetComponent<Outline>();
        if (border == null) border = card.gameObject.AddComponent<Outline>();
        border.effectColor = Ink; border.effectDistance = new Vector2(7, -7);
        var rail = Panel(card, "Ink sidebar", Ink); Rect(rail, 0, 0, 300, 760);
        Label(rail, "COMIC\nSHOP", 28, 35, 248, 120, 48, Paper);
        Label(rail, "TIDY UP TOGETHER", 30, 175, 250, 26, 16, Accent);
        var rule = Panel(rail, "Rule", Accent); Rect(rule, 30, 225, 238, 4);
        Label(rail, Loc.T("rail.tagline"), 30, 257, 240, 125, 24, Paper);
        Label(rail, Loc.T("rail.note"), 30, 575, 240, 130, 17, Paper);
        content = new GameObject("Page", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(card, false); Rect(content, 336, 30, 708, 700);
        switch (page)
        {
            case Page.Credits: BuildCredits(); break;
            case Page.Settings: BuildSettings(); break;
            case Page.Results: BuildResults(); break;
            case Page.Leave: BuildLeave(); break;
            case Page.Display: BuildDisplayConfirmation(); break;
            default: BuildSession(); break;
        }
        ApplyComicTheme();
    }
    void Title(string eyebrow, string title, string subtitle)
    {
        Label(content, eyebrow, 0, 0, 708, 28, 16, Muted);
        var heading = Label(content, title, 0, 35, 708, 60, 40, Ink);
        heading.font = Loc.Display(baseFont); heading.fontStyle = FontStyle.Normal;
        Label(content, subtitle, 0, 103, 708, 62, 19, Muted);
    }
    void BuildSession()
    {
        Title(Loc.T("session.eyebrow"), Connected ? Loc.T("session.open") : Loc.T("session.welcome"), Connected ? Loc.T("session.open_sub") : Loc.T("session.welcome_sub"));
        statusText = Label(content, connection != null ? connection.Status : "", 0, 164, 690, 70, 19, Ink);
        if (Connected)
        {
            Label(content, Loc.T("lobby.room_code") + "  " + (connection != null && !string.IsNullOrEmpty(connection.relayJoinCode) ? connection.relayJoinCode : Loc.T("pause.local")), 0, 246, 708, 35, 25, Ink);
            Button(content, Loc.T("pause.resume"), 0, 303, 708, 58, Resume, true);
            if (connection != null && !string.IsNullOrEmpty(connection.relayJoinCode)) Button(content, Loc.T("session.copy_code"), 0, 380, 340, 48, () => GUIUtility.systemCopyBuffer = connection.relayJoinCode);
            Button(content, Loc.T("pause.settings"), 0, 456, 708, 52, OpenSettings);
            Button(content, Loc.T("pause.leave"), 0, 531, 708, 52, () => { page = Page.Leave; Build(); });
        }
        else if (connection != null && connection.IsRunning)
        {
            Label(content, Loc.T("session.preparing"), 0, 290, 708, 65, 28, Ink);
            Button(content, Loc.T("lobby.cancel"), 0, 410, 708, 58, connection.Disconnect);
        }
        else if (connection != null)
        {
            Button(content, Loc.T("lobby.solo"), 0, 242, 708, 54, () => { connection.maxPlayers = 1; connection.StartHost(); }, true);
            Button(content, Loc.T("lobby.host_online"), 0, 311, 708, 54, () => { connection.maxPlayers = 4; connection.StartRelayHost(); });
            var code = InputField(content, Loc.T("lobby.room_code"), connection.relayJoinCode, 0, 388, 450, 54, value => connection.relayJoinCode = value.Trim().ToUpperInvariant());
            code.characterLimit = 16;
            Button(content, Loc.T("lobby.join"), 470, 388, 238, 54, connection.StartRelayClient);
            Button(content, Loc.T("pause.settings"), 0, 481, 340, 54, OpenSettings);
            Button(content, Loc.T("lobby.quit_game"), 368, 481, 340, 54, () => { page = Page.Leave; Build(); });
            var ip = InputField(content, Loc.T("lobby.local_ip"), connection.address, 0, 578, 450, 46, value => connection.address = value.Trim());
            ip.characterLimit = 64;
            Button(content, Loc.T("lobby.connect"), 470, 578, 238, 46, () => connection.StartClient());
            Button(content, Loc.T("lobby.host_local"), 0, 642, 708, 42, () => { connection.maxPlayers = 4; connection.StartHost(); });
        }
    }
    void Resume()
    {
        displayDeadline = 0;
        menuOpen = false; dismissedResults |= ShopRound.State.Completed; page = Page.Session;
        ShopSettings.Save(); ConnectionManager.SetCursor(true); Build();
    }
    void OpenSettings() { settingsReturn = page; page = Page.Settings; tab = 0; Build(); }
    void BackFromSettings() { waitingKey = null; ShopSettings.Save(); page = settingsReturn; Build(); }
    void BuildSettings()
    {
        BuildComicSettings();
    }
    void BuildResults()
    {
        Title(Loc.T("results.eyebrow"), Loc.T("results.title"), "");
        Label(content, Loc.Number(ShopRound.State.Total), 0, 226, 708, 92, 76, Ink, TextAnchor.MiddleCenter);
        Label(content, Loc.T("hud.shelved"), 0, 330, 708, 35, 22, Muted, TextAnchor.MiddleCenter);
        Label(content, TimeLabel(ShopRound.Elapsed), 0, 397, 708, 40, 27, Ink, TextAnchor.MiddleCenter);
        Button(content, Loc.T("results.stay"), 0, 500, 708, 58, Resume, true);
        Button(content, Loc.T("results.menu"), 0, 582, 708, 52, () => { dismissedResults = true; page = Page.Session; Build(); });
    }
    void BuildLeave()
    {
        bool host = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        Title(Loc.T("leave.eyebrow"), Connected ? Loc.T("leave.title") : Loc.T("lobby.close_shop"), Connected
            ? host ? Loc.T("leave.host_note") : Loc.T("leave.client_note")
            : Loc.T("lobby.save_note"));
        Button(content, Loc.T("lobby.stay"), 0, 298, 708, 58, () => { page = Page.Session; Build(); }, true);
        Button(content, Connected ? Loc.T("leave.confirm") : Loc.T("lobby.quit_game"), 0, 389, 708, 58, () =>
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
        Title(Loc.T("display.eyebrow"), Loc.T("display.title"), $"{proposedWidth} × {proposedHeight}");
        statusText = Label(content, "", 0, 214, 708, 65, 23, Ink);
        Button(content, Loc.T("display.keep"), 0, 330, 708, 58, () =>
        {
            var p = ShopSettings.Current; p.width = proposedWidth; p.height = proposedHeight; p.windowMode = proposedMode;
            displayDeadline = 0; ShopSettings.Save(); page = Page.Settings; Build();
        }, true);
        Button(content, Loc.T("display.revert"), 0, 416, 708, 54, RevertDisplay);
    }
    void RevertDisplay()
    {
        if (displayDeadline > 0) Screen.SetResolution(priorWidth, priorHeight, priorMode);
        displayDeadline = 0; page = Page.Settings; Build();
    }
    void OnApplicationFocus(bool focus)
    {
        if (!focus && IsActive && Connected && gameplayStarted && !ShopLoadingScreen.IsVisible && !menuOpen)
        {
            menuOpen = true;
            page = Page.Session;
            ConnectionManager.SetCursor(false);
            Build();
        }
    }
    void OnApplicationQuit() => ShopSettings.Save();
    void OnDestroy()
    {
        Loc.Changed -= OnLanguageChanged;
        IsActive = false;
        if (displayDeadline > 0) Screen.SetResolution(priorWidth, priorHeight, priorMode);
        if (connection != null) connection.showDebugUI = oldDebug;
        if (oldHud != null) oldHud.enabled = oldHudEnabled;
    }
    void CreateTitleArt(Transform parent)
    {
        titleArt = new GameObject("Comic cover", typeof(RectTransform)).GetComponent<RectTransform>();
        titleArt.SetParent(parent, false); Stretch(titleArt);
        var fit = titleArt.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = 1672f / 941f; fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        var art = titleArt.gameObject.AddComponent<RawImage>();
        art.texture = Resources.Load<Texture2D>("ComicShopMenu/menu_background"); art.raycastTarget = false;
        art.gameObject.AddComponent<ComicShop.ComicLiveBackdrop>();
        titleButtons = new GameObject("Title buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        titleButtons.SetParent(titleArt, false); Stretch(titleButtons);
        CoverButton("btn_play", 1158, 450, 497, 142, () => { page = Page.Session; Build(); });
        CoverButton("btn_settings", 1166, 586, 412, 98, OpenSettings);
        CoverButton("btn_credits", 1166, 686, 412, 96, () => { page = Page.Credits; Build(); });
        CoverButton("btn_quit", 1166, 782, 412, 98, () => { page = Page.Leave; Build(); });
    }
    void CoverButton(string asset, float x, float y, float w, float h, Action action)
    {
        var go = new GameObject(asset, typeof(RectTransform), typeof(RawImage), typeof(Button));
        go.transform.SetParent(titleButtons, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(x / 1672f, 1f - (y + h) / 941f);
        rt.anchorMax = new Vector2((x + w) / 1672f, 1f - y / 941f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var image = go.GetComponent<RawImage>(); image.texture = Resources.Load<Texture2D>("ComicShopMenu/" + asset);
        var button = go.GetComponent<Button>(); button.targetGraphic = image; button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => { ShopAudio.Play(ShopCue.Click, Vector3.zero, false); action(); });
        go.AddComponent<ComicShopMenuFeedback>();
    }
    void BuildCredits()
    {
        Title("COMIC SHOP", "Tidy Up Together", Loc.T("credits.motto"));
        Label(content, "COMIC SHOP: TIDY UP TOGETHER\n\n" + Loc.T("credits.music") + "\nSunday Morning Vinyl", 0, 245, 708, 240, 28, Ink);
        Button(content, Loc.T("credits.main_menu"), 0, 555, 708, 56, () => { page = Page.Title; Build(); }, true);
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
        button.transition = Selectable.Transition.None;
        panel.gameObject.AddComponent<ComicShopMenuFeedback>();
        var edge = panel.gameObject.AddComponent<Outline>(); edge.effectColor = primary ? Ink : Accent; edge.effectDistance = new Vector2(2, -2);
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
