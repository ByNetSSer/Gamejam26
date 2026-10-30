Shader "StylizedPaper/IrregularOutline"
{
    Properties
    {
        [MainColor] _OutlineColor("Outline Color", Color) = (0.08, 0.04, 0.12, 1)
        _OutlineWidth("Outline Width", Range(0, 0.08)) = 0.015
        _NoiseAmount("Irregularity", Range(0, 1)) = 0.2
        _NoiseScale("Noise Scale", Range(1, 80)) = 18
        _NoiseSpeed("Noise Speed", Range(0, 5)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth, _NoiseAmount, _NoiseScale, _NoiseSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            float Hash(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float n = Hash(floor(p * _NoiseScale + _Time.y * _NoiseSpeed)) - 0.5;
                p += normalize(input.normalOS) * _OutlineWidth * (1.0 + n * _NoiseAmount);
                output.positionCS = TransformObjectToHClip(p);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
