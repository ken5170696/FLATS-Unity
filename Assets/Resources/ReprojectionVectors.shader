Shader "Hidden/Amplify Motion/ReprojectionVectors" {
Properties {
 _MainTex ("-", 2D) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
float _AM_MOTION_SCALE,_AM_MIN_VELOCITY,_AM_MAX_VELOCITY,_AM_RCP_TOTAL_VELOCITY,_AM_OBJECT_ID;
 // RG: unit direction; B: thresholded speed; A: object identity.
 float4 encodeMotion(float2 velocity){
 float2 v=velocity*_AM_MOTION_SCALE;
 float speed=length(v);
 float2 direction=speed>1e-7?v/speed:float2(0,0);
 float magnitude=max(min(speed,_AM_MAX_VELOCITY)-_AM_MIN_VELOCITY,0)*_AM_RCP_TOTAL_VELOCITY;
 return float4(direction*0.5+0.5,magnitude,_AM_OBJECT_ID);
 }

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
 float4 motion=encodeMotion(current.xy-previous.xy/max(abs(previous.w),0.00001)); motion.a=0; return motion;
}
ENDCG
}
}
}
