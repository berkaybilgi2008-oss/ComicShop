using UnityEngine;

namespace ComicShop
{
    /// <summary>
    /// Menu acikken oyun mantiginin araya girmesini engeller.
    /// Player/FPS controller gibi scriptler imleci kilitler (CursorLockMode.Locked);
    /// bu bilesen her karede imleci serbest tutar ve gerekirse suclu scriptleri kapatir.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class MenuGuard : MonoBehaviour
    {
        [Header("Imleci serbest tut")]
        public bool enforceCursorUnlocked = true;

        [Header("Menu acikken kapatilacak objeler")]
        [Tooltip("Player, FPS kamera, oyun yoneticisi gibi objeleri buraya surukle")]
        public GameObject[] disableWhileInMenu;

        [Header("Zaman")]
        public bool forceNormalTimeScale = true;

        int _fixCount;
        bool _warned;

        void Awake()
        {
            if (forceNormalTimeScale) Time.timeScale = 1f;

            if (disableWhileInMenu != null)
                foreach (var go in disableWhileInMenu)
                    if (go != null) go.SetActive(false);

            Unlock();
        }

        void LateUpdate()
        {
            if (!enforceCursorUnlocked) return;

            if (Cursor.lockState != CursorLockMode.None)
            {
                Unlock();
                _fixCount++;

                if (_fixCount > 60 && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning(
                        "[ComicShop] Sahnede imleci surekli kilitleyen bir script var " +
                        "(genelde player / FPS kamera kontrolcusu). Menu kendi sahnesinde " +
                        "olmali: Tools > Comic Shop > Ayri Menu Sahnesi Olustur. " +
                        "Alternatif: o objeyi MenuGuard'daki 'Disable While In Menu' listesine surukle.");
                }
            }
        }

        void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;

#if UNITY_2023_1_OR_NEWER
            var custom = Object.FindFirstObjectByType<ComicCursor>();
#else
            var custom = Object.FindObjectOfType<ComicCursor>();
#endif
            bool customActive = custom != null && custom.isActiveAndEnabled
                                && custom.useCustomCursor && custom.hideSystemCursor;
            Cursor.visible = !customActive;
        }
    }
}
