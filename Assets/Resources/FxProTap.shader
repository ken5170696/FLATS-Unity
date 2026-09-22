Shader "Hidden/FxProTap" {
Properties {
 _MainTex ("", 2D) = "white" {}
}
	SubShader {Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;
 
ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float2 t=_MainTex_TexelSize.xy;return (tex2D(_MainTex,i.uv+t)+tex2D(_MainTex,i.uv-t)+tex2D(_MainTex,i.uv+t*float2(1,-1))+tex2D(_MainTex,i.uv+t*float2(-1,1)))*0.25;
}
ENDCG
}
}
}
