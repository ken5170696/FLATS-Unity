// FLATS unlit surfaces retain their original texture/vertex colour formulas.
// URP 17.3: depth, normals and per-object/skinned motion are explicit passes.
#ifndef FLATS_URP_SURFACE_INCLUDED
#define FLATS_URP_SURFACE_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
#if defined(FLATS_LIGHTMAP)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif
#if defined(FLATS_SOFT_PARTICLES)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#endif
#if defined(FLATS_SHADOW_PASS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
float3 _LightDirection;
float3 _LightPosition;
#endif
#if defined(FLATS_MOTION_PASS)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MotionVectorsCommon.hlsl"
#endif
TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half4 _Color;
float _Cutoff;
float _InvFade;
CBUFFER_END
struct FlatsAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 previousPositionOS : TEXCOORD4;
    half4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct FlatsVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    half4 color : COLOR;
    float3 normalWS : TEXCOORD1;
    float2 lightmapUV : TEXCOORD2;
#if defined(FLATS_FOG)
    half fogIntensity : TEXCOORD6;
#endif
#if defined(FLATS_SOFT_PARTICLES)
    float eyeDepth : TEXCOORD3;
#endif
#if defined(FLATS_MOTION_PASS)
    float4 positionCSNoJitter : TEXCOORD4;
    float4 previousPositionCSNoJitter : TEXCOORD5;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};
FlatsVaryings FlatsVertex(FlatsAttributes input)
{
    FlatsVaryings output = (FlatsVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
#if defined(FLATS_FOG)
    // Match Unity's generated fixed-function passes: radial view distance,
    // with the completed fog intensity interpolated from each vertex.
    float3 eyePos = mul(UNITY_MATRIX_MV, float4(input.positionOS.xyz, 1)).xyz;
    float fogDistance = length(eyePos);
#if defined(FOG_LINEAR)
    output.fogIntensity = saturate(fogDistance * unity_FogParams.z + unity_FogParams.w);
#elif defined(FOG_EXP)
    output.fogIntensity = saturate(exp2(-unity_FogParams.y * fogDistance));
#elif defined(FOG_EXP2)
    float fogDensity = unity_FogParams.x * fogDistance;
    output.fogIntensity = saturate(exp2(-fogDensity * fogDensity));
#else
    output.fogIntensity = 1;
#endif
#endif
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(FLATS_SHADOW_PASS)
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirection = normalize(_LightPosition - positionWS);
#else
    float3 lightDirection = _LightDirection;
#endif
    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
        ApplyShadowBias(positionWS, output.normalWS, lightDirection)));
#endif
    output.color = input.color;
    output.lightmapUV = input.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
#if defined(FLATS_SOFT_PARTICLES)
    output.eyeDepth = -TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz)).z;
#endif
#if defined(FLATS_MOTION_PASS)
    // TEXCOORD4 carries the previous skinned position when Unity provides it.
    float4 previous = unity_MotionVectorsParams.x == 1
        ? float4(input.previousPositionOS, 1) : input.positionOS;
    output.positionCSNoJitter = mul(_NonJitteredViewProjMatrix, mul(UNITY_MATRIX_M, input.positionOS));
    output.previousPositionCSNoJitter = mul(_PrevViewProjMatrix, mul(UNITY_PREV_MATRIX_M, previous));
#endif
    return output;
}
half4 FlatsSample(FlatsVaryings input)
{
    half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
#if defined(FLATS_CUTOUT)
    clip(color.a - _Cutoff);
#endif
#if defined(FLATS_VERTEX_COLOR)
    color *= input.color;
#endif
#if defined(FLATS_TINT)
    color *= _Color;
#endif
    return color;
}
half4 FlatsFragment(FlatsVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    half4 color = FlatsSample(input);
#if defined(FLATS_LIGHTMAP) && defined(LIGHTMAP_ON)
    color.rgb *= SampleLightmap(input.lightmapUV, normalize(input.normalWS));
#endif
#if defined(FLATS_SOFT_PARTICLES) && defined(SOFTPARTICLES_ON)
    float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
    color.a *= saturate(_InvFade * (sceneDepth - input.eyeDepth));
#endif
#if defined(FLATS_PREMULTIPLY)
    color.rgb *= color.a;
#endif
#if defined(FLATS_FOG)
#if defined(FLATS_BLACK_FOG)
    color.rgb = lerp(half3(0, 0, 0), color.rgb, input.fogIntensity);
#else
    color.rgb = lerp(unity_FogColor.rgb, color.rgb, input.fogIntensity);
#endif
#endif
    return color;
}
half4 FlatsDepthFragment(FlatsVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    FlatsSample(input);
    return 0;
}
half4 FlatsNormalsFragment(FlatsVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    FlatsSample(input);
    float3 normalWS = normalize(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 oct = PackNormalOctQuadEncode(normalWS);
    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
#else
    return half4(normalWS, 0);
#endif
}
#if defined(FLATS_MOTION_PASS)
float4 FlatsMotionFragment(FlatsVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    FlatsSample(input);
    return float4(CalcNdcMotionVectorFromCsPositions(input.positionCSNoJitter,
        input.previousPositionCSNoJitter), 0, 0);
}
#endif
#endif
