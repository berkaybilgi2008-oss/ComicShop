using UnityEngine;

namespace ComicShop
{
    /// <summary>
    /// Menu arka plan muzigi. Sessizden acilir, dongude calar,
    /// Ayarlar'daki muzik kaydiriciya baglidir.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MenuMusic : MonoBehaviour
    {
        [Header("Parca")]
        [Tooltip("Muzik dosyasini buraya surukle (.ogg / .wav / .mp3)")]
        public AudioClip track;

        [Header("Ses")]
        [Range(0f, 1f)] public float volume = 0.55f;
        [Tooltip("Sessizden acilma suresi (saniye)")]
        public float fadeInDuration = 1.5f;

        [Header("Davranis")]
        public bool playOnStart = true;
        [Tooltip("Oyun sahnesine gecerken muzik devam etsin mi")]
        public bool keepPlayingOnSceneChange = false;

        AudioSource _src;
        float _userVolume = 1f;
        float _fadeTimer;
        bool fadingOut;

        void Awake()
        {
            _src = GetComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.spatialBlend = 0f;
            _userVolume = PlayerPrefs.GetFloat("cs_music_volume", 0.8f);

            if (keepPlayingOnSceneChange)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        void Start()
        {
            if (playOnStart) PlayTrack();
        }

        public void PlayTrack()
        {
            if (track == null)
            {
                Debug.Log("[ComicShop] Muzik dosyasi atanmamis. " +
                          "MainMenuCanvas > Music > Menu Music > Track alanina surukle.");
                return;
            }
            fadingOut=false;
            StopAllCoroutines();
            _src.clip = track;
            _src.volume = fadeInDuration<=0 ? volume*_userVolume : 0f;
            _fadeTimer = 0f;
            _src.Play();
        }

        public void SetUserVolume(float v)
        {
            _userVolume = Mathf.Clamp01(v);
            if (_fadeTimer >= fadeInDuration) Apply();
        }

        void Apply() { if (_src != null) _src.volume = volume * _userVolume; }

        void Update()
        {
            if (_src == null || !_src.isPlaying || fadingOut) return;

            if (_fadeTimer < fadeInDuration)
            {
                _fadeTimer += Time.unscaledDeltaTime;
                float k = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(_fadeTimer / fadeInDuration);
                _src.volume = volume * _userVolume * k;
            }
        }

        /// <summary>Sahne degisiminde muzigi yumusakca sustur.</summary>
        public void FadeOut(float duration = 0.5f)
        {
            if (_src != null && _src.isPlaying) { fadingOut=true; StopAllCoroutines(); StartCoroutine(FadeRoutine(duration)); }
        }

        System.Collections.IEnumerator FadeRoutine(float d)
        {
            float start = _src.volume, t = 0f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                _src.volume = Mathf.Lerp(start, 0f, t / d);
                yield return null;
            }
            _src.Stop();
        }
    }
}
