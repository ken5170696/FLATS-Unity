Shader "Hidden/Amplify Motion/ClothVectors" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _Cutoff ("Alpha cutoff", Range(0,1)) = 0.25
}
	SubShader { Cull Back ZWrite On ZTest LEqual
CGINCLUDE
#include "UnityCG.cginc"
float _AM_MOTION_SCALE,_AM_MAX_VELOCITY,_AM_OBJECT_ID;
 float4 encodeMotion(float2 velocity){float scale=max(_AM_MAX_VELOCITY/1000,0.00001);float2 v=clamp(velocity*_AM_MOTION_SCALE/scale,-1,1);return float4(v*0.5+0.5,_AM_OBJECT_ID,1);}
sampler2D _MainTex;float4 _MainTex_ST;float _Cutoff;float4x4 _AM_MATRIX_PREV_MVP;
 struct MotionInput {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
 struct MotionVaryings {float4 pos:SV_POSITION;float4 current:TEXCOORD0;float4 previous:TEXCOORD1;float2 uv:TEXCOORD2;};
 MotionVaryings vert(MotionInput v){MotionVaryings o;o.pos=UnityObjectToClipPos(v.vertex);o.current=o.pos;o.previous=mul(_AM_MATRIX_PREV_MVP,float4(v.normal,1));o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}

ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
float4 frag(MotionVaryings i):SV_Target{float2 velocity=(i.current.xy/max(abs(i.current.w),0.00001)-i.previous.xy/max(abs(i.previous.w),0.00001))*0.5;return encodeMotion(velocity);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
float4 frag(MotionVaryings i):SV_Target{clip(tex2D(_MainTex,i.uv).a-_Cutoff);float2 velocity=(i.current.xy/max(abs(i.current.w),0.00001)-i.previous.xy/max(abs(i.previous.w),0.00001))*0.5;return encodeMotion(velocity);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
float4 frag(MotionVaryings i):SV_Target{float2 velocity=(i.current.xy/max(abs(i.current.w),0.00001)-i.previous.xy/max(abs(i.previous.w),0.00001))*0.5;return encodeMotion(velocity);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert
#pragma fragment frag
float4 frag(MotionVaryings i):SV_Target{clip(tex2D(_MainTex,i.uv).a-_Cutoff);float2 velocity=(i.current.xy/max(abs(i.current.w),0.00001)-i.previous.xy/max(abs(i.previous.w),0.00001))*0.5;return encodeMotion(velocity);}
ENDCG
}
}
}
