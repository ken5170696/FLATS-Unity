Shader "Hidden/Amplify Motion/GPUSkinDeform" {
	SubShader { Cull Off ZWrite Off ZTest Always
CGINCLUDE
#include "UnityCG.cginc"
float4x4 _Bones[64];
 struct SkinInput{float4 vertex:POSITION;float4 weights:TEXCOORD1;float4 indices:TEXCOORD2;};
 struct SkinOutput{float4 pos:SV_POSITION;float4 value:TEXCOORD0;};
 SkinOutput vert(SkinInput v){SkinOutput o;float4 p=0;for(int k=0;k<4;k++)p+=mul(_Bones[(int)v.indices[k]],v.vertex)*v.weights[k];o.pos=UnityObjectToClipPos(v.vertex);o.value=p;return o;}

ENDCG
Pass { 
CGPROGRAM
#pragma target 4.0
#pragma vertex vert
#pragma fragment frag
float4 frag(SkinOutput i):SV_Target{return i.value;}
ENDCG
}
}
}
