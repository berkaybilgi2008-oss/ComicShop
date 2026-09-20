using UnityEngine;
using UnityEngine.EventSystems;

namespace ComicShop
{
    /// <summary>
    /// Arka plana takilir. Butonlarin atasi oldugu icin tum pointer olaylari
    /// buraya kadar yukselir; imlecin konumunu dogrudan EventSystem'den besler.
    /// Input System ayarlari ne olursa olsun calisir.
    /// </summary>
    public class CursorProbe : MonoBehaviour,
        IPointerEnterHandler, IPointerDownHandler, IDragHandler
#if UNITY_2021_1_OR_NEWER
        , IPointerMoveHandler
#endif
    {
        public static Vector2 Position;
        public static float LastUpdate = -99f;
        public static bool IsFresh => Time.unscaledTime - LastUpdate < 0.5f;

        static void Feed(PointerEventData e)
        {
            if (e == null) return;
            Position = e.position;
            LastUpdate = Time.unscaledTime;
        }

        public void OnPointerEnter(PointerEventData e) => Feed(e);
        public void OnPointerDown(PointerEventData e) => Feed(e);
        public void OnDrag(PointerEventData e) => Feed(e);
#if UNITY_2021_1_OR_NEWER
        public void OnPointerMove(PointerEventData e) => Feed(e);
#endif
    }
}
