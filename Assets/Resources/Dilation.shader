Shader "Hidden/Amplify Motion/Dilation" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
sampler2D _MotionTex;float4 _MotionTex_TexelSize;
ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 best=tex2D(_MotionTex,i.uv);float strength=dot(best.xy-0.5,best.xy-0.5)*best.a;for(int k=-1;k<=1;k++){float4 v=tex2D(_MotionTex,i.uv+_MotionTex_TexelSize.xy*float2(k,0));float next=dot(v.xy-0.5,v.xy-0.5)*v.a;if(next>strength){best=v;strength=next;}}return best;}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{float4 best=tex2D(_MotionTex,i.uv);float strength=dot(best.xy-0.5,best.xy-0.5)*best.a;for(int k=-1;k<=1;k++){float4 v=tex2D(_MotionTex,i.uv+_MotionTex_TexelSize.xy*float2(0,k));float next=dot(v.xy-0.5,v.xy-0.5)*v.a;if(next>strength){best=v;strength=next;}}return best;}
ENDCG
}
}
}
