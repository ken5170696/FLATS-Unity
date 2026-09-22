Shader "Hidden/Amplify Motion/Combine" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
 _CameraMotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
sampler2D _MainTex,_MotionTex;
ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return tex2D(_MainTex,i.uv);}
ENDCG
}
Pass { Blend SrcAlpha OneMinusSrcAlpha
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 c=tex2D(_MainTex,i.uv);float4 v=tex2D(_MotionTex,i.uv);c.a=1-step(0.0001,length(v.xy*2-1)*v.a);return c;}
ENDCG
}
}
}
