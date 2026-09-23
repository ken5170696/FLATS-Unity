Shader "RenderFX/Skybox Cubed" {
Properties {
 _Tint ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
 _Tex ("Cubemap", CUBE) = "white" {}
}
	SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass {HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURECUBE(_Tex); SAMPLER(sampler_Tex);
 CBUFFER_START(UnityPerMaterial)
 float4 _Tint;
 CBUFFER_END
 struct appdata_base { float4 vertex:POSITION; float4 texcoord:TEXCOORD0; };
 struct v2f {float4 pos:SV_POSITION;float3 uv:TEXCOORD0;};
 // Original Store D3D11 vertex program passes TEXCOORD0.xyz unchanged.
 v2f vert(appdata_base v){v2f o;o.pos=TransformObjectToHClip(v.vertex.xyz);o.uv=v.texcoord.xyz;return o;}
 // Original pixel program: sample + tint - unity_ColorSpaceGrey; alpha * tint.a.
 float4 frag(v2f i):SV_Target
 {
  float4 c=SAMPLE_TEXTURECUBE(_Tex,sampler_Tex,i.uv);
  #if defined(UNITY_COLORSPACE_GAMMA)
  const float grey = 0.5;
  #else
  const float grey = 0.21404114;
  #endif
  return float4(c.rgb+_Tint.rgb-grey,c.a*_Tint.a);
 }
 ENDHLSL
 }
}
}
