using UnityEngine;
using UnityEngine.UI;

namespace ComicShop
{
    /// <summary>
    /// Menuye ozel comic imleci. Konumu once EventSystem'den (CursorProbe),
    /// o susarsa dogrudan input'tan alir. Ikisi de yoksa kendini gizler ve
    /// sistem imlecini geri acar — asla kosede takili kalmaz.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class ComicCursor : MonoBehaviour
    {
        [Header("Gorunum")]
        public Image image;
        public Color normalColor = Color.white;
        public Color hoverColor = new Color(1f, 0.72f, 0.18f, 1f);
        public float hoverScale = 1.22f;
        public float clickScale = 0.86f;
        public float hoverTilt = -12f;
        public float animSpeed = 18f;

        [Header("Davranis")]
        [Tooltip("Kapatirsan normal sistem imleci kullanilir")]
        public bool useCustomCursor = true;
        public bool hideSystemCursor = true;

        RectTransform _rt;
        Canvas _canvas;
        Vector3 _baseScale;
        Vector2 _pos;
        bool _hasPos;
        float _startTime;

        void Awake()
        {
            _rt = (RectTransform)transform;
            _baseScale = _rt.localScale;
            _canvas = GetComponentInParent<Canvas>();
            if (image == null) image = GetComponent<Image>();
            if (image != null) image.raycastTarget = false;
            _startTime = Time.unscaledTime;
            _pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        void OnEnable() { Cursor.lockState = CursorLockMode.None; }
        void OnDisable() { Cursor.visible = true; }

        bool TryGetPointer(out Vector2 sp)
        {
            if (CursorProbe.IsFresh) { sp = CursorProbe.Position; return true; }

            sp = InputCompat.MousePosition();
            // Sifir veya ekran disi deger = gecersiz, eski konumu koru
            if (sp.x <= 0f && sp.y <= 0f) { sp = _pos; return _hasPos; }
            return true;
        }

        void LateUpdate()
        {
            if (image == null) return;

            if (!useCustomCursor)
            {
                if (image.enabled) image.enabled = false;
                Cursor.visible = true;
                return;
            }

            bool ok = TryGetPointer(out Vector2 sp);

            // 2 saniye boyunca konum alinamadiysa vazgec, sistem imlecini geri ver
            if (!ok)
            {
                if (Time.unscaledTime - _startTime > 2f)
                {
                    image.enabled = false;
                    Cursor.visible = true;
                }
                return;
            }

            _pos = sp;
            _hasPos = true;

            bool inside = sp.x >= 0f && sp.y >= 0f && sp.x <= Screen.width && sp.y <= Screen.height;
            if (image.enabled != inside) image.enabled = inside;
            Cursor.visible = !(hideSystemCursor && inside);
            if (!inside) return;

            // Overlay canvas'ta dunya koordinati = ekran pikseli
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rt.position = new Vector3(sp.x, sp.y, 0f);
            }
            else
            {
                var cam = _canvas != null ? _canvas.worldCamera : null;
                var parent = transform.parent as RectTransform;
                if (parent != null &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, cam, out var lp))
                    _rt.localPosition = lp;
            }

            bool hovering = ComicMenuButton.Hovered != null;
            bool pressed = InputCompat.MouseDown();

            float target = hovering ? hoverScale : 1f;
            if (pressed) target *= clickScale;

            float k = 1f - Mathf.Exp(-animSpeed * Time.unscaledDeltaTime);
            _rt.localScale = Vector3.Lerp(_rt.localScale, _baseScale * target, k);

            float z = Mathf.LerpAngle(_rt.localEulerAngles.z, hovering ? hoverTilt : 0f, k);
            _rt.localEulerAngles = new Vector3(0f, 0f, z);

            image.color = Color.Lerp(image.color, hovering ? hoverColor : normalColor, k);
        }
    }
}
