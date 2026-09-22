Shader "Hidden/FisheyeShader" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 float4 intensity;
float4 frag(v2f_img i):SV_Target {
float2 p=i.uv*2-1; float2 uv=i.uv+p*dot(p,p)*intensity.xy; return tex2D(_MainTex,saturate(uv));
}
ENDCG
}
}
}
