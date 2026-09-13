using UnityEngine;

namespace ComicShop.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ToonGlobals : MonoBehaviour
    {
        public bool halftoneEnabled = true;
        static readonly int Disabled = Shader.PropertyToID("_ComicHalftoneDisabled");
        void OnEnable() => Apply();
        void OnValidate() => Apply();
        public void SetHalftone(bool enabled) { halftoneEnabled = enabled; Apply(); }
        void Apply()
        {
            Shader.SetGlobalFloat(Disabled, halftoneEnabled ? 0 : 1);
            if (halftoneEnabled) Shader.DisableKeyword("_COMIC_HALFTONE_OFF");
            else Shader.EnableKeyword("_COMIC_HALFTONE_OFF");
        }
        void OnDisable()
        {
            Shader.SetGlobalFloat(Disabled, 0);
            Shader.DisableKeyword("_COMIC_HALFTONE_OFF");
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlobals()
        {
            Shader.SetGlobalFloat(Disabled, 0);
            Shader.DisableKeyword("_COMIC_HALFTONE_OFF");
        }
    }
}
