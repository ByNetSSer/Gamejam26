Shader "Runner/Room Curse"
{
    Properties
    {
        _BaseMap ("Original", 2D) = "white" {}
        _BaseColor ("Color original", Color) = (1,1,1,1)
        _CurseBlend ("Transición", Range(0,1)) = 0
        _CurseType ("Tipo", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; float _CurseBlend; float _CurseType;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };
            Varyings vert(Attributes input) { Varyings o; o.positionWS=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); o.normalWS=TransformObjectToWorldNormal(input.normalOS); o.uv=TRANSFORM_TEX(input.uv,_BaseMap); return o; }
            float noise(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            half4 frag(Varyings i) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                float2 p=i.positionWS.xz;
                float cell=noise(floor(p*1.7));
                float ripple=sin(p.x*3.1 + sin(p.y*2.2 + _Time.y)*1.5 + _Time.y*1.4)*0.5+0.5;
                float veins=pow(abs(sin(p.x*2.8+sin(p.y*3.7)*1.2)),18);
                half3 ice=lerp(half3(0.19,0.52,0.68),half3(0.78,0.96,1),saturate(veins+cell*0.35));
                half3 tar=lerp(half3(0.035,0.025,0.025),half3(0.24,0.14,0.11),ripple*cell);
                half3 lava=lerp(half3(0.12,0.025,0.008),half3(1.0,0.24,0.015),saturate(ripple*0.6+cell*0.45));
                half3 cursed=_CurseType < 1.5 ? ice : (_CurseType < 2.5 ? tar : lava);
                Light light=GetMainLight(); half lighting=0.35+0.65*saturate(dot(normalize(i.normalWS),light.direction));
                half3 color=lerp(base.rgb, cursed, saturate(_CurseBlend));
                return half4(color * lighting,base.a);
            }
            ENDHLSL
        }
    }
}
