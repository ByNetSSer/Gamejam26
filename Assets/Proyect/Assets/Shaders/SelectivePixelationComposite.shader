Shader "Hidden/SelectivePixelationComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            TEXTURE2D_X_FLOAT(_PixelatedDepth);
            struct FragmentOutput { half4 color : SV_Target; float depth : SV_Depth; };
            FragmentOutput Frag(Varyings input)
            {
                FragmentOutput output;
                output.color = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, input.texcoord, 0);
                clip(output.color.a - 0.5);
                output.depth = SAMPLE_TEXTURE2D_X_LOD(_PixelatedDepth, sampler_PointClamp, input.texcoord, 0).r;
                return output;
            }
            ENDHLSL
        }
    }
    Fallback Off
}

