Shader "Hidden/Amplify Motion/MotionBlurSM3" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
sampler2D _MainTex,_MotionTex,_DepthTex;float4 _MainTex_TexelSize,_AM_BLUR_STEP,_AM_DEPTH_THRESHOLD;
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 float4 sampleMotion(float2 uv,int samples,float jitter){
 float4 motion=tex2D(_MotionTex,uv);
 float2 velocity=(motion.xy*2-1)*motion.b*_AM_BLUR_STEP.xy;
 float4 center=tex2D(_MainTex,uv);
 if(length(velocity)<1e-7)return center;
 // Mobile uses the packed depth buffer and two symmetric taps. It is the
 // original player preset; preserve object IDs through repeated blur steps.
 bool mobile=samples==4;
 float depth=LinearEyeDepth(mobile?DecodeFloatRGBA(tex2D(_DepthTex,uv)):SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));
 float objectId=floor(center.a*255+0.5);
 float4 sum=float4(center.rgb,1);
 int taps=mobile?1:(samples==12?4:2);
 for(int k=-taps;k<=taps;k++){
 if(k==0)continue;
 float2 pos=uv+velocity*(float(k)/taps);
 float sampleDepth=LinearEyeDepth(mobile?DecodeFloatRGBA(tex2D(_DepthTex,pos)):SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,pos));
 float4 color=tex2D(_MainTex,pos);
 float weight=(sampleDepth>depth-_AM_DEPTH_THRESHOLD.x || (objectId>1 && objectId<254 && color.a==center.a))?1:0;
 sum+=float4(color.rgb,1)*weight;
 }
 return float4(sum.rgb/sum.a,center.a);
 }

ENDCG
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,4,0);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,8,0);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,12,0);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,16,0);}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,4,frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453));}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,8,frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453));}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,12,frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453));}
ENDCG
}
Pass { 
CGPROGRAM
#pragma target 3.0
#pragma vertex vert_img
#pragma fragment frag
float4 frag(v2f_img i):SV_Target{return sampleMotion(i.uv,16,frac(sin(dot(i.uv,float2(12.9898,78.233)))*43758.5453));}
ENDCG
}
}
}
