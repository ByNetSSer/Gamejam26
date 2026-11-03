Shader "Runner/VoidEyes"
{
    Properties
    {
        [MainTexture] _MainTex ("Atlas del ojo", 2D) = "white" {}
        [MainColor] _Color ("Tinte", Color) = (1, 1, 1, 1)
        _FrameCount ("Fotogramas", Float) = 12
        _BlinkDuration ("Duración del parpadeo", Float) = 0.75
        _AlphaClip ("Recorte alfa", Range(0, 1)) = 0.04
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+20"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VoidEyes"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _FrameCount;
                float _BlinkDuration;
                float _AlphaClip;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 eyeData : TEXCOORD1;
                float2 timing : TEXCOORD2;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 timing : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 towardCamera = normalize(_WorldSpaceCameraPos.xyz - centerWS);
                float3 referenceUp = abs(towardCamera.y) > 0.96
                    ? float3(0.0, 0.0, 1.0)
                    : float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(referenceUp, towardCamera));
                float3 up = normalize(cross(towardCamera, right));

                float2 corner = input.uv * 2.0 - 1.0;
                float sine;
                float cosine;
                sincos(input.eyeData.z, sine, cosine);
                float2 rotatedCorner = float2(
                    corner.x * cosine - corner.y * sine,
                    corner.x * sine + corner.y * cosine);
                float3 positionWS = centerWS
                    + right * rotatedCorner.x * input.eyeData.x
                    + up * rotatedCorner.y * input.eyeData.y;

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.timing = input.timing;
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float frameCount = max(1.0, floor(_FrameCount + 0.5));
                float period = max(_BlinkDuration + 0.01, input.timing.y);
                float localTime = fmod(_Time.y + input.timing.x, period);
                float closing = 0.0;
                if (localTime < _BlinkDuration)
                {
                    float progress = saturate(localTime / max(0.01, _BlinkDuration));
                    closing = 1.0 - abs(progress * 2.0 - 1.0);
                }
                float frame = min(frameCount - 1.0, floor(closing * (frameCount - 1.0) + 0.5));
                float2 atlasUV = float2((frame + input.uv.x) / frameCount, input.uv.y);
                half4 sampled = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, atlasUV) * input.color;
                clip(sampled.a - _AlphaClip);
                return sampled;
            }
            ENDHLSL
        }
    }
}
