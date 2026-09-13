using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ComicShop.Rendering
{
    public sealed class ScreenSpaceOutline : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            public Shader outlineShader;
            public Color outlineColor = new Color(0.025f, 0.018f, 0.04f, 1);
            [Range(1, 4)] public float thickness = 1.5f;
            [Range(0.001f, 0.2f)] public float depthThreshold = 0.035f;
            [Range(0.01f, 2)] public float normalThreshold = 0.25f;
            public LayerMask layerMask = -1;
        }
        public Settings settings = new Settings();
        Material material;
        OutlinePass pass;
        bool warned;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = settings.outlineShader != null ? CoreUtils.CreateEngineMaterial(settings.outlineShader) : null;
            pass = material != null ? new OutlinePass(settings, material) : null;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            if (renderingData.cameraData.cameraTargetDescriptor.msaaSamples > 1)
            {
                if (!warned) Debug.LogError("ComicShop outline requires MSAA Disabled. Use SMAA High and Render Scale 1.");
                warned = true;
                return;
            }
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class OutlinePass : ScriptableRenderPass
        {
            readonly Settings settings;
            readonly Material material;
            static readonly ShaderTagId MaskTag = new ShaderTagId("ToonMask");
            static readonly int ColorId = Shader.PropertyToID("_OutlineColor");
            static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
            static readonly int DepthId = Shader.PropertyToID("_DepthThreshold");
            static readonly int NormalId = Shader.PropertyToID("_NormalThreshold");

            sealed class MaskData { public RendererListHandle list; public bool draw; }
            sealed class InkData { public TextureHandle mask; public Material material; }
            public OutlinePass(Settings settings, Material material)
            {
                this.settings = settings;
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.cameraNormalsTexture.IsValid()) return;
                bool filtered = settings.layerMask.value != -1;
                // No full scene redraw for Everything: a 1x1 white mask is sufficient.
                var desc = camera.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R8_UNorm;
                desc.msaaSamples = 1;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                if (!filtered) { desc.width = 1; desc.height = 1; }
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(graph, desc,
                    "ComicShop Visible Layer Mask", true, FilterMode.Point);
                using (var builder = graph.AddRasterRenderPass<MaskData>("ComicShop Outline Mask", out var data))
                {
                    data.draw = filtered;
                    builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                    if (filtered)
                    {
                        var draw = RenderingUtils.CreateDrawingSettings(MaskTag, rendering, camera, lights,
                            camera.defaultOpaqueSortFlags);
                        var filter = new FilteringSettings(RenderQueueRange.opaque, settings.layerMask);
                        data.list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, draw, filter));
                        builder.UseRendererList(data.list);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    }
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Color, data.draw ? Color.black : Color.white, 1, 0);
                        if (data.draw) context.cmd.DrawRendererList(data.list);
                    });
                }
                material.SetColor(ColorId, settings.outlineColor.linear);
                material.SetFloat(ThicknessId, settings.thickness);
                material.SetFloat(DepthId, settings.depthThreshold);
                material.SetFloat(NormalId, settings.normalThreshold);
                using (var builder = graph.AddRasterRenderPass<InkData>("ComicShop Sobel Ink", out var data))
                {
                    data.mask = mask;
                    data.material = material;
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                    // URP binds these named globals; declare their graph lifetime as well as explicit handles.
                    builder.UseAllGlobalTextures(true);
                    // Blending reads the attachment, but the shader never samples its own output.
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (InkData data, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, data.mask, new Vector4(1, 1, 0, 0), data.material, 0));
                }
            }
        }
    }
}
