Shader "Mobile/Skybox"
{
Properties {
 _FrontTex ("Front (+Z)", 2D) = "white" {}
 _BackTex ("Back (-Z)", 2D) = "white" {}
 _LeftTex ("Left (+X)", 2D) = "white" {}
 _RightTex ("Right (-X)", 2D) = "white" {}
 _UpTex ("Up (+Y)", 2D) = "white" {}
 _DownTex ("Down (-Y)", 2D) = "white" {}
}
SubShader
{
Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
Cull Off ZWrite Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_FrontTex); SAMPLER(sampler_FrontTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_FrontTex,sampler_FrontTex,input.uv);}
ENDHLSL
}
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_BackTex); SAMPLER(sampler_BackTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_BackTex,sampler_BackTex,input.uv);}
ENDHLSL
}
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_LeftTex); SAMPLER(sampler_LeftTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_LeftTex,sampler_LeftTex,input.uv);}
ENDHLSL
}
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_RightTex); SAMPLER(sampler_RightTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_RightTex,sampler_RightTex,input.uv);}
ENDHLSL
}
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_UpTex); SAMPLER(sampler_UpTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_UpTex,sampler_UpTex,input.uv);}
ENDHLSL
}
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
TEXTURE2D(_DownTex); SAMPLER(sampler_DownTex);
struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
Varyings vert(Attributes input){Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o;}
half4 frag(Varyings input):SV_Target{return SAMPLE_TEXTURE2D(_DownTex,sampler_DownTex,input.uv);}
ENDHLSL
}
}
}
