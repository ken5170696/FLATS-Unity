Shader "Hidden/FXAA3" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
}
	SubShader { Cull Off ZWrite Off ZTest Always
 Pass { CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 
float4 frag(v2f_img i):SV_Target {

 float2 t=_MainTex_TexelSize.xy;float3 l=float3(0.299,0.587,0.114);
 float4 c=tex2D(_MainTex,i.uv);
 float nw=dot(tex2D(_MainTex,i.uv+t*float2(-1,-1)).rgb,l);
 float ne=dot(tex2D(_MainTex,i.uv+t*float2(1,-1)).rgb,l);
 float sw=dot(tex2D(_MainTex,i.uv+t*float2(-1,1)).rgb,l);
 float se=dot(tex2D(_MainTex,i.uv+t*float2(1,1)).rgb,l);float m=dot(c.rgb,l);
 float lo=min(m,min(min(nw,ne),min(sw,se))),hi=max(m,max(max(nw,ne),max(sw,se)));
 float2 dir=float2(-((nw+ne)-(sw+se)),(nw+sw)-(ne+se));
 float reduce=max((nw+ne+sw+se)*0.03125,0.0078125);
 dir=clamp(dir/(min(abs(dir.x),abs(dir.y))+reduce),-8,8)*t;
 float3 a=0.5*(tex2D(_MainTex,i.uv-dir/6).rgb+tex2D(_MainTex,i.uv+dir/6).rgb);
 float3 b=a*0.5+0.25*(tex2D(_MainTex,i.uv-dir*0.5).rgb+tex2D(_MainTex,i.uv+dir*0.5).rgb);
 float lb=dot(b,l);return float4(lb<lo||lb>hi?a:b,c.a);

}
ENDCG
}
}
}
