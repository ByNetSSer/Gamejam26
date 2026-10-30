Shader "Prototype/IrregularOutlineSprite"
{
    Properties
    {
        [MainTexture] _BaseMap("Imagen PNG", 2D) = "white" {}
        _Color("Multiplicador alfa", Color) = (1,1,1,1)
        _Cutoff("Corte alfa", Range(0,1)) = 0.5
        _OutlineColor("Color del contorno", Color) = (0.08,0.04,0.12,1)
        _OutlineWidth("Grosor del contorno (texeles)", Range(0,8)) = 2
        _NoiseAmount("Irregularidad", Range(0,1)) = 0.3
        _NoiseScale("Escala del ruido", Range(1,80)) = 18
        _NoiseSpeed("Velocidad del ruido", Range(0,5)) = 0.25
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "OutlineOnly"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZTest Always
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            float4 _BaseMap_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color, _OutlineColor;
                float _Cutoff, _OutlineWidth, _NoiseAmount, _NoiseScale, _NoiseSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }
            half Mask(float2 uv)
            {
                // Do not wrap the opposite edge of the image into the outline.
                if (any(uv < 0.0) || any(uv > 1.0)) return 0;
                return step(max(_Cutoff, 0.001), SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _Color.a);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _Color;
                float frame = floor(_Time.y * _NoiseSpeed);
                float noise = Hash(floor(input.uv * _NoiseScale) + frame) * 2.0 - 1.0;
                float width = max(0.0, _OutlineWidth * (1.0 + noise * _NoiseAmount));
                float2 d = _BaseMap_TexelSize.xy * width;
                half edge = 0;
                edge = max(edge, Mask(input.uv + float2(d.x, 0)));
                edge = max(edge, Mask(input.uv - float2(d.x, 0)));
                edge = max(edge, Mask(input.uv + float2(0, d.y)));
                edge = max(edge, Mask(input.uv - float2(0, d.y)));
                edge = max(edge, Mask(input.uv + d * 0.7071));
                edge = max(edge, Mask(input.uv - d * 0.7071));
                edge = max(edge, Mask(input.uv + float2(d.x, -d.y) * 0.7071));
                edge = max(edge, Mask(input.uv + float2(-d.x, d.y) * 0.7071));
                half inside = step(max(_Cutoff, 0.001), color.a);
                half outlineAlpha = edge * _OutlineColor.a * (1.0 - inside);
                half alpha = outlineAlpha;
                clip(alpha - 0.001);
                return half4(_OutlineColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}


