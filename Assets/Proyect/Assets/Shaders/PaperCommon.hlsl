#ifndef STYLIZED_PAPER_COMMON_INCLUDED
#define STYLIZED_PAPER_COMMON_INCLUDED

// Textures
TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
TEXTURE2D(_GrainTex); SAMPLER(sampler_GrainTex);

// All material properties live in one CBUFFER so the SRP Batcher works.
// This block must be identical in every pass (it is, because every pass includes this file).
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseColor;
    float4 _ShadowColor;
    float4 _HighlightColor;
    float  _LightingSteps;
    float  _ShadowThreshold;
    float  _WrapLighting;
    float  _EdgeSmooth;
    float  _LightingInfluence;
    float  _GrainIntensity;
    float  _GrainScale;
    float  _InkVariation;
    float  _InkScale;
    float  _ColorQuantization;
    float  _Cutoff;
CBUFFER_END

// ---------------------------------------------------------------------------
// Noise helpers
// ---------------------------------------------------------------------------

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Two octaves are enough for soft pigment blotches.
float SoftNoise(float2 p)
{
    return ValueNoise(p) * 0.65 + ValueNoise(p * 2.13 + 7.1) * 0.35;
}

// ---------------------------------------------------------------------------
// Paper grain: supplied texture (keyword _GRAINTEX) or procedural fallback.
// Returns roughly 0..1 with an average near 0.5.
// ---------------------------------------------------------------------------
float PaperGrain(float2 uv)
{
    float2 p = uv * _GrainScale;
    #if defined(_GRAINTEX)
        return SAMPLE_TEXTURE2D(_GrainTex, sampler_GrainTex, p).r;
    #else
        float coarse = ValueNoise(p);
        float fine   = Hash21(floor(p * 4.0));
        return lerp(coarse, fine, 0.5);
    #endif
}

// ---------------------------------------------------------------------------
// Stepped lighting. Input: lit (0..1). Output: band value 0..1 with
// 'steps' discrete levels. Edges are antialiased with fwidth so bands
// do not shimmer as the camera moves.
// ---------------------------------------------------------------------------
float SteppedLight(float lit)
{
    float steps = max(_LightingSteps, 2.0);
    float t = saturate((lit - _ShadowThreshold) / max(1.0 - _ShadowThreshold, 1e-4));
    float x = t * steps;
    float k = floor(x);
    float w = max(fwidth(x) * _EdgeSmooth, 1e-4);
    float s = saturate((x - k) / w);
    float level = clamp(k - 1.0 + s, 0.0, steps - 1.0);
    return level / (steps - 1.0);
}

// Reduce each color channel to a limited number of levels (<2 = off).
float3 QuantizeColor(float3 c, float levels)
{
    if (levels < 2.0) return c;
    float n = levels - 1.0;
    return floor(c * n + 0.5) / n;
}

#endif
