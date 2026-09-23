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
 // Binomial kernels used by the original FxPro quality levels.
 float4 blur(float2 uv,float2 offset){
 #if defined(BLUR_RADIUS_10)
 const int radius=10;
 #elif defined(BLUR_RADIUS_5)
 const int radius=5;
 #else
 const int radius=3;
 #endif
 float weight=exp2(-2.0*radius);
 float3 color=0;
 for(int k=0;k<=2*radius;k++){
 color+=tex2Dlod(_MainTex,float4(uv+offset*(k-radius),0,0)).rgb*weight;
 weight*=float(2*radius-k)/float(k+1);
 }
 return float4(color,tex2D(_MainTex,uv).a);
}
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);sampler2D _COCTex;
 float _FocalDist,_FocalLength,_BlurIntensity,_OneOverDepthScale,_BokehThreshold,_BokehGain,_BokehBias;float4 _SeparableBlurOffsets;

ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile USE_CAMERA_DEPTH_TEXTURE DONT_USE_CAMERA_DEPTH_TEXTURE

float4 frag(v2f_img i):SV_Target{
 #if defined(USE_CAMERA_DEPTH_TEXTURE)
 float d=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv))*_OneOverDepthScale;
 #else
 float d=tex2D(_MainTex,i.uv).a;
 #endif
 float coc=saturate(abs(d-_FocalDist)/max(d,1e-7)*_FocalLength/max(saturate(_FocalDist-_FocalLength),1e-7));
 return float4(coc,d,0,0);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile BLUR_RADIUS_3 BLUR_RADIUS_5 BLUR_RADIUS_10

float4 frag(v2f_img i):SV_Target{
return float4(blur(i.uv,_SeparableBlurOffsets.xy*_MainTex_TexelSize.xy*tex2D(_COCTex,i.uv).r).rgb,0);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile BLUR_RADIUS_3 BLUR_RADIUS_5 BLUR_RADIUS_10

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
