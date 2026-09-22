Shader "Hidden/ColorCorrectionSelective" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 float4 selColor,targetColor;
float4 frag(v2f_img i):SV_Target {
float4 c=tex2D(_MainTex,i.uv); float w=saturate(1-length(c.rgb-selColor.rgb)); c.rgb=lerp(c.rgb,targetColor.rgb,w); return c;
}
ENDCG
}
}
}
