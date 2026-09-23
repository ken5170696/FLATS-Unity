Shader "Simple Color Texture"
{
    Properties {
 _Color ("Main Color", Color) = (1,1,1,1)
 _MainTex ("Base (RGB)", 2D) = "white" {}
}
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
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

            #define FLATS_TINT 1
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

            #define FLATS_TINT 1
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

            #define FLATS_TINT 1
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
            #define FLATS_TINT 1
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
            #define FLATS_TINT 1
            #include "FlatsUrpSurface.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
