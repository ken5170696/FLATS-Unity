Shader "Hidden/EdgeDetect" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "" {}
}
	SubShader {Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;
 sampler2D _CameraDepthNormalsTexture;UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 float4 _Sensitivity,_BgColor;float _BgFade,_SampleDistance,_Exponent,_Threshold;
 float4 edgeResult(float2 uv,float edge){float4 c=lerp(tex2D(_MainTex,uv),_BgColor,_BgFade);c.rgb*=1-saturate(edge);return c;}
 float sobelDepth(float2 uv, bool thin){
 float2 t=abs(_MainTex_TexelSize.xy)*_SampleDistance;
 float center=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));
 float4 axis=float4(
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+float2(0,t.y))),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv-float2(t.x,0))),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+float2(t.x,0))),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv-float2(0,t.y))));
 float4 diagonal=float4(
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+t)),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+t*float2(-1,1))),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv+t*float2(1,-1))),
 Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv-t)));
 if(thin){axis=max(axis,center);diagonal=max(diagonal,center);}
 axis/=max(center,1e-7); diagonal-=center;
 float x=dot(axis,float4(1,0,0,-1))+dot(diagonal,float4(1,1,-1,-1));
 float y=dot(axis,float4(0,1,-1,0))+dot(diagonal,float4(-1,1,-1,1));
 return pow(saturate(length(float2(x,y))),_Exponent);
 }

ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float2 t=_MainTex_TexelSize.xy*_SampleDistance;float4 a=tex2D(_CameraDepthNormalsTexture,i.uv-t),b=tex2D(_CameraDepthNormalsTexture,i.uv+t);float4 c=tex2D(_CameraDepthNormalsTexture,i.uv+t*float2(-1,1)),d=tex2D(_CameraDepthNormalsTexture,i.uv+t*float2(1,-1));float n=(length(a.xy-b.xy)+length(c.xy-d.xy))*_Sensitivity.x;float z=(abs(DecodeFloatRG(a.zw)-DecodeFloatRG(b.zw))+abs(DecodeFloatRG(c.zw)-DecodeFloatRG(d.zw)))*_Sensitivity.y*100;return edgeResult(i.uv,step(0.1,n+z));
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float2 t=_MainTex_TexelSize.xy*_SampleDistance;float4 a=tex2D(_CameraDepthNormalsTexture,i.uv-t),b=tex2D(_CameraDepthNormalsTexture,i.uv+t);float4 c=tex2D(_CameraDepthNormalsTexture,i.uv+t*float2(-1,1)),d=tex2D(_CameraDepthNormalsTexture,i.uv+t*float2(1,-1));float n=(length(a.xy-b.xy)+length(c.xy-d.xy))*_Sensitivity.x;float z=(abs(DecodeFloatRG(a.zw)-DecodeFloatRG(b.zw))+abs(DecodeFloatRG(c.zw)-DecodeFloatRG(d.zw)))*_Sensitivity.y*100;return edgeResult(i.uv,step(0.1,n+z));
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
return edgeResult(i.uv,sobelDepth(i.uv,false));
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
return edgeResult(i.uv,sobelDepth(i.uv,true));
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0

float4 frag(v2f_img i):SV_Target{
float2 t=_MainTex_TexelSize.xy*_SampleDistance;float3 l=float3(0.299,0.587,0.114);float a=dot(tex2D(_MainTex,i.uv-t).rgb,l),b=dot(tex2D(_MainTex,i.uv+t).rgb,l);return edgeResult(i.uv,step(_Threshold,abs(a-b)));
}
ENDCG
}
}
}
