Shader "Texture Only" {
 Properties { _MainTex ("Texture", 2D) = "white" {} }
 SubShader {
  Tags { "RenderType"="Opaque" }
  // Built-in depth textures use ShadowCaster passes in the current renderer.
  Pass {
  Name "SHADOWCASTER"
  Tags { "LightMode"="ShadowCaster" }
  ZWrite On ZTest LEqual
  CGPROGRAM
  #pragma vertex depthVert
  #pragma fragment depthFrag
  #pragma multi_compile_shadowcaster
  #include "UnityCG.cginc"

  struct depthV2f { V2F_SHADOW_CASTER;  };
  depthV2f depthVert(appdata_base v) { depthV2f o;  TRANSFER_SHADOW_CASTER_NORMALOFFSET(o); return o; }
  float4 depthFrag(depthV2f i):SV_Target {  SHADOW_CASTER_FRAGMENT(i) }
  ENDCG
 }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex;
   float4 _MainTex_ST;
   struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
   struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
   v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=TRANSFORM_TEX(v.uv,_MainTex); return o; }
   fixed4 frag(v2f i):SV_Target { return tex2D(_MainTex,i.uv); }
   ENDCG
  }
 }
}
