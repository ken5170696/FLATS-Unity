Shader "Particles/Alpha Blended Premultiply" {
Properties {
 _MainTex ("Particle Texture", 2D) = "white" {}
 _InvFade ("Soft Particles Factor", Range(0.01,3)) = 1
}
	SubShader {Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"} Blend One OneMinusSrcAlpha Cull Off ZWrite Off
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_particles
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_ST;float _InvFade;UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;float4 proj:TEXCOORD1;};
 v2f vert(appdata_full v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);o.color=v.color;o.proj=ComputeScreenPos(o.pos);COMPUTE_EYEDEPTH(o.proj.z);return o;}
 fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv)*i.color;
 #ifdef SOFTPARTICLES_ON
 float d=LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture,UNITY_PROJ_COORD(i.proj)));c.a*=saturate(_InvFade*(d-i.proj.z));
 #endif
 c.rgb*=c.a;return c;}
 ENDCG
 }
}
}
