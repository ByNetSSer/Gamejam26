Shader "StylizedPaper/PixelArtObject"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [Header(Screen Pixel Grid)]
        _PixelSize("Pixel Size (screen pixels)", Range(1,32)) = 5
        _PixelAspect("Pixel Aspect", Range(0.25,4)) = 1
        _PixelSnapStrength("Vertex Snap Strength", Range(0,1)) = 1
        _PixelGridOffset("Screen Grid Offset (pixels)", Vector) = (0,0,0,0)
        [Header(Texture Pixel Grid)]
        _TexturePixelSize("Texture Grid Resolution", Range(1,256)) = 4
        _TexturePixelAspect("Texture Pixel Aspect", Range(0.25,4)) = 1
        _TexturePixelStrength("Texture Pixelation Strength", Range(0,1)) = 1
        _TextureGridOffset("Texture Grid Offset (cells)", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "PixelArtForward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor, _PixelGridOffset, _TextureGridOffset;
                float _PixelSize, _PixelAspect, _PixelSnapStrength;
                float _TexturePixelSize, _TexturePixelAspect, _TexturePixelStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 positionWS:TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                float2 grid = max(_PixelSize, 1.0) * float2(max(_PixelAspect, 0.25), 1.0);
                float2 pixelPos = (o.positionCS.xy / o.positionCS.w * 0.5 + 0.5) * _ScreenParams.xy;
                float2 snapped = floor((pixelPos - _PixelGridOffset.xy) / grid + 0.5) * grid + _PixelGridOffset.xy;
                pixelPos = lerp(pixelPos, snapped, saturate(_PixelSnapStrength));
                o.positionCS.xy = (pixelPos / _ScreenParams.xy * 2.0 - 1.0) * o.positionCS.w;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 grid = max(_TexturePixelSize, 1.0) / float2(max(_TexturePixelAspect, 0.25), 1.0);
                float2 pixelUV = (floor(i.uv * grid - _TextureGridOffset.xy) + 0.5 + _TextureGridOffset.xy) / grid;
                float2 uv = lerp(i.uv, pixelUV, saturate(_TexturePixelStrength));
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
                half3 normal = normalize(i.normalWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 illumination = SampleSH(normal) + light.color * (saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                return half4(baseColor.rgb * illumination, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback Off
}
