Shader "RenderFX/Skybox Cubed" {
Properties {
 _Tint ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
 _Tex ("Cubemap", CUBE) = "white" {}
}
	SubShader {Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off
 Pass {CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 samplerCUBE _Tex;float4 _Tint;
 struct v2f {float4 pos:SV_POSITION;float3 uv:TEXCOORD0;};
 // Original Store D3D11 vertex program passes TEXCOORD0.xyz unchanged.
 v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord.xyz;return o;}
 // Original pixel program: sample + tint - unity_ColorSpaceGrey; alpha * tint.a.
 float4 frag(v2f i):SV_Target{float4 c=texCUBE(_Tex,i.uv);return float4(c.rgb+_Tint.rgb-unity_ColorSpaceGrey.rgb,c.a*_Tint.a);}
 ENDCG
 }
}
}
