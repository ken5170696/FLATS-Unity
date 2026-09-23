Shader "Unlit/Transparent Cutout"
{
    Properties {
 _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
 _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        ZWrite On
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsFragment
            #pragma multi_compile_instancing

            #define FLATS_CUTOUT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsDepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define FLATS_SHADOW_PASS 1

            #define FLATS_CUTOUT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsDepthFragment
            #pragma multi_compile_instancing

            #define FLATS_CUTOUT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode"="DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define FLATS_CUTOUT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode"="MotionVectors" }
            ColorMask RG
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FlatsVertex
            #pragma fragment FlatsMotionFragment
            #pragma multi_compile_instancing
            #define FLATS_MOTION_PASS 1
            #define FLATS_CUTOUT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
