Shader "Hidden/Amplify Motion/Debug" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
sampler2D _MotionTex;
ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 v=tex2D(_MotionTex,i.uv);return float4(abs(v.xy*2-1)*v.a,v.b,1);}
ENDCG
}
}
}
