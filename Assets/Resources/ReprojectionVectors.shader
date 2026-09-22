Shader "Hidden/Amplify Motion/ReprojectionVectors" {
Properties {
 _MainTex ("-", 2D) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
float _AM_MOTION_SCALE,_AM_MAX_VELOCITY,_AM_OBJECT_ID;
 float4 encodeMotion(float2 velocity){float scale=max(_AM_MAX_VELOCITY/1000,0.00001);float2 v=clamp(velocity*_AM_MOTION_SCALE/scale,-1,1);return float4(v*0.5+0.5,_AM_OBJECT_ID,1);}

 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);float4x4 _AM_MATRIX_CURR_REPROJ;

ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{
 float depth=SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv);
 #if !defined(UNITY_REVERSED_Z)
 depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
 #endif
 float4 current=float4(i.uv*2-1,depth,1);float4 previous=mul(_AM_MATRIX_CURR_REPROJ,current);
 return encodeMotion((current.xy-previous.xy/max(abs(previous.w),0.00001))*0.5);
}
ENDCG
}
}
}
