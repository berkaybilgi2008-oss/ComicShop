using UnityEngine;
namespace ComicShop.Rendering
{
    // Serialized compatibility shell for existing scenes. The style asset owns the look.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ToonGlobals : MonoBehaviour
    {
        [HideInInspector] public bool halftoneEnabled = true;
        public void SetHalftone(bool enabled)
        {
            ToonStyleController.ActiveStyle.HalftoneEnabled = enabled ? 1 : 0;
            ToonStyleController.PublishActive();
        }
        void OnEnable() => ToonStyleController.PublishActive();
    }
}
