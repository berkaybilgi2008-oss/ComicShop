using UnityEngine;
namespace ComicShop.Rendering
{
    public sealed class ToonStyleRuntimeDriver : MonoBehaviour
    {
        void LateUpdate() => ToonStyleController.PublishActive();
    }
}
