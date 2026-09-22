Shader "Hidden/Amplify Motion/Depth" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return EncodeFloatRGBA(min(0.999999,Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv))));}
ENDCG
}
}
}
