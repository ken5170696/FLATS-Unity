Shader "Particles/Alpha Blended Premultiply"
{
    Properties {
 _MainTex ("Particle Texture", 2D) = "white" {}
 _InvFade ("Soft Particles Factor", Range(0.01,3)) = 1
}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SOFTPARTICLES_ON
            #define FLATS_VERTEX_COLOR 1
#define FLATS_PREMULTIPLY 1
#define FLATS_SOFT_PARTICLES 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
