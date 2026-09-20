using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ComicShop
{
    /// <summary>
    /// Comic tarzi menu butonu. Mouse uzerine gelince buyur, yana kayar, egilir.
    /// EventSystem calismiyorsa kendi pointer kontrolune gecer (yedek sistem).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class ComicMenuButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler,
        ISelectHandler, IDeselectHandler
    {
        [Header("Hover ayarlari")]
        public float hoverScale = 1.07f;
        public float pressScale = 0.95f;
        public float hoverTilt = -1.4f;
        public Vector2 hoverOffset = new Vector2(-18f, 0f);
        public float animSpeed = 14f;

        [Header("Renk")]
        public Color normalColor = Color.white;
        public Color hoverColor = new Color(1f, 0.96f, 0.86f, 1f);

        [Header("Bosta nabiz efekti")]
        public bool idlePulse = false;
        public float pulseAmount = 0.012f;
        public float pulseSpeed = 2.2f;

        [Header("Tiklama darbesi")]
        public float clickPunch = 0.10f;
        public float punchDuration = 0.22f;

        [Header("Ses (opsiyonel)")]
        public AudioClip hoverClip;
        public AudioClip clickClip;
        [Range(0f, 1f)] public float sfxVolume = 0.7f;
        public AudioSource audioSource;

        [Header("Etkilesim")]
        public bool interactable = true;
        [Tooltip("EventSystem cevap vermezse kendi pointer kontroluyle calis")]
        public bool fallbackPointer = true;

        /// <summary>Su an uzerinde olunan buton (ozel imlec bunu okur).</summary>
        public static ComicMenuButton Hovered;
        /// <summary>EventSystem'den gercek pointer olayi geldi mi?</summary>
        public static bool EventsAlive;
        /// <summary>Ayarlar'daki efekt kaydiricisi bunu ayarlar.</summary>
        public static float GlobalSfxVolume = 1f;

        RectTransform _rt;
        Graphic _graphic;
        Canvas _canvas;
        Button _button;
        Vector2 _basePos;
        Vector3 _baseScale;
        Vector3 _baseEuler;

        bool _hover, _pressed, _selected, _prevMouseDown;
        float _punchTimer = -1f;
        float _bornTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Hovered=null; EventsAlive=false; GlobalSfxVolume=1f; }

        void Awake()
        {
            if(!hoverClip) hoverClip=Resources.Load<AudioClip>("ComicMenu/Hover");
            if(!clickClip) clickClip=Resources.Load<AudioClip>("ComicMenu/Click");
            _rt = (RectTransform)transform;
            _graphic = GetComponent<Graphic>();
            _button = GetComponent<Button>();
            _canvas = GetComponentInParent<Canvas>();
            _basePos = _rt.anchoredPosition;
            _baseScale = _rt.localScale;
            _baseEuler = _rt.localEulerAngles;
            _bornTime = Time.unscaledTime;

            if (audioSource == null) audioSource = GetComponentInParent<AudioSource>();
            if (_graphic != null) _graphic.color = normalColor;
        }

        void OnDisable()
        {
            _hover = _pressed = _selected = false;
            if (Hovered == this) Hovered = null;
            if (_rt == null) return;
            _rt.anchoredPosition = _basePos;
            _rt.localScale = _baseScale;
            _rt.localEulerAngles = _baseEuler;
            if (_graphic != null) _graphic.color = normalColor;
        }

        void Update()
        {
            // EventSystem sessizse yedek pointer devreye girer
            if (fallbackPointer && !EventsAlive && Time.unscaledTime - _bornTime > 1f)
                ManualPointer();

            float dt = Time.unscaledDeltaTime;
            bool active = interactable && (_button == null || _button.IsInteractable()) && (_hover || _selected);

            float targetScale = active ? hoverScale : 1f;
            if (_pressed && interactable) targetScale = pressScale;

            if (idlePulse && !active && !_pressed)
                targetScale += Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount;

            if (_punchTimer >= 0f)
            {
                _punchTimer += dt;
                float t = _punchTimer / punchDuration;
                if (t >= 1f) _punchTimer = -1f;
                else targetScale += Mathf.Sin(t * Mathf.PI) * clickPunch * (1f - t);
            }

            Vector2 targetPos = _basePos + (active ? hoverOffset : Vector2.zero);
            float targetTilt = active ? hoverTilt : 0f;
            Color targetCol = active ? hoverColor : normalColor;

            float k = 1f - Mathf.Exp(-animSpeed * dt);
            _rt.localScale = Vector3.Lerp(_rt.localScale, _baseScale * targetScale, k);
            _rt.anchoredPosition = Vector2.Lerp(_rt.anchoredPosition, targetPos, k);

            float z = Mathf.LerpAngle(_rt.localEulerAngles.z, _baseEuler.z + targetTilt, k);
            _rt.localEulerAngles = new Vector3(_baseEuler.x, _baseEuler.y, z);

            if (_graphic != null)
                _graphic.color = Color.Lerp(_graphic.color, targetCol, k);
        }

        // ---------- Yedek pointer (EventSystem'siz calisir) ----------
        void ManualPointer()
        {
            if (!interactable || (_button != null && !_button.IsInteractable())) return;

            Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? _canvas.worldCamera : null;

            Vector2 sp = InputCompat.MousePosition();
            bool inside = RectTransformUtility.RectangleContainsScreenPoint(_rt, sp, cam);

            if (inside && !_hover)
            {
                _hover = true;
                Hovered = this;
                transform.SetAsLastSibling();
                Play(hoverClip);
            }
            else if (!inside && _hover)
            {
                _hover = false;
                if (Hovered == this) Hovered = null;
            }

            bool down = InputCompat.MouseDown();
            _pressed = inside && down;

            if (inside && down && !_prevMouseDown)
            {
                _punchTimer = 0f;
                Play(clickClip);
                if (_button != null && _button.interactable) _button.onClick.Invoke();
            }
            _prevMouseDown = down;
        }

        void Play(AudioClip clip)
        {
            if (clip == null) return;
            if (audioSource != null) audioSource.PlayOneShot(clip, sfxVolume * GlobalSfxVolume);
        }

        // ---------- EventSystem yolu ----------
        public void OnPointerEnter(PointerEventData e)
        {
            EventsAlive = true;
            if (!interactable || (_button != null && !_button.IsInteractable())) return;
            _hover = true;
            Hovered = this;
            transform.SetAsLastSibling();
            Play(hoverClip);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            _pressed = false;
            if (Hovered == this) Hovered = null;
        }

        public void OnPointerDown(PointerEventData e)
        {
            EventsAlive = true;
            if (!interactable || (_button != null && !_button.IsInteractable())) return;
            _pressed = true;
        }

        public void OnPointerUp(PointerEventData e) => _pressed = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!interactable || (_button != null && !_button.IsInteractable())) return;
            _punchTimer = 0f;
            Play(clickClip);
        }

        public void OnSelect(BaseEventData e)
        {
            if (!interactable || (_button != null && !_button.IsInteractable())) return;
            _selected = true;
            transform.SetAsLastSibling();
            Play(hoverClip);
        }

        public void OnDeselect(BaseEventData e) => _selected = false;
    }
}
