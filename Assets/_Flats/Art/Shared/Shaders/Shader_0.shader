Shader "Simple Color Texture" {
Properties {
 _Color ("Main Color", Color) = (1,1,1,1)
 _MainTex ("Base (RGB)", 2D) = "white" {}
}
SubShader {
 LOD 100
 Tags { "RenderType"="Opaque" }
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
  Tags { "RenderType"="Opaque" }
  SetTexture [_MainTex] { ConstantColor [_Color] combine constant * texture }
 }
}
}
