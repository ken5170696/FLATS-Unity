Shader "Hidden/BloomPro" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _DsTex1 ("DsTexture 1 (RGB)", 2D) = "black" {}
 _DsTex2 ("DsTexture 2 (RGB)", 2D) = "black" {}
 _DsTex3 ("DsTexture 3 (RGB)", 2D) = "black" {}
 _DsTex4 ("DsTexture 4 (RGB)", 2D) = "black" {}
 _DsTex5 ("DsTexture 5 (RGB)", 2D) = "black" {}
 _BloomThreshold ("Bloom Threshold", Float) = 0.8
 _BloomIntensity ("Bloom Intensity", Float) = 5
 _BloomTexFactors1 ("Bloom Tex Factors 1", Vector) = (0.166,0.166,0.166,0.166)
 _BloomTexFactors2 ("Bloom Tex Factors 2", Vector) = (0.166,0.166,0,0)
 _BloomTint ("Bloom Tint", Color) = (1,1,1,1)
}
	SubShader {Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;
 float4 blur(float2 uv,float2 offset){
 float4 c=tex2D(_MainTex,uv)*0.227027;
 c+=(tex2D(_MainTex,uv+offset*1.384615)+tex2D(_MainTex,uv-offset*1.384615))*0.316216;
 c+=(tex2D(_MainTex,uv+offset*3.230769)+tex2D(_MainTex,uv-offset*3.230769))*0.070270;
 return c;
}
 sampler2D _DsTex1,_DsTex2,_DsTex3,_DsTex4,_DsTex5;
 float _BloomThreshold,_BloomIntensity;float4 _BloomTexFactors1,_BloomTexFactors2,_BloomTint,_SeparableBlurOffsets;

ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float4 c=tex2D(_MainTex,i.uv);return float4(max(c.rgb-_BloomThreshold,0),c.a);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float3 c=tex2D(_DsTex1,i.uv).rgb*_BloomTexFactors1.x+tex2D(_DsTex2,i.uv).rgb*_BloomTexFactors1.y+tex2D(_DsTex3,i.uv).rgb*_BloomTexFactors1.z+tex2D(_DsTex4,i.uv).rgb*_BloomTexFactors1.w+tex2D(_DsTex5,i.uv).rgb*_BloomTexFactors2.x;return float4(c*_BloomIntensity*_BloomTint.rgb,1);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
return blur(i.uv,_SeparableBlurOffsets.xy*_MainTex_TexelSize.xy);
}
ENDCG
}
}
}
