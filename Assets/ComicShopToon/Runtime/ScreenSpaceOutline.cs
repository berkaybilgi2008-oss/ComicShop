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
            [Tooltip("Selected opaque layers. ToonLit materials also respect their Outline Mask toggle.")]
            public LayerMask layerMask = -1;
        }
        public Settings settings = new Settings();
        Material material;
        OutlinePass pass;
        bool warned;
        float nextMaterialScan;
        bool materialMaskNeeded;
        static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = settings.outlineShader != null ? CoreUtils.CreateEngineMaterial(settings.outlineShader) : null;
            pass = material != null ? new OutlinePass(settings, material) : null;
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (settings.layerMask.value == 0 || pass == null || renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            if (renderingData.cameraData.cameraTargetDescriptor.msaaSamples > 1)
            {
                if (!warned) Debug.LogError("ComicShop outline requires MSAA Disabled. Use SMAA High and Render Scale 1.");
                warned = true;
                return;
            }
            // Keep the original zero-geometry Everything path when all materials opt in.
            // Refresh at 4 Hz: material Inspector edits and newly loaded runtime materials are picked up.
            if (Time.realtimeSinceStartup >= nextMaterialScan)
            {
                nextMaterialScan = Time.realtimeSinceStartup + .25f;
                materialMaskNeeded = false;
                foreach (var candidate in Resources.FindObjectsOfTypeAll<Material>())
                    if (candidate.HasProperty(OutlineEnabledId) && candidate.GetFloat(OutlineEnabledId) < .5f)
                    { materialMaskNeeded = true; break; }
            }
            pass.MaterialMaskNeeded = materialMaskNeeded;
            renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        sealed class OutlinePass : ScriptableRenderPass
        {
            public bool MaterialMaskNeeded;
            readonly Settings settings;
            readonly Material material;
            static readonly ShaderTagId MaskTag = new ShaderTagId("UniversalForward");
            static readonly ShaderTagId ForwardOnlyTag = new ShaderTagId("UniversalForwardOnly");
            static readonly ShaderTagId UnlitTag = new ShaderTagId("SRPDefaultUnlit");
            sealed class MaskData { public RendererListHandle list, toonList; public bool draw; }
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
                bool filtered = settings.layerMask.value != -1 || MaterialMaskNeeded;
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
                        draw.SetShaderPassName(1, ForwardOnlyTag);
                        draw.SetShaderPassName(2, UnlitTag);
                        draw.overrideMaterial = material;
                        draw.overrideMaterialPassIndex = 1;
                        var filter = new FilteringSettings(RenderQueueRange.opaque, settings.layerMask);
                        data.list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, draw, filter));
                        builder.UseRendererList(data.list);
                        // Draw fallback coverage first, then overwrite with original ToonLit material flags.
                        // ZTest against the camera depth keeps hidden surfaces out of both lists.
                        var toonDraw = RenderingUtils.CreateDrawingSettings(new ShaderTagId("ToonMask"),
                            rendering, camera, lights, camera.defaultOpaqueSortFlags);
                        data.toonList = graph.CreateRendererList(new RendererListParams(rendering.cullResults, toonDraw, filter));
                        builder.UseRendererList(data.toonList);
                        builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    }
                    builder.SetRenderFunc(static (MaskData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(RTClearFlags.Color, data.draw ? Color.black : Color.white, 1, 0);
                        if (data.draw)
                        {
                            context.cmd.DrawRendererList(data.list);
                            context.cmd.DrawRendererList(data.toonList);
                        }
                    });
                }
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
