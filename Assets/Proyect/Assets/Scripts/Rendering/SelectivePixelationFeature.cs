using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class SelectivePixelationFeature : ScriptableRendererFeature
{
    [SerializeField] private LayerMask pixelatedLayers;
    [SerializeField, Range(1, 64)] private int pixelWidth = 6;
    [SerializeField, Range(1, 64)] private int pixelHeight = 6;
    [SerializeField] private Shader compositeShader;
    private Material compositeMaterial;
    private PixelationPass pass;

    public override void Create()
    {
        CoreUtils.Destroy(compositeMaterial);
        if (compositeShader == null)
            compositeShader = Shader.Find("Hidden/SelectivePixelationComposite");
        if (compositeShader != null)
            compositeMaterial = CoreUtils.CreateEngineMaterial(compositeShader);
        pass = new PixelationPass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (compositeMaterial == null || pixelatedLayers.value == 0 || renderingData.cameraData.isPreviewCamera)
            return;
        pass.Setup(pixelatedLayers, pixelWidth, pixelHeight, compositeMaterial);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(compositeMaterial);

    private sealed class PixelationPass : ScriptableRenderPass
    {
        private readonly List<ShaderTagId> tags = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };
        private LayerMask layers;
        private int pixelWidth;
        private int pixelHeight;
        private Material material;
        private sealed class DrawData { public RendererListHandle rendererList; }
        private sealed class CompositeData { public TextureHandle color, depth; public Material material; }
        private static readonly int PixelDepth = Shader.PropertyToID("_PixelatedDepth");

        public void Setup(LayerMask mask, int width, int height, Material composite)
        {
            layers = mask;
            pixelWidth = Mathf.Max(1, width);
            pixelHeight = Mathf.Max(1, height);
            material = composite;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer) return;
            var camera = frameData.Get<UniversalCameraData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var lights = frameData.Get<UniversalLightData>();
            int width = Mathf.Max(1, Mathf.RoundToInt(camera.cameraTargetDescriptor.width / (float)pixelWidth));
            int height = Mathf.Max(1, Mathf.RoundToInt(camera.cameraTargetDescriptor.height / (float)pixelHeight));
            var colorDesc = new TextureDesc(width, height)
            {
                name = "Pixelated objects", colorFormat = GraphicsFormat.R8G8B8A8_UNorm,
                filterMode = FilterMode.Point, clearBuffer = true, clearColor = Color.clear
            };
            var depthDesc = new TextureDesc(width, height)
            {
                name = "Pixelated objects depth", depthBufferBits = DepthBits.Depth24,
                clearBuffer = true
            };
            TextureHandle color = graph.CreateTexture(colorDesc);
            TextureHandle depth = graph.CreateTexture(depthDesc);
            var drawing = RenderingUtils.CreateDrawingSettings(tags, rendering, camera, lights, camera.defaultOpaqueSortFlags);
            var filtering = new FilteringSettings(RenderQueueRange.opaque, layers);
            var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
            using (var builder = graph.AddRasterRenderPass<DrawData>("Render pixelated objects", out var data))
            {
                data.rendererList = list;
                builder.UseRendererList(list);
                builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                builder.SetRenderFunc(static (DrawData d, RasterGraphContext ctx) => ctx.cmd.DrawRendererList(d.rendererList));
            }
            using (var builder = graph.AddRasterRenderPass<CompositeData>("Composite pixelated objects", out var data))
            {
                data.color = color;
                data.depth = depth;
                data.material = material;
                builder.UseTexture(color);
                builder.UseTexture(depth);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (CompositeData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(PixelDepth, d.depth);
                    Blitter.BlitTexture(ctx.cmd, d.color, new Vector4(1, 1, 0, 0), d.material, 0);
                });
            }
        }
    }
}
