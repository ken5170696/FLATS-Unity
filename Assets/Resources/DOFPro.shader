Shader "Hidden/DOFPro" {
Properties {
[HideInInspector]  _MainTex ("Base (RGB), Depth (A)", 2D) = "white" {}
[HideInInspector]  _COCTex ("COC Texture (RGBA)", 2D) = "white" {}
[HideInInspector]  _BlurOffsets ("Blur step size", Vector) = (1,0,0,0)
 _FocalDist ("Focal Dist", Float) = 0.1
 _FocalLength ("Focal Length", Float) = 0.02
 _BokehThreshold ("Bokeh Threshold", Float) = 0.5
 _BokehGain ("Bokeh Gain", Float) = 2
 _BokehBias ("Bokeh Bias", Float) = 0.5
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
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);sampler2D _COCTex;
 float _FocalDist,_FocalLength,_BlurIntensity,_OneOverDepthScale,_BokehThreshold,_BokehGain,_BokehBias;float4 _SeparableBlurOffsets;

ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float d=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv))*max(_OneOverDepthScale,1);float coc=saturate(abs(d-_FocalDist)/max(_FocalLength,0.0001));return coc.xxxx;
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
return blur(i.uv,_SeparableBlurOffsets.xy*_MainTex_TexelSize.xy*tex2D(_COCTex,i.uv).r);
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
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
return float4(tex2D(_MainTex,i.uv).rrr,1);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float radius=_BlurIntensity*tex2D(_COCTex,i.uv).r;float4 c=tex2D(_MainTex,i.uv);float total=1;for(int n=0;n<8;n++){float a=n*0.785398;float4 s=tex2D(_MainTex,i.uv+float2(cos(a),sin(a))*_MainTex_TexelSize.xy*radius);float weight=1+max(max(s.r,max(s.g,s.b))-_BokehThreshold,0)*max(_BokehGain,0)*saturate(_BokehBias);c+=s*weight;total+=weight;}return c/total;
}
ENDCG
}
}
}
