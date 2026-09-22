Shader "Hidden/ColorCorrectionCurvesSimple" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "" {}
 _RgbTex ("_RgbTex (RGB)", 2D) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 sampler2D _RgbTex; float _Saturation;
float4 frag(v2f_img i):SV_Target {
float4 c=tex2D(_MainTex,i.uv);
 float3 corrected=float3(tex2D(_RgbTex,float2(c.r,0.125)).r,tex2D(_RgbTex,float2(c.g,0.375)).g,tex2D(_RgbTex,float2(c.b,0.625)).b);
c.rgb=lerp(dot(corrected,float3(0.299,0.587,0.114)).xxx,corrected,_Saturation);return c;
}
ENDCG
}
}
}
