Shader "Unlit/Transparent Cutout" {
Properties {
 _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
 _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
}
	SubShader { Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off
 Pass {
  Name "SHADOWCASTER"
  Tags { "LightMode"="ShadowCaster" }
  ZWrite On ZTest LEqual
  CGPROGRAM
  #pragma vertex depthVert
  #pragma fragment depthFrag
  #pragma multi_compile_shadowcaster
  #include "UnityCG.cginc"
  sampler2D _MainTex; float4 _MainTex_ST; float _Cutoff;
  struct depthV2f { V2F_SHADOW_CASTER; float2 uv:TEXCOORD1; };
  depthV2f depthVert(appdata_base v) { depthV2f o; o.uv=TRANSFORM_TEX(v.texcoord,_MainTex); TRANSFER_SHADOW_CASTER_NORMALOFFSET(o); return o; }
  float4 depthFrag(depthV2f i):SV_Target { clip(tex2D(_MainTex,i.uv).a-_Cutoff); SHADOW_CASTER_FRAGMENT(i) }
  ENDCG
 }
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_ST;float _Cutoff;
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.texcoord,_MainTex);return o;}
 fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-_Cutoff);return c;}
 ENDCG
 }
}
}
