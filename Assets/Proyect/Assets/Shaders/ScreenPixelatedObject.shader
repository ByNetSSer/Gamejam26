Shader "StylizedPaper/ScreenPixelatedObject"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _PixelSize("Pixel Size (screen pixels)", Range(1,64)) = 6
        _PixelAspect("Pixel Aspect", Range(0.25,4)) = 1
        _PixelGridOffset("Grid Offset (screen pixels)", Vector) = (0,0,0,0)
        _PixelStrength("Pixelation Strength", Range(0,1)) = 1
        [HideInInspector] _Overlay("Draw On Top", Float) = 0
        [HideInInspector] _ZTest("Depth Test", Float) = 4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "PixelatedForward"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off ZTest [_ZTest] Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
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
                float4 _BaseMap_ST, _BaseColor, _PixelGridOffset;
                float _PixelSize, _PixelAspect, _PixelStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 grid = max(_PixelSize, 1.0) * float2(max(_PixelAspect, 0.25), 1.0);
                float2 cellCenter = (floor((i.positionCS.xy - _PixelGridOffset.xy) / grid) + 0.5) * grid + _PixelGridOffset.xy;
                float2 delta = (cellCenter - i.positionCS.xy) * saturate(_PixelStrength);
                float2 uv = i.uv + ddx(i.uv) * delta.x + ddy(i.uv) * delta.y;
                float3 positionWS = i.positionWS + ddx(i.positionWS) * delta.x + ddy(i.positionWS) * delta.y;
                half3 normal = normalize(i.normalWS + ddx(i.normalWS) * delta.x + ddy(i.normalWS) * delta.y);
                half4 baseColor = SAMPLE_TEXTURE2D_GRAD(_BaseMap, sampler_BaseMap, uv, ddx(i.uv), ddy(i.uv)) * _BaseColor;
                Light light = GetMainLight(TransformWorldToShadowCoord(positionWS));
                half3 illumination = SampleSH(normal) + light.color * (saturate(dot(normal, light.direction)) * light.distanceAttenuation * light.shadowAttenuation);
                return half4(baseColor.rgb * illumination, baseColor.a);
            }
            ENDHLSL
        }
    }
    CustomEditor "ScreenPixelatedObjectGUI"
    Fallback Off
}
