Shader "Mobile/Particles/Alpha Blended"
{
    Properties {
 _MainTex ("Particle Texture", 2D) = "white" {}
}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsFragment
            #pragma multi_compile_instancing

            #define FLATS_VERTEX_COLOR 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
