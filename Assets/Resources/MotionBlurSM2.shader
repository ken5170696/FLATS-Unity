Shader "Hidden/Amplify Motion/MotionBlurSM2" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _MotionTex ("Motion (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
sampler2D _MainTex,_MotionTex;float4 _MainTex_TexelSize,_AM_BLUR_STEP,_AM_DEPTH_THRESHOLD;UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 float4 sampleMotion(float2 uv,int samples,float jitter){float4 motion=tex2D(_MotionTex,uv);float2 velocity=(motion.xy*2-1)*motion.a*_AM_BLUR_STEP.xy;
 if(length(velocity)<0.00001)return tex2D(_MainTex,uv);
 float depth=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));float4 color=0;float total=0;
 for(int k=0;k<16;k++){if(k>=samples)break;float2 pos=saturate(uv+velocity*((k+jitter)/max(samples-1,1)-0.5));float d=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,pos));float w=abs(depth-d)<max(_AM_DEPTH_THRESHOLD.x,0.001)?1:0.1;color+=tex2D(_MainTex,pos)*w;total+=w;}return color/max(total,0.0001);}

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
