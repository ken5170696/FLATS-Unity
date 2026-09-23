Shader "Mobile/Unlit (Supports Lightmap)" {
Properties {
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
  Tags { "LIGHTMODE"="Vertex" "RenderType"="Opaque" }
  SetTexture [_MainTex] { combine texture }
 }
 Pass {
  Tags { "LIGHTMODE"="VertexLM" "RenderType"="Opaque" }
  BindChannels {
   Bind "vertex", Vertex
   Bind "texcoord1", TexCoord0
   Bind "texcoord", TexCoord1
  }
  SetTexture [unity_Lightmap] { Matrix [unity_LightmapMatrix] combine texture }
  SetTexture [_MainTex] { combine texture * previous double, texture alpha * primary alpha }
 }
 Pass {
  Tags { "LIGHTMODE"="VertexLMRGBM" "RenderType"="Opaque" }
  BindChannels {
   Bind "vertex", Vertex
   Bind "texcoord1", TexCoord0
   Bind "texcoord", TexCoord1
  }
  SetTexture [unity_Lightmap] { Matrix [unity_LightmapMatrix] combine texture * texture alpha double }
  SetTexture [_MainTex] { combine texture * previous quad, texture alpha * primary alpha }
 }
}
}
