Shader "Hidden/BlurEffectConeTap" {
Properties {
 _MainTex ("", any) = "" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 struct InputVertex {float4 vertex:POSITION; float2 a:TEXCOORD0; float2 b:TEXCOORD1; float2 c:TEXCOORD2; float2 d:TEXCOORD3;};
 struct Varyings {float4 pos:SV_POSITION; float2 a:TEXCOORD0; float2 b:TEXCOORD1; float2 c:TEXCOORD2; float2 d:TEXCOORD3;};
 Varyings vert(InputVertex v){Varyings o;o.pos=UnityObjectToClipPos(v.vertex);o.a=v.a;o.b=v.b;o.c=v.c;o.d=v.d;return o;}
 float4 frag(Varyings i):SV_Target{return (tex2D(_MainTex,i.a)+tex2D(_MainTex,i.b)+tex2D(_MainTex,i.c)+tex2D(_MainTex,i.d))*0.25;}
 ENDCG
 }
}
}
