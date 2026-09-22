Shader "Hidden/FxPro" {
Properties {
 _MainTex ("Base (RGB)", 2D) = "white" {}
 _ChromAberrTex ("Chromatic Aberration (RGB)", 2D) = "black" {}
 _LensDirtTex ("Lens Dirt Texture", 2D) = "black" {}
 _DirtIntensity ("Lens Dirt Intensity", Float) = 0.1
 _ChromaticAberrationOffset ("Chromatic Aberration Offset", Float) = 1
 _BloomTex ("Bloom (RGBA)", 2D) = "black" {}
 _DOFTex ("DOF (RGB), COC(A)", 2D) = "black" {}
 _COCTex ("COC Texture (RGBA)", 2D) = "white" {}
 _SCurveIntensity ("S-Curve Intensity", Float) = 0.5
 _LensCurvatureBarrelPower ("Lens Curvature Barrel Power", Float) = 1.1
 _LensCurvatureZoom ("Lens Curvature Zoom", Float) = 1
 _FilmGrainTex ("Film Grain (RGB)", 2D) = "white" {}
 _FilmGrainIntensity ("Film Grain Intensity", Float) = 0.5
 _FilmGrainTiling ("Film Grain Tiling", Float) = 4
 _FilmGrainChannel ("Film Grain Channel", Vector) = (1,0,0,0)
 _VignettingIntensity ("Vignetting Intensity", Float) = 0.5
 _CloseTint ("Warm Tint Color", Color) = (1,0.5,0,1)
 _FarTint ("Warm Tint Color", Color) = (0,0,1,1)
 _CloseTintStrength ("Close Tint Strength", Float) = 0.5
 _FarTintStrength ("Far Tint Strength", Float) = 0.5
 _DesaturateDarksStrength ("Desaturate Darks Strength", Float) = 0.25
 _DesaturateFarObjsStrength ("Desaturate Far Objs Strength", Float) = 0.5
 _FogTint ("Fog Tint Color", Color) = (1,1,1,1)
 _FogStrength ("Fog Strength", Float) = 0.5
}
	SubShader {Cull Off ZWrite Off ZTest Always
 CGINCLUDE
 #include "UnityCG.cginc"
 sampler2D _MainTex;float4 _MainTex_TexelSize;
 
 sampler2D _DOFTex,_COCTex,_BloomTex,_LensDirtTex,_ChromAberrTex,_FilmGrainTex;
 UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
 float _DirtIntensity,_FilmGrainTiling,_FilmGrainIntensity,_VignettingIntensity,_SCurveIntensity,_ChromaticAberrationOffset;
 float _LensCurvatureBarrelPower,_LensCurvatureZoom,_CloseTintStrength,_FarTintStrength,_DesaturateDarksStrength,_DesaturateFarObjsStrength,_FogStrength;
 float4 _FilmGrainChannel,_CloseTint,_FarTint,_FogTint;

ENDCG
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
float4 c=tex2D(_MainTex,i.uv);
 #ifdef DOF_ENABLED
 c.rgb=lerp(c.rgb,tex2D(_DOFTex,i.uv).rgb,tex2D(_COCTex,i.uv).r);
 #endif
 #ifdef BLOOM_ENABLED
 c.rgb+=tex2D(_BloomTex,i.uv).rgb;
 #endif
 #ifdef LENS_DIRT_ON
 c.rgb+=tex2D(_BloomTex,i.uv).rgb*tex2D(_LensDirtTex,i.uv).rgb*_DirtIntensity;
 #endif
 #ifdef CHROMATIC_ABERRATION_ON
 c.r=tex2D(_ChromAberrTex,i.uv).r;
 #endif
 #ifdef FILM_GRAIN_ON
 c.rgb+=(dot(tex2D(_FilmGrainTex,i.uv*_FilmGrainTiling),_FilmGrainChannel)-0.5)*_FilmGrainIntensity;
 #endif
 #ifdef COLOR_FX_ON
 float d=Linear01Depth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,i.uv));
 float l=dot(c.rgb,float3(0.299,0.587,0.114));
 c.rgb=lerp(c.rgb,l.xxx,saturate(_DesaturateDarksStrength*(1-l)+_DesaturateFarObjsStrength*d));
 c.rgb=lerp(c.rgb,c.rgb*_CloseTint.rgb,saturate(_CloseTintStrength*(1-d)));
 c.rgb=lerp(c.rgb,c.rgb*_FarTint.rgb,saturate(_FarTintStrength*d));
 c.rgb=lerp(c.rgb,_FogTint.rgb,saturate(_FogStrength*d));
 #endif
 #ifdef VIGNETTING_ON
 float2 v=i.uv*2-1;c.rgb*=saturate(1-dot(v,v)*_VignettingIntensity);
 #endif
 c.rgb=lerp(c.rgb,c.rgb*c.rgb*(3-2*c.rgb),saturate(_SCurveIntensity));return c;
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
return tex2D(_MainTex,i.uv);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
float2 off=(i.uv-0.5)*_ChromaticAberrationOffset*_MainTex_TexelSize.xy;float4 c=tex2D(_MainTex,i.uv);c.r=tex2D(_MainTex,i.uv+off).r;c.b=tex2D(_MainTex,i.uv-off).b;return c;
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
float2 p=i.uv*2-1;float radius=length(p);float2 uv=0.5+0.5*p*pow(max(radius,0.00001),_LensCurvatureBarrelPower-1)/max(_LensCurvatureZoom,0.01);return tex2D(_MainTex,uv);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
float2 p=i.uv*2-1;float radius=length(p);float2 uv=0.5+0.5*p*pow(max(radius,0.00001),_LensCurvatureBarrelPower-1)/max(_LensCurvatureZoom,0.01);return tex2D(_MainTex,uv);
}
ENDCG
}
Pass { CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#pragma multi_compile __ DOF_ENABLED
#pragma multi_compile __ BLOOM_ENABLED
#pragma multi_compile __ LENS_DIRT_ON
#pragma multi_compile __ CHROMATIC_ABERRATION_ON
#pragma multi_compile __ FILM_GRAIN_ON
#pragma multi_compile __ VIGNETTING_ON
#pragma multi_compile __ COLOR_FX_ON
float4 frag(v2f_img i):SV_Target{
float4 c=tex2D(_MainTex,i.uv);return float4(c.rgb/(1+c.rgb),c.a);
}
ENDCG
}
}
}
