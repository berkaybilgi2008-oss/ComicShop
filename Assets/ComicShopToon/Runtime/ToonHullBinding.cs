using UnityEngine;
using UnityEngine.Rendering;

namespace ComicShop.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class ToonHullBinding : MonoBehaviour
    {
        public Renderer source;
        Renderer hull;
        void OnEnable()
        {
            hull = GetComponent<Renderer>();
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
        }
        void OnDisable() => RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        void LateUpdate()
        {
            if (hull == null || source == null) return;
            hull.enabled = source.enabled;
            if (source is SkinnedMeshRenderer skin && hull is SkinnedMeshRenderer outline && skin.sharedMesh != null)
                for (int i = 0; i < skin.sharedMesh.blendShapeCount; i++)
                    outline.SetBlendShapeWeight(i, skin.GetBlendShapeWeight(i));
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (hull == null || source == null) return;
            var style = ToonStyleController.ActiveStyle;
            float projection = Mathf.Max(0.001f, Mathf.Abs(camera.projectionMatrix.m11));
            float compensation = (camera.orthographic ? 1f : camera.farClipPlane)
                * 1.7320508f / (Mathf.Max(0.1f, style.OutlineReferenceDistance) * projection);
            Vector3 scale = transform.lossyScale;
            float minimumScale = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Bounds bounds = source.localBounds;
            bounds.Expand(2f * Mathf.Max(0, style.OutlineWidth) * compensation / minimumScale);
            hull.localBounds = bounds;
        }
    }
}
