using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace ComicShop
{
    /// <summary>
    /// Menu acilirken imleci gorunur yapar ve EventSystem'in dogru input modulu
    /// ile calistigindan emin olur. Hover calismiyorsa sebep %90 bu ikisidir.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class MenuBootstrap : MonoBehaviour
    {
        [Tooltip("Menude fare imlecini zorla gorunur yap")]
        public bool forceCursorVisible = true;

        void Awake()
        {
            ApplyCursor();
            EnsureRaycaster();
            EnsureEventSystem();
        }

        void OnEnable() => ApplyCursor();

        void ApplyCursor()
        {
            Cursor.lockState = CursorLockMode.None;
#if UNITY_2023_1_OR_NEWER
            var custom = Object.FindFirstObjectByType<ComicCursor>();
#else
            var custom = Object.FindObjectOfType<ComicCursor>();
#endif
            if (custom != null && custom.useCustomCursor && custom.hideSystemCursor) { Cursor.visible = false; return; }
            if (forceCursorVisible) Cursor.visible = true;
        }

        void EnsureRaycaster()
        {
            if (GetComponent<Canvas>() != null && GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// Kodla eklenen InputSystemUIInputModule bos action listesiyle gelir ve
        /// hicbir tiklama/hover isini gormez. Varsayilan action'lari zorla atar.
        /// </summary>
        public static void AssignDefaultActions(Component module)
        {
            if (module == null) return;
            try
            {
                var t = module.GetType();
                var prop = t.GetProperty("actionsAsset");
                if (prop != null && prop.GetValue(module) != null) return;
                var mi = t.GetMethod("AssignDefaultActions");
                if (mi != null) mi.Invoke(module, null);
            }
            catch { }
        }

        public static void EnsureEventSystem()
        {
#if UNITY_2023_1_OR_NEWER
            var es = Object.FindFirstObjectByType<EventSystem>();
#else
            var es = Object.FindObjectOfType<EventSystem>();
#endif
            if (es == null)
                es = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            // Yeni Input System aktif: eski modul pointer olaylarini isleyemez
            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Destroy(legacy);
            }
            var mod = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (mod == null) mod = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            AssignDefaultActions(mod);
#else
            if (es.GetComponent<BaseInputModule>() == null)
                es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
