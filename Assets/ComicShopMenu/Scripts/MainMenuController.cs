using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

namespace ComicShop
{
    /// <summary>
    /// Ana menu mantigi. PLAY / SETTINGS / CREDITS / QUIT.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Sahne")]
        [Tooltip("PLAY'e basinca yuklenecek sahne adi. Build Settings'e ekli olmali!")]
        public string gameSceneName = "Game";

        [Header("Referanslar")]
        public CanvasGroup fadeOverlay;      // siyah gecis katmani
        public GameObject settingsPanel;
        public GameObject creditsPanel;
        public GameObject buttonsRoot;
        public MenuMusic music;

        [Header("Ayar kontrolleri (opsiyonel)")]
        public Slider musicSlider;
        public Slider sfxSlider;
        public Toggle fullscreenToggle;

        [Header("Gecis")]
        public float fadeInDuration = 0.6f;
        public float fadeOutDuration = 0.45f;

        [Header("Ilk secili buton (klavye/gamepad)")]
        public GameObject firstSelected;

        const string K_MUSIC = "cs_music_volume";
        const string K_SFX = "cs_sfx_volume";

        bool loading;

        void OnEnable() { ComicPreferences.Changed += RefreshAudio; }
        void OnDisable() { ComicPreferences.Changed -= RefreshAudio; SavePrefs(); }
        void RefreshAudio() { AudioListener.volume=ComicPreferences.Master; if(music) music.SetUserVolume(ComicPreferences.Music); ComicMenuButton.GlobalSfxVolume=ComicPreferences.Sfx; }

        void Awake()
        {
            // Play'de siyahtan acilis; editorde katman seffaf kalir
            if (fadeOverlay != null && fadeInDuration > 0f) fadeOverlay.alpha = 1f;
        }

        void Start()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
            if (creditsPanel) creditsPanel.SetActive(false);

            LoadPrefs();
            RefreshAudio();
            if(PlayerPrefs.HasKey("cs_fullscreen")) Screen.fullScreen=PlayerPrefs.GetInt("cs_fullscreen")==1;

            if (musicSlider) musicSlider.onValueChanged.AddListener(SetMusicVolume);
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(SetSfxVolume);
            if (fullscreenToggle) fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

            if (firstSelected && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(firstSelected);

            StartCoroutine(FadeIn());
        }

        void Update()
        {
            // ESC ile panelleri kapat
            if (WasEscapePressed() && (IsOpen(settingsPanel) || IsOpen(creditsPanel)))
                ClosePanels();
        }

        static bool IsOpen(GameObject go) => go != null && go.activeSelf;

        static bool WasEscapePressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        // ---------------- Butonlar ----------------

        public void PlayGame()
        {
            if(loading) return;
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogWarning("[ComicShop] gameSceneName bos. Inspector'dan sahne adini yaz.");
                return;
            }
            if (gameSceneName == SceneManager.GetActiveScene().name)
            {
                Debug.LogError("[ComicShop] 'Game Scene Name' menunun kendi sahnesiyle ayni: '" +
                               gameSceneName + "'. Buraya OYUN sahnenin adini yaz.");
                return;
            }
            if(!Application.CanStreamedLevelBeLoaded(gameSceneName)) { Debug.LogError("[ComicShop] Oyun sahnesini Build Profiles sahne listesine ekleyin: "+gameSceneName); return; }
            loading=true;
            SavePrefs();
            StartCoroutine(FadeAndLoad());
        }

        public void OpenSettings()
        {
            if (creditsPanel) creditsPanel.SetActive(false);
            if (buttonsRoot) buttonsRoot.SetActive(false);
            if (settingsPanel) settingsPanel.SetActive(true);
        }

        public void OpenCredits()
        {
            if (settingsPanel) settingsPanel.SetActive(false);
            if (buttonsRoot) buttonsRoot.SetActive(false);
            if (creditsPanel) creditsPanel.SetActive(true);
        }

        public void ClosePanels()
        {
            SavePrefs();
            if(buttonsRoot) buttonsRoot.SetActive(true);
            if (settingsPanel) settingsPanel.SetActive(false);
            if (creditsPanel) creditsPanel.SetActive(false);
            if (firstSelected && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(firstSelected);
        }

        public void QuitGame()
        {
            SavePrefs();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- Ayarlar ----------------

        public void SetMusicVolume(float v)
        {
            v = Mathf.Clamp01(v);
            if (music != null) music.SetUserVolume(v);
            else AudioListener.volume = ComicPreferences.Master;          // muzik objesi yoksa genel ses
            PlayerPrefs.SetFloat(K_MUSIC, v);
        }

        public void SetSfxVolume(float v)
        {
            v = Mathf.Clamp01(v);
            ComicMenuButton.GlobalSfxVolume = v;
            PlayerPrefs.SetFloat(K_SFX, v);
        }

        public void SetFullscreen(bool on) => Screen.fullScreen = on;

        void LoadPrefs()
        {
            float m = PlayerPrefs.GetFloat(K_MUSIC, 0.8f);
            float s = PlayerPrefs.GetFloat(K_SFX, 0.8f);
            if (music != null) music.SetUserVolume(m); else AudioListener.volume = ComicPreferences.Master;
            ComicMenuButton.GlobalSfxVolume = s;
            if (musicSlider) musicSlider.SetValueWithoutNotify(m);
            if (sfxSlider) sfxSlider.SetValueWithoutNotify(s);
            if (fullscreenToggle) fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
        }

        void SavePrefs() => PlayerPrefs.Save();

        // ---------------- Gecisler ----------------

        IEnumerator FadeIn()
        {
            if (fadeOverlay == null) yield break;
            if (fadeInDuration <= 0f)
            {
                fadeOverlay.alpha = 0f;
                fadeOverlay.blocksRaycasts = false;
                yield break;
            }
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = 1f - Mathf.Clamp01(t / fadeInDuration);
                yield return null;
            }
            fadeOverlay.alpha = 0f;
            fadeOverlay.blocksRaycasts = false;
        }

        IEnumerator FadeAndLoad()
        {
            if (music != null && !music.keepPlayingOnSceneChange) music.FadeOut(fadeOutDuration);

            if (fadeOverlay != null)
            {
                fadeOverlay.blocksRaycasts = true;
                float t = 0f;
                while (t < fadeOutDuration)
                {
                    t += Time.unscaledDeltaTime;
                    fadeOverlay.alpha = Mathf.Clamp01(t / fadeOutDuration);
                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                Time.timeScale=1f;
                Cursor.lockState=CursorLockMode.Locked; Cursor.visible=false;
                SceneManager.LoadScene(gameSceneName);
            }
            else
            {
                Debug.LogError($"[ComicShop] '{gameSceneName}' sahnesi bulunamadi. " +
                               "File > Build Settings icine sahneyi eklemeyi unutma.");
                if (fadeOverlay != null)
                {
                    fadeOverlay.alpha = 0f;
                    fadeOverlay.blocksRaycasts = false;
                }
            }
        }
    }
}
