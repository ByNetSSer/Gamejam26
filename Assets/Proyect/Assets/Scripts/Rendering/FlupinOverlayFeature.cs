using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

// Draw the assigned materials after opaque geometry, using a fresh depth buffer instead
// of the scene depth. No material or shader override is applied.
public sealed class FlupinOverlayFeature : ScriptableRendererFeature
{
    [SerializeField] LayerMask overlayLayers;
    OverlayPass pass;

    public override void Create() => pass = new OverlayPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (overlayLayers.value == 0 || renderingData.cameraData.isPreviewCamera) return;
        pass.Setup(overlayLayers);
        renderer.EnqueuePass(pass);
    }

    sealed class OverlayPass : ScriptableRenderPass
    {
        readonly List<ShaderTagId> tags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };
        LayerMask layers;
        sealed class PassData { public RendererListHandle list; }

        public void Setup(LayerMask mask) { layers = mask; requiresIntermediateTexture = true; }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var camera = frameData.Get<UniversalCameraData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var lights = frameData.Get<UniversalLightData>();
            var depthDesc = graph.GetTextureDesc(resources.activeColorTexture);
            depthDesc.name = "Flupin overlay depth";
            depthDesc.colorFormat = GraphicsFormat.None;
            depthDesc.depthBufferBits = DepthBits.Depth24;
            depthDesc.clearBuffer = true;
            TextureHandle depth = graph.CreateTexture(depthDesc);
            var drawing = RenderingUtils.CreateDrawingSettings(tags, rendering, camera, lights, SortingCriteria.CommonTransparent);
            var filtering = new FilteringSettings(RenderQueueRange.all, layers);
            var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
            using (var builder = graph.AddRasterRenderPass<PassData>("Draw Flupin over world", out var data))
            {
                data.list = list;
                builder.UseRendererList(list);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.list));
            }
        }
    }
}

