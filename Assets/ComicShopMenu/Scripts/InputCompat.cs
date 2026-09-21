using UnityEngine;

namespace ComicShop
{
    /// <summary>
    /// Eski/yeni Input System farkini kendisi cozer. Hangisi calisiyorsa onu kullanir,
    /// ikisi de cevap vermezse son bilinen konumu dondurur (imlec kose'ye yapismaz).
    /// </summary>
    public static class InputCompat
    {
        const int UNKNOWN = 0, LEGACY = 1, MODERN = 2;
        static int _mode = UNKNOWN;
        static Vector2 _last = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        public static Vector2 MousePosition()
        {
            if (_mode != MODERN)
            {
                try
                {
                    Vector2 p = Input.mousePosition;
                    if (p != Vector2.zero || _mode == LEGACY)
                    {
                        _mode = LEGACY; _last = p; return p;
                    }
                }
                catch { _mode = MODERN; }   // eski input kapali
            }

#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null)
            {
                _mode = MODERN;
                _last = m.position.ReadValue();
                return _last;
            }
            var p2 = UnityEngine.InputSystem.Pointer.current;
            if (p2 != null)
            {
                _mode = MODERN;
                _last = p2.position.ReadValue();
                return _last;
            }
#endif
            return _last;
        }

        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            var kb=UnityEngine.InputSystem.Keyboard.current;
            return kb!=null && kb.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public static bool MouseDown()
        {
            if (_mode != MODERN)
            {
                try { return Input.GetMouseButton(0); }
                catch { _mode = MODERN; }
            }
#if ENABLE_INPUT_SYSTEM
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null) return m.leftButton.isPressed;
            var p = UnityEngine.InputSystem.Pointer.current;
            if (p != null) return p.press.isPressed;
#endif
            return false;
        }
    }
}
