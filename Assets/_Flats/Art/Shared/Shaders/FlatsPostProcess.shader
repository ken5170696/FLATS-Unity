Shader "Hidden/FLATS/URPPostProcess"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
        TEXTURE2D_X(_FlatsAuxTex);
        TEXTURE2D_X(_FlatsCoCTex);
        TEXTURE2D_X(_MotionVectorTexture);
        TEXTURE2D_X(_FlatsMotionIds);
        TEXTURE2D(_FlatsMotionAlpha); SAMPLER(sampler_FlatsMotionAlpha);
        float _FlatsMotionId, _FlatsMotionCutoff;
        float4 _FlatsMotionAlphaST;
        TEXTURE2D(_Curves); SAMPLER(sampler_Curves);
        float4 _Focus, _Direction, _Bokeh, _Edge, _Sensitivity, _EdgeBackground, _Motion, _MotionOptions, _Gray, _Fish, _Correction;
        float4 _SelectiveFrom, _SelectiveTo;
        float _Radius, _SCurve;
        float4 ColorAt(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, saturate(uv), 0); }
        float4 ColorPointAt(float2 uv) { return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, saturate(uv), 0); }
        float Depth01(float2 uv) { return Linear01Depth(SampleSceneDepth(saturate(uv)), _ZBufferParams); }
        float DepthEye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(saturate(uv)), _ZBufferParams); }
        float CocAt(float2 uv) { return SAMPLE_TEXTURE2D_X(_FlatsCoCTex, sampler_LinearClamp, saturate(uv)).r; }
        float4 CircleOfConfusion(Varyings input) : SV_Target
        {
            float depth = Depth01(input.texcoord) * _Focus.z;
            float coc = saturate(abs(depth - _Focus.x) / max(depth, 1e-7) * _Focus.y / max(saturate(_Focus.x - _Focus.y), 1e-7));
            return float4(coc, depth, 0, 0);
        }
        float4 BinomialBlur(float2 uv, float2 stepSize)
        {
            int radius = (int)_Radius;
            float weight = exp2(-2.0 * radius);
            float3 color = 0;
            [loop] for (int k = 0; k <= 2 * radius; k++)
            {
                color += ColorAt(uv + stepSize * (k - radius)).rgb * weight;
                weight *= float(2 * radius - k) / float(k + 1);
            }
            return float4(color, ColorAt(uv).a);
        }
        float4 DepthBlur(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            float radius = _Focus.w * CocAt(uv);
            if (_Bokeh.x > .5)
            {
                float4 color = ColorAt(uv); float total = 1;
                [loop] for (int n = 0; n < 8; n++)
                {
                    float a = n * .785398;
                    float4 sampleColor = ColorAt(uv + float2(cos(a), sin(a)) * _BlitTexture_TexelSize.xy * radius);
                    float weight = 1 + max(max(sampleColor.r, max(sampleColor.g, sampleColor.b)) - _Bokeh.y, 0) * max(_Bokeh.z, 0) * saturate(_Bokeh.w);
                    color += sampleColor * weight; total += weight;
                }
                return color / total;
            }
            return BinomialBlur(uv, _Direction.xy * _BlitTexture_TexelSize.xy * radius);
        }
        float4 CocBlur(Varyings input) : SV_Target
        { return BinomialBlur(input.texcoord, _Direction.xy * _BlitTexture_TexelSize.xy); }
        float3 Tone(float3 color) { return lerp(color, color * color * (3 - 2 * color), saturate(_SCurve)); }
        float4 Composite(Varyings input) : SV_Target
        {
            float4 color = ColorAt(input.texcoord);
            color.rgb = Tone(lerp(color.rgb, SAMPLE_TEXTURE2D_X(_FlatsAuxTex, sampler_LinearClamp, input.texcoord).rgb, CocAt(input.texcoord)));
            return color;
        }
        float4 ToneOnly(Varyings input) : SV_Target
        { float4 color = ColorAt(input.texcoord); color.rgb = Tone(color.rgb); return color; }
        float SobelDepth(float2 uv, bool thin)
        {
            float2 t = abs(_BlitTexture_TexelSize.xy) * _Edge.y;
            float center = Depth01(uv);
            float4 axis = float4(Depth01(uv + float2(0,t.y)), Depth01(uv - float2(t.x,0)), Depth01(uv + float2(t.x,0)), Depth01(uv - float2(0,t.y)));
            float4 diagonal = float4(Depth01(uv+t), Depth01(uv+t*float2(-1,1)), Depth01(uv+t*float2(1,-1)), Depth01(uv-t));
            if(thin) { axis=max(axis,center); diagonal=max(diagonal,center); }
            axis /= max(center,1e-7); diagonal -= center;
            float x=dot(axis,float4(1,0,0,-1))+dot(diagonal,float4(1,1,-1,-1));
            float y=dot(axis,float4(0,1,-1,0))+dot(diagonal,float4(-1,1,-1,1));
            return pow(saturate(length(float2(x,y))),_Edge.z);
        }
        float2 LegacyNormal(float2 uv)
        {
            // Built-in depth normals use stereographic view-space encoding;
            // URP stores world normals, so convert before the original comparison.
            float3 normal = mul((float3x3)UNITY_MATRIX_V, SampleSceneNormals(saturate(uv)));
            return normal.xy / (normal.z + 1.0) / 1.7777 * .5 + .5;
        }
        float4 Outline(Varyings input) : SV_Target
        {
            float2 uv=input.texcoord, t=_BlitTexture_TexelSize.xy*_Edge.y;
            float edge;
            if (_Edge.x < 1.5)
            {
                float2 a=uv-t,b=uv+t,c=uv+t*float2(-1,1),d=uv+t*float2(1,-1);
                float n=(length(LegacyNormal(a)-LegacyNormal(b))+length(LegacyNormal(c)-LegacyNormal(d)))*_Sensitivity.x;
                float z=(abs(Depth01(a)-Depth01(b))+abs(Depth01(c)-Depth01(d)))*_Sensitivity.y*100;
                edge=step(.1,n+z);
            }
            else if (_Edge.x < 3.5) edge=SobelDepth(uv,_Edge.x>2.5);
            else edge=step(_Edge.w,abs(dot(ColorAt(uv-t).rgb-ColorAt(uv+t).rgb,float3(.299,.587,.114))));
            float4 color=lerp(ColorAt(uv),_EdgeBackground,_Sensitivity.z); color.rgb*=1-saturate(edge);return color;
        }
        float ObjectIdAt(float2 uv) { return floor(SAMPLE_TEXTURE2D_X_LOD(_FlatsMotionIds,sampler_PointClamp,saturate(uv),0).r*255+.5); }
        float2 MotionRaw(float2 uv)
        {
            if(ObjectIdAt(uv)>254)return float2(0,0);
            // URP vectors are screen UV deltas; Amplify's recovered vectors are
            // NDC deltas, hence 2. Thresholds and radius retain original units.
            float2 velocityNdc=SAMPLE_TEXTURE2D_X(_MotionVectorTexture,sampler_PointClamp,uv).xy*2;
            if(abs(_MotionOptions.w-1)>.00001 && ObjectIdAt(uv)<2)
            {
                float deviceDepth=SampleSceneDepth(uv);
                #if !UNITY_REVERSED_Z
                    deviceDepth=lerp(UNITY_NEAR_CLIP_VALUE,1,deviceDepth);
                #endif
                float4 world=float4(ComputeWorldSpacePosition(uv,deviceDepth,UNITY_MATRIX_I_VP),1);
                float4 previous=mul(_PrevViewProjMatrix,world),current=mul(_NonJitteredViewProjMatrix,world);
                float2 cameraVelocity=current.xy/max(abs(current.w),1e-7)-previous.xy/max(abs(previous.w),1e-7);
                #if UNITY_UV_STARTS_AT_TOP
                    cameraVelocity.y=-cameraVelocity.y;
                #endif
                velocityNdc+=cameraVelocity*(_MotionOptions.w-1);
            }
            return velocityNdc*_Motion.x;
        }
        float4 MotionBlur(Varyings input) : SV_Target
        {
            float2 uv=input.texcoord;
            float2 raw=MotionRaw(uv);
            float speed=length(raw);
            float amount=max(min(speed,_Motion.z)-_Motion.y,0)/max(_Motion.z-_Motion.y,1e-7);
            float2 velocity=raw/max(speed,1e-7)*amount*_Motion.z*.001*_MotionOptions.x;
            if(_MotionOptions.z>.5)return float4(raw/max(speed,1e-7)*.5+.5,amount,1);
            float4 center=ColorPointAt(uv); if(length(velocity)<1e-7)return center;
            float depth=DepthEye(uv);float objectId=ObjectIdAt(uv);float3 sum=center.rgb;float count=1;
            int taps=(int)_MotionOptions.y;
            [loop] for(int k=-taps;k<=taps;k++)
            {
                if(k==0)continue;
                float2 pos=uv+velocity*(float(k)/taps);
                float weight=(DepthEye(pos)>depth-_Motion.w || (objectId>1 && objectId<254 && ObjectIdAt(pos)==objectId))?1:0;
                sum+=ColorPointAt(pos).rgb*weight;count+=weight;
            }
            return float4(sum/count,center.a);
        }
        float4 MotionComposite(Varyings input) : SV_Target
        {
            float speed=length(MotionRaw(input.texcoord));
            float amount=max(min(speed,_Motion.z)-_Motion.y,0)/max(_Motion.z-_Motion.y,1e-7);
            float4 original=ColorPointAt(input.texcoord);
            float3 blurred=SAMPLE_TEXTURE2D_X(_FlatsAuxTex,sampler_PointClamp,input.texcoord).rgb;
            return float4(lerp(original.rgb,blurred,saturate(amount*3)),original.a);
        }
        float4 Grayscale(Varyings input) : SV_Target
        {float4 c=ColorAt(input.texcoord);c.rgb=lerp(c.rgb,dot(c.rgb,_Gray.rgb).xxx,_Gray.a);return c;}
        float4 Fish(Varyings input) : SV_Target
        {float2 p=input.texcoord*2-1;return ColorAt(input.texcoord+p*dot(p,p)*_Fish.xy);}
        float Curve(float value,float row) {return SAMPLE_TEXTURE2D(_Curves,sampler_Curves,float2(value,(row+.5)/7)).r;}
        float4 Correction(Varyings input) : SV_Target
        {
            float4 c=ColorAt(input.texcoord);
            float3 corrected=float3(Curve(c.r,0),Curve(c.g,1),Curve(c.b,2));
            if(_Correction.y>.5)corrected=lerp(corrected,float3(Curve(c.r,3),Curve(c.g,4),Curve(c.b,5)),Curve(Depth01(input.texcoord),6));
            c.rgb=lerp(dot(corrected,float3(.299,.587,.114)).xxx,corrected,_Correction.x);
            if(_Correction.z>.5)c.rgb=lerp(c.rgb,_SelectiveTo.rgb,saturate(1-length(c.rgb-_SelectiveFrom.rgb)));
            return c;
        }
        float4 Fxaa(Varyings input) : SV_Target
        {
            float2 uv=input.texcoord,t=_BlitTexture_TexelSize.xy;float3 l=float3(.299,.587,.114);float4 c=ColorAt(uv);
            float nw=dot(ColorAt(uv+t*float2(-1,-1)).rgb,l),ne=dot(ColorAt(uv+t*float2(1,-1)).rgb,l);
            float sw=dot(ColorAt(uv+t*float2(-1,1)).rgb,l),se=dot(ColorAt(uv+t*float2(1,1)).rgb,l),m=dot(c.rgb,l);
            float lo=min(m,min(min(nw,ne),min(sw,se))),hi=max(m,max(max(nw,ne),max(sw,se)));
            float2 dir=float2(-((nw+ne)-(sw+se)),(nw+sw)-(ne+se));float reduce=max((nw+ne+sw+se)*.03125,.0078125);
            dir=clamp(dir/(min(abs(dir.x),abs(dir.y))+reduce),-8,8)*t;
            float3 a=.5*(ColorAt(uv-dir/6).rgb+ColorAt(uv+dir/6).rgb);
            float3 b=a*.5+.25*(ColorAt(uv-dir*.5).rgb+ColorAt(uv+dir*.5).rgb);
            float lb=dot(b,l);return float4(lb<lo||lb>hi?a:b,c.a);
        }
        float4 Downsample(Varyings input) : SV_Target
        {
            float2 uv=input.texcoord,t=_BlitTexture_TexelSize.xy;
            return (ColorAt(uv+t)+ColorAt(uv-t)+ColorAt(uv+t*float2(1,-1))+ColorAt(uv+t*float2(-1,1)))*.25;
        }
        float4 Copy(Varyings input) : SV_Target { return ColorAt(input.texcoord); }
        struct IdAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct IdVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
        IdVaryings IdVertex(IdAttributes input)
        {
            IdVaryings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
            output.uv=input.uv*_FlatsMotionAlphaST.xy+_FlatsMotionAlphaST.zw;
            return output;
        }
        float4 IdFragment(IdVaryings input) : SV_Target
        {
            clip(SAMPLE_TEXTURE2D(_FlatsMotionAlpha,sampler_FlatsMotionAlpha,input.uv).a-_FlatsMotionCutoff);
            return float4(_FlatsMotionId,0,0,1);
        }
        ENDHLSL
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CircleOfConfusion
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthBlur
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Outline
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment MotionBlur
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Grayscale
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fish
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Correction
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fxaa
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Copy
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment CocBlur
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment ToneOnly
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Downsample
            ENDHLSL
        }
        Pass {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment MotionComposite
            ENDHLSL
        }
        Pass {
            Name "MotionObjectIdentity"
            Cull Back ZWrite Off ZTest Equal
            HLSLPROGRAM
            #pragma vertex IdVertex
            #pragma fragment IdFragment
            ENDHLSL
        }
    }
}
