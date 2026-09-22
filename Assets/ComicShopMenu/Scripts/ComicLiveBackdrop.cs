using UnityEngine;
using UnityEngine.UI;

namespace ComicShop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public sealed class ComicLiveBackdrop : MonoBehaviour
    {
        [Range(0f, 1f)] public float motionAmount = 1f;
        Graphic graphic;
        Material animated, original;
        static readonly int Clock = Shader.PropertyToID("_MenuTime");
        static readonly int Amount = Shader.PropertyToID("_MotionAmount");

        void OnEnable()
        {
            graphic = GetComponent<Graphic>();
            var shader = Resources.Load<Shader>("ComicMenu/LiveBackdrop");
            if (shader == null || !shader.isSupported) return;
            original = graphic.material;
            animated = new Material(shader) { name = "Comic menu live backdrop", hideFlags = HideFlags.HideAndDontSave };
            graphic.material = animated;
            Update();
        }
        void Update()
        {
            if (animated == null) return;
            // Main menu and pause/settings menus must animate even at timeScale = 0.
            animated.SetFloat(Clock, Time.unscaledTime);
            animated.SetFloat(Amount, motionAmount);
        }
        void OnDisable()
        {
            if (graphic != null && graphic.material == animated) graphic.material = original;
            if (animated != null) Destroy(animated);
            animated = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BindExistingMenu()
        {
            // Supports the previously generated standalone MainMenu scene too.
            foreach (var graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Texture texture = graphic.mainTexture;
                if (texture != null && texture.name == "menu_background" &&
                    graphic.GetComponent<ComicLiveBackdrop>() == null)
                    graphic.gameObject.AddComponent<ComicLiveBackdrop>();
            }
        }
    }
}
