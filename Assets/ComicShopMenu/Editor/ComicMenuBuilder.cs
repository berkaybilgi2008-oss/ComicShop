using System;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ComicShop;

namespace ComicShop.EditorTools
{
    /// <summary>
    /// Tools > Comic Shop > Ana Menuyu Olustur
    /// Sprite'lari import eder, Canvas'i kurar, butonlari tam yerlerine koyar,
    /// hover scriptlerini ekler ve tum click event'lerini baglar.
    /// </summary>
    public static class ComicMenuBuilder
    {
        const string SPRITES = "Assets/ComicShopMenu/Sprites/";

        // Orijinal gorseldeki piksel koordinatlari (1672 x 941)
        const float IMG_W = 1672f;
        const float IMG_H = 941f;

        struct BtnDef
        {
            public string name, sprite, label;
            public Rect px;                 // x0, y0, x1, y1 (sol-ust orijin)
            public bool pulse;
            public BtnDef(string n, string s, float x0, float y0, float x1, float y1, bool p)
            { name = n; sprite = s; label = n; px = new Rect(x0, y0, x1, y1); pulse = p; }
        }

        static readonly BtnDef[] Buttons =
        {
            new BtnDef("PlayButton",     "btn_play",     1158, 450, 1655, 592, true),
            new BtnDef("SettingsButton", "btn_settings", 1166, 586, 1578, 684, false),
            new BtnDef("CreditsButton",  "btn_credits",  1166, 686, 1578, 782, false),
            new BtnDef("QuitButton",     "btn_quit",     1166, 782, 1578, 880, false),
        };

        [MenuItem("Tools/Comic Shop/Ayri Menu Sahnesi Olustur (onerilen)", false, 0)]
        public static void BuildInNewScene()
        {
            var cur = EditorSceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(cur.path) || cur.name=="MainMenu") { Debug.LogError("[ComicShop] Once kayitli OYUN sahnesini acin."); return; }
            string gameScene = cur.name;
            string gamePath = cur.path;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            const string existingMenuPath="Assets/ComicShopMenu/Scenes/MainMenu.unity";
            if(System.IO.File.Exists(existingMenuPath)) {
                System.IO.Directory.CreateDirectory("Assets/ComicShopMenu/Backups");
                AssetDatabase.CopyAsset(existingMenuPath,"Assets/ComicShopMenu/Backups/MainMenu-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildInternal(false);

#if UNITY_2023_1_OR_NEWER
            var ctrl = UnityEngine.Object.FindFirstObjectByType<MainMenuController>();
#else
            var ctrl = UnityEngine.Object.FindObjectOfType<MainMenuController>();
#endif
            if (ctrl != null && !string.IsNullOrEmpty(gameScene)) ctrl.gameSceneName = gameScene;

            System.IO.Directory.CreateDirectory("Assets/ComicShopMenu/Scenes");
            const string menuPath = "Assets/ComicShopMenu/Scenes/MainMenu.unity";
            EditorSceneManager.SaveScene(scene, menuPath);
            RegisterBuildScenes(menuPath, gamePath);

            EditorUtility.DisplayDialog("Comic Shop",
                "Menu kendi sahnesine kuruldu:\n" + menuPath + "\n\n" +
                "- Build Settings'te ilk sahne olarak ayarlandi\n" +
                (string.IsNullOrEmpty(gameScene) ? "" : "- PLAY butonu '" + gameScene + "' sahnesini acacak\n") +
                "\nArtik oyun scriptlerin menuye karismaz, imlec kilitlenmez.", "Tamam");
        }

        static void RegisterBuildScenes(string menuPath, string gamePath)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            list.Add(new EditorBuildSettingsScene(menuPath, true));
            foreach (var sc in EditorBuildSettings.scenes)
                if (sc.path != menuPath) list.Add(new EditorBuildSettingsScene(sc.path, sc.path==gamePath || sc.enabled));
            if (!string.IsNullOrEmpty(gamePath) && !list.Exists(x => x.path == gamePath))
                list.Add(new EditorBuildSettingsScene(gamePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("Tools/Comic Shop/Ana Menuyu Olustur (bu sahneye)", false, 1)]
        public static void Build() => BuildInternal(true);

        static void BuildInternal(bool showDialog)
        {
            if (!ImportSprites()) return;

            var bgSprite = Load("menu_background");
            if (bgSprite == null) return;

            EnsureEventSystem();
            EnsureCamera();

            // ---------- Canvas ----------
            var canvasGO = new GameObject("MainMenuCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AudioSource));
            Undo.RegisterCreatedObjectUndo(canvasGO, "Comic Menu");

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(IMG_W, IMG_H);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var sfxSource = canvasGO.GetComponent<AudioSource>();
            sfxSource.playOnAwake = false;

            canvasGO.AddComponent<MenuBootstrap>();   // imlec + EventSystem guvencesi
            canvasGO.AddComponent<MenuGuard>();       // oyun scriptleri imleci kilitlemesin

            // ---------- Letterbox zemin ----------
            var backdrop = NewUI("Backdrop", canvasGO.transform);
            var backImg = backdrop.AddComponent<Image>();
            backImg.color = new Color(0.055f, 0.035f, 0.05f, 1f);
            backImg.raycastTarget = false;
            Stretch(backdrop.GetComponent<RectTransform>());

            // ---------- Arka plan ----------
            var bgGO = NewUI("Background", canvasGO.transform);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = bgSprite;
            bgGO.AddComponent<ComicLiveBackdrop>();
            bgImg.raycastTarget = true;          // imlec konumu icin olay yakalar
            bgGO.AddComponent<CursorProbe>();
            var bgRt = bgGO.GetComponent<RectTransform>();
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(IMG_W, IMG_H);
            var fitter = bgGO.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;   // tasmasin, hicbir buton disarda kalmasin
            fitter.aspectRatio = IMG_W / IMG_H;

            // ---------- Butonlar ----------
            var controllerGO = new GameObject("MenuController");
            Undo.RegisterCreatedObjectUndo(controllerGO, "Comic Menu");
            controllerGO.transform.SetParent(canvasGO.transform, false);
            var controller = controllerGO.AddComponent<MainMenuController>();

            var buttonsRoot = NewUI("Buttons", bgGO.transform);
            Stretch(buttonsRoot.GetComponent<RectTransform>());

            Button playBtn = null;
            foreach (var def in Buttons)
            {
                var sp = Load(def.sprite);
                if (sp == null) continue;

                var go = NewUI(def.name, buttonsRoot.transform);
                var img = go.AddComponent<Image>();
                img.sprite = sp;
                img.raycastTarget = true;

                var rt = go.GetComponent<RectTransform>();
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = new Vector2(def.px.x / IMG_W, 1f - def.px.height / IMG_H);
                rt.anchorMax = new Vector2(def.px.width / IMG_W, 1f - def.px.y / IMG_H);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var btn = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;   // gorsel isi ComicMenuButton'da
                btn.targetGraphic = img;

                var hover = go.AddComponent<ComicMenuButton>();
                hover.fallbackPointer = false;
                hover.idlePulse = def.pulse;
                hover.audioSource = sfxSource;
                if (def.pulse) { hover.hoverScale = 1.09f; hover.hoverOffset = new Vector2(-22f, 0f); }

                switch (def.name)
                {
                    case "PlayButton":
                        playBtn = btn;
                        UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.PlayGame); break;
                    case "SettingsButton":
                        UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.OpenSettings); break;
                    case "CreditsButton":
                        UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.OpenCredits); break;
                    case "QuitButton":
                        UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.QuitGame); break;
                }
            }

            // ---------- Paneller ----------
            var settingsPanel = BuildSettingsPanel(canvasGO.transform, controller);
            var creditsPanel = BuildCreditsPanel(canvasGO.transform, controller);

            // ---------- Fade katmani ----------
            var fadeGO = NewUI("FadeOverlay", canvasGO.transform);
            var fadeImg = fadeGO.AddComponent<Image>();
            fadeImg.color = Color.black;
            fadeImg.raycastTarget = false;
            Stretch(fadeGO.GetComponent<RectTransform>());
            var fadeCg = fadeGO.AddComponent<CanvasGroup>();
            fadeCg.alpha = 0f;          // editorde ekrani kapatmasin, Play'de Awake 1 yapar
            fadeCg.blocksRaycasts = false;

            // ---------- Muzik ----------
            var musicGO = new GameObject("Music", typeof(AudioSource));
            Undo.RegisterCreatedObjectUndo(musicGO, "Comic Menu");
            musicGO.transform.SetParent(canvasGO.transform, false);
            var msrc = musicGO.GetComponent<AudioSource>();
            msrc.playOnAwake = false;
            msrc.loop = true;
            var menuMusic = musicGO.AddComponent<MenuMusic>();
            System.IO.Directory.CreateDirectory("Assets/ComicShopMenu/Audio");
            AssetDatabase.Refresh();
            menuMusic.track=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/ComicShopMenu/Audio/Sunday_Morning_Vinyl.mp3");

            // ---------- Ozel imlec ----------
            BuildCursor(canvasGO.transform);

            // ---------- Baglantilar ----------
            controller.fadeOverlay = fadeCg;
            controller.settingsPanel = settingsPanel;
            controller.creditsPanel = creditsPanel;
            controller.buttonsRoot = buttonsRoot;
            controller.music = menuMusic;
            controller.firstSelected = playBtn ? playBtn.gameObject : null;
            controller.musicSlider = settingsPanel.transform.Find("Board/MusicSlider")?.GetComponent<Slider>();
            controller.sfxSlider = settingsPanel.transform.Find("Board/SfxSlider")?.GetComponent<Slider>();
            controller.fullscreenToggle = settingsPanel.transform.Find("Board/FullscreenToggle")?.GetComponent<Toggle>();

            settingsPanel.SetActive(false);
            creditsPanel.SetActive(false);

            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            if (!showDialog) return;
            EditorUtility.DisplayDialog("Comic Shop",
                "Ana menu olusturuldu!\n\n" +
                "1) Sahneyi kaydet (Ctrl+S)\n" +
                "2) MenuController > Game Scene Name alanina oyun sahnenin adini yaz\n" +
                "3) Play'e bas ve butonlarin uzerine gel.", "Tamam");
        }


        [MenuItem("Tools/Comic Shop/Menuyu Onar (mouse + hover)", false, 20)]
        public static void Repair()
        {
            ImportSprites();

            EnsureCamera();

            // 0) Simsiyah ekran = fade katmani acik kalmis
#if UNITY_2023_1_OR_NEWER
            var groups = UnityEngine.Object.FindObjectsByType<CanvasGroup>(FindObjectsSortMode.None);
#else
            var groups = UnityEngine.Object.FindObjectsOfType<CanvasGroup>();
#endif
            foreach (var g in groups)
                if (g.name == "FadeOverlay") { g.alpha = 0f; g.blocksRaycasts = false; EditorUtility.SetDirty(g); }

            // 1) EventSystem'i dogru input modulu ile kur
            EnsureEventSystem();
#if UNITY_2023_1_OR_NEWER
            var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            var es = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            var ism = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (es != null && ism != null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
                var m = es.GetComponent(ism);
                if (m == null) m = es.gameObject.AddComponent(ism);
                MenuBootstrap.AssignDefaultActions(m);
            }

            // 2) Canvas kontrolleri
#if UNITY_2023_1_OR_NEWER
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
#else
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#endif
            foreach (var cv in canvases)
            {
                if (cv.name != "MainMenuCanvas") continue;
                if (cv.GetComponent<GraphicRaycaster>() == null) cv.gameObject.AddComponent<GraphicRaycaster>();
                if (cv.GetComponent<MenuBootstrap>() == null) cv.gameObject.AddComponent<MenuBootstrap>();
                if (cv.GetComponent<MenuGuard>() == null) cv.gameObject.AddComponent<MenuGuard>();

                var fit = cv.GetComponentInChildren<AspectRatioFitter>(true);
                if (fit != null) fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;

                foreach (var b in cv.GetComponentsInChildren<Image>(true))
                    b.alphaHitTestMinimumThreshold = 0f;      // okunamayan texture raycast'i bozar

                foreach (var b in cv.GetComponentsInChildren<Button>(true))
                {
                    b.interactable = true;
                    b.transition = Selectable.Transition.None;
                    if (b.GetComponent<ComicMenuButton>() == null)
                        b.gameObject.AddComponent<ComicMenuButton>();
                }
                if (cv.GetComponentInChildren<MenuMusic>(true) == null)
                {
                    var mgo = new GameObject("Music", typeof(AudioSource));
                    mgo.transform.SetParent(cv.transform, false);
                    var ms = mgo.GetComponent<AudioSource>();
                    ms.playOnAwake = false; ms.loop = true;
                    var mm = mgo.AddComponent<MenuMusic>();
                    var mc = cv.GetComponentInChildren<MainMenuController>(true);
                    if (mc != null) mc.music = mm;
                    System.IO.Directory.CreateDirectory("Assets/ComicShopMenu/Audio");
                }

                var bgT = cv.transform.Find("Background");
                if (bgT != null)
                {
                    var bgI = bgT.GetComponent<Image>();
                    if (bgI != null) bgI.raycastTarget = true;
                    if (bgT.GetComponent<CursorProbe>() == null) bgT.gameObject.AddComponent<CursorProbe>();
                }
                if (cv.GetComponentInChildren<ComicCursor>(true) == null) BuildCursor(cv.transform);
                var cc = cv.GetComponentInChildren<ComicCursor>(true);
                if (cc != null) cc.transform.SetAsLastSibling();
                EditorUtility.SetDirty(cv.gameObject);
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog("Comic Shop",
                "Onarim tamam.\n\n- Mouse imleci zorla acildi\n- EventSystem dogru modulle kuruldu\n" +
                "- Arka plan tasmayacak sekilde ayarlandi\n- Butonlarin raycast sorunu giderildi\n\n" +
                "Play'e basip test et.", "Tamam");
        }

        [MenuItem("Tools/Comic Shop/Ozel Imleci Ac-Kapa", false, 21)]
        public static void ToggleCursor()
        {
#if UNITY_2023_1_OR_NEWER
            var cur = UnityEngine.Object.FindFirstObjectByType<ComicCursor>(FindObjectsInactive.Include);
#else
            var cur = UnityEngine.Object.FindObjectOfType<ComicCursor>(true);
#endif
            if (cur == null) { Debug.LogWarning("[ComicShop] Sahnede ComicCursor yok."); return; }
            cur.useCustomCursor = !cur.useCustomCursor;
            EditorUtility.SetDirty(cur);
            EditorSceneManager.MarkAllScenesDirty();
            Debug.Log("[ComicShop] Ozel imlec: " + (cur.useCustomCursor ? "ACIK" : "KAPALI (sistem imleci)"));
        }

        [MenuItem("Tools/Comic Shop/Bu Sahnedeki Menuyu Temizle", false, 22)]
        public static void CleanMenuFromScene()
        {
            int removed = 0;
#if UNITY_2023_1_OR_NEWER
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var canvases = Resources.FindObjectsOfTypeAll<Canvas>();
#endif
            foreach (var cv in canvases)
            {
                if (cv == null || cv.gameObject.scene.IsValid() == false) continue;
                bool isMenu = cv.name == "MainMenuCanvas"
                              || cv.GetComponentInChildren<MainMenuController>(true) != null
                              || cv.GetComponent<MenuBootstrap>() != null;
                if (!isMenu) continue;
                Undo.DestroyObjectImmediate(cv.gameObject);
                removed++;
            }

            EditorSceneManager.MarkAllScenesDirty();
            EditorUtility.DisplayDialog("Comic Shop",
                removed > 0
                    ? removed + " adet menu objesi bu sahneden silindi.\n\nSahneyi kaydet (Ctrl+S)."
                    : "Bu sahnede menu objesi bulunamadi.", "Tamam");
        }

        // ================= Paneller =================

        static GameObject BuildSettingsPanel(Transform parent, MainMenuController c)
            => BuildThemedPanel(parent, "SettingsPanel", ComicThemedPanel.Page.Settings, c);
        static GameObject BuildCreditsPanel(Transform parent, MainMenuController c)
            => BuildThemedPanel(parent, "CreditsPanel", ComicThemedPanel.Page.Credits, c);
        static GameObject BuildThemedPanel(Transform parent, string name, ComicThemedPanel.Page page, MainMenuController c)
        {
            var go=NewUI(name,parent); Stretch(go.GetComponent<RectTransform>());
            var panel=go.AddComponent<ComicThemedPanel>(); panel.page=page;panel.menu=c;
            return go;
        }

        [MenuItem("Tools/Comic Shop/Oyun Icine Temali Pause Ekle", false, 3)]
        public static void BuildPause()
        {
            if(UnityEngine.Object.FindFirstObjectByType<ComicPauseMenu>() != null) {
                Debug.LogWarning("[ComicShop] Pause zaten var; mevcut objeyi kullanin.");return;
            }
            EnsureEventSystem();
            var root=NewUI("ComicPauseUI",null);
            var canvas=root.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=200;
            var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1672,941);scaler.matchWidthOrHeight=.5f;
            root.AddComponent<GraphicRaycaster>();root.AddComponent<AudioSource>().playOnAwake=false;
            var pause=root.AddComponent<ComicPauseMenu>();root.AddComponent<ComicGameplaySettings>();
            var pausePanel=BuildThemedPanel(root.transform,"PausePanel",ComicThemedPanel.Page.Pause,null);
            var settings=BuildThemedPanel(root.transform,"SettingsPanel",ComicThemedPanel.Page.Settings,null);
            pausePanel.GetComponent<ComicThemedPanel>().pause=pause;settings.GetComponent<ComicThemedPanel>().pause=pause;
            pause.pausePanel=pausePanel;pause.settingsPanel=settings;pausePanel.SetActive(false);settings.SetActive(false);
            Selection.activeGameObject=root;EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("[ComicShop] Temali pause eklendi. Local Input Scripts alanina yerel oyuncu kontrol/etkilesim scriptlerini, ComicGameplaySettings alanina kamera ve hassasiyet baglantisini atayin. Eski pause/ShopFrontEnd ayni anda aktif olmamali. Multiplayer'da Pause World Time kapali kalmali.");
        }

        static void BuildCursor(Transform canvas)
        {
            var sp = Load("cursor_comic");
            if (sp == null) return;

            var go = NewUI("ComicCursor", canvas);
            var img = go.AddComponent<Image>();
            img.sprite = sp;
            img.raycastTarget = false;
            img.preserveAspect = true;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.06f, 0.94f);      // ok ucu tam imlec noktasinda
            rt.sizeDelta = new Vector2(46f, 58f);

            var cur = go.AddComponent<ComicCursor>();
            cur.image = img;
            go.transform.SetAsLastSibling();           // her seyin ustunde
        }

        // ================= Yardimcilar =================

        static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Comic Menu");
            return go;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text MakeLabel(Transform parent, string name, string content,
                              Vector2 pos, int size, TextAnchor anchor)
        {
            var go = NewUI(name, parent);
            var t = go.AddComponent<Text>();
            t.text = content;
            t.font = GetFont();
            t.fontSize = size;
            t.alignment = anchor;
            t.color = new Color(0.10f, 0.08f, 0.07f);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460f, 44f);
            return t;
        }

        static Slider MakeSlider(Transform parent, string name, Vector2 pos)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460f, 26f);

            var bg = NewUI("Background", go.transform);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = BuiltIn("UI/Skin/Background.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.25f, 0.20f, 0.18f);
            Stretch(bg.GetComponent<RectTransform>());

            var fillArea = NewUI("Fill Area", go.transform);
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = new Vector2(0f, 0.25f);
            faRt.anchorMax = new Vector2(1f, 0.75f);
            faRt.offsetMin = new Vector2(8f, 0f);
            faRt.offsetMax = new Vector2(-8f, 0f);

            var fill = NewUI("Fill", fillArea.transform);
            var fImg = fill.AddComponent<Image>();
            fImg.sprite = BuiltIn("UI/Skin/UISprite.psd");
            fImg.type = Image.Type.Sliced;
            fImg.color = new Color(0.98f, 0.68f, 0.13f);
            var fRt = fill.GetComponent<RectTransform>();
            fRt.anchorMin = Vector2.zero; fRt.anchorMax = new Vector2(0f, 1f);
            fRt.sizeDelta = new Vector2(12f, 0f);

            var handleArea = NewUI("Handle Slide Area", go.transform);
            var haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
            haRt.offsetMin = new Vector2(10f, 0f); haRt.offsetMax = new Vector2(-10f, 0f);

            var handle = NewUI("Handle", handleArea.transform);
            var hImg = handle.AddComponent<Image>();
            hImg.sprite = BuiltIn("UI/Skin/Knob.psd");
            hImg.color = new Color(0.12f, 0.10f, 0.09f);
            var hRt = handle.GetComponent<RectTransform>();
            hRt.anchorMin = Vector2.zero; hRt.anchorMax = new Vector2(0f, 1f);
            hRt.sizeDelta = new Vector2(28f, 0f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fRt;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f; slider.maxValue = 1f; slider.value = 0.8f;
            return slider;
        }

        static Toggle MakeToggle(Transform parent, string name, string label, Vector2 pos)
        {
            var go = NewUI(name, parent);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460f, 40f);

            var bgGO = NewUI("Background", go.transform);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.sprite = BuiltIn("UI/Skin/UISprite.psd");
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(0.25f, 0.20f, 0.18f);
            var bRt = bgGO.GetComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0f, 0.5f); bRt.anchorMax = new Vector2(0f, 0.5f);
            bRt.pivot = new Vector2(0f, 0.5f);
            bRt.sizeDelta = new Vector2(34f, 34f);
            bRt.anchoredPosition = Vector2.zero;

            var checkGO = NewUI("Checkmark", bgGO.transform);
            var cImg = checkGO.AddComponent<Image>();
            cImg.sprite = BuiltIn("UI/Skin/Checkmark.psd");
            cImg.color = new Color(0.98f, 0.68f, 0.13f);
            Stretch(checkGO.GetComponent<RectTransform>());

            var lbl = MakeLabel(go.transform, "Label", label, Vector2.zero, 28, TextAnchor.MiddleLeft);
            var lRt = lbl.GetComponent<RectTransform>();
            lRt.anchorMin = lRt.anchorMax = new Vector2(0f, 0.5f);
            lRt.pivot = new Vector2(0f, 0.5f);
            lRt.anchoredPosition = new Vector2(48f, 0f);
            lRt.sizeDelta = new Vector2(380f, 40f);

            var toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = cImg;
            toggle.isOn = Screen.fullScreen;
            return toggle;
        }

        static Font _font;
        static Font GetFont()
        {
            if (_font != null) return _font;
            try { _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (_font == null) { try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
            return _font;
        }

        static Sprite BuiltIn(string path) =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        static Sprite Load(string file)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + file + ".png");
            if (sp == null)
                Debug.LogError($"[ComicShop] Sprite bulunamadi: {SPRITES}{file}.png");
            return sp;
        }

        static bool ImportSprites()
        {
            string[] files = { "menu_background", "btn_play", "btn_settings", "btn_credits", "btn_quit", "cursor_comic" };
            bool ok = true;
            foreach (var f in files)
            {
                string path = SPRITES + f + ".png";
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null)
                {
                    Debug.LogError($"[ComicShop] Dosya yok: {path}");
                    ok = false; continue;
                }
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 2048;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.isReadable = false;
                imp.npotScale = TextureImporterNPOTScale.None;   // netlik icin: yeniden boyutlandirma yok
                imp.SaveAndReimport();
            }
            return ok;
        }

        static void EnsureCamera()
        {
#if UNITY_2023_1_OR_NEWER
            if (UnityEngine.Object.FindFirstObjectByType<Camera>() != null) return;
#else
            if (UnityEngine.Object.FindObjectOfType<Camera>() != null) return;
#endif
            var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            var cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.055f, 0.035f, 0.05f, 1f);
            cam.orthographic = true;
            camGO.transform.position = new Vector3(0f, 0f, -10f);
            Undo.RegisterCreatedObjectUndo(camGO, "Comic Menu");
        }

        static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
#else
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
#endif

            var es = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(es, "Comic Menu");

            // Yeni Input System varsa onu kullan, yoksa klasik modul
            var t = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) MenuBootstrap.AssignDefaultActions(es.AddComponent(t));
            else es.AddComponent<StandaloneInputModule>();
        }
    }
}
