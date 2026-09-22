Shader "Hidden/CC_Grayscale" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _Data ("Luminance (RGB) + Amount (A)", Vector) = (0.3,0.59,0.11,1)
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 float4 _Data;
float4 frag(v2f_img i):SV_Target {
float4 c=tex2D(_MainTex,i.uv); c.rgb=lerp(c.rgb,dot(c.rgb,_Data.rgb).xxx,saturate(_Data.a)); return c;
}
ENDCG
}
}
}
