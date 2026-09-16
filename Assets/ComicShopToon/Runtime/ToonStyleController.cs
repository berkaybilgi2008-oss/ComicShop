using UnityEngine;

namespace ComicShop.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-10000)]
    public sealed class ToonStyleController : MonoBehaviour
    {
        public ToonStyleAsset style;
        static ToonStyleController owner;
        static ToonStyleAsset defaultStyle;
        static ToonStyleAsset fallback;
        public static ToonStyleAsset DefaultStyle
        {
            get
            {
                if (defaultStyle == null) defaultStyle = Resources.Load<ToonStyleAsset>("ComicShopToon/DefaultToonStyle");
                if (defaultStyle != null) return defaultStyle;
                if (fallback == null)
                {
                    fallback = ScriptableObject.CreateInstance<ToonStyleAsset>();
                    fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return fallback;
            }
        }
        public static ToonStyleAsset ActiveStyle => owner != null && owner.style != null ? owner.style : DefaultStyle;
        public static void PublishActive() => ActiveStyle.Apply();
        void OnEnable()
        {
            if (owner != null && owner != this)
            {
                Debug.LogWarning("Only one ToonStyleController can own the global style. This duplicate is ignored.", this);
                return;
            }
            owner = this;
            PublishActive();
        }
        void OnValidate()
        {
            // Unity can invoke validation on a loading thread; defer actual shader API calls.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= ValidateOnMainThread;
            UnityEditor.EditorApplication.delayCall += ValidateOnMainThread;
#endif
        }
#if UNITY_EDITOR
        void ValidateOnMainThread()
        {
            if (this != null && isActiveAndEnabled && (owner == null || owner == this))
            {
                owner = this;
                PublishActive();
                UnityEditor.SceneView.RepaintAll();
            }
        }
#endif
        void Update() { if (owner == this) PublishActive(); }
        void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= ValidateOnMainThread;
#endif
            if (owner != this) return;
            owner = null;
            // Re-elect a remaining scene controller, without last-writer-wins flicker.
            foreach (var candidate in FindObjectsByType<ToonStyleController>(FindObjectsSortMode.InstanceID))
                if (candidate != this && candidate.isActiveAndEnabled) { owner = candidate; break; }
            PublishActive();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() { owner = null; defaultStyle = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            foreach (var candidate in FindObjectsByType<ToonStyleController>(FindObjectsSortMode.InstanceID))
                if (candidate.isActiveAndEnabled) { owner = candidate; break; }
            PublishActive();
            // A scene component is optional: the shipped default asset is always active.
            var go = new GameObject("ComicShop Global Toon Style") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            go.AddComponent<ToonStyleRuntimeDriver>();
        }
    }
}
