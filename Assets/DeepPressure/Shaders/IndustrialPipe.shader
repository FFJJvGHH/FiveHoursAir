Shader "DeepPressure/IndustrialPipe"
{
    Properties
    {
        _PipeTint("Brushed Metal",Color)=(0.42,0.51,0.53,1)
        _GasColor("Gas Identification Stripe",Color)=(0.25,0.85,0.78,1)
        _FlowPhase("Flow Distance",Float)=0
        _FlowStrength("Actual Flow",Range(0,1))=0
        _ValveClosed("Closed Valve",Float)=0
    }
    SubShader
    {
        Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags{"LightMode"="Universal2D"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_1 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_2 __
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_3 __
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #if USE_SHAPE_LIGHT_TYPE_0
            SHAPE_LIGHT(0)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_1
            SHAPE_LIGHT(1)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_2
            SHAPE_LIGHT(2)
            #endif
            #if USE_SHAPE_LIGHT_TYPE_3
            SHAPE_LIGHT(3)
            #endif
            half _HDREmulationScale;
            half _UseSceneLighting;
            struct Attributes{float3 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;float4 color:COLOR;};
            struct Varyings{float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;float4 color:COLOR;float2 lightingUV:TEXCOORD2;};
            CBUFFER_START(UnityPerMaterial)
            float4 _PipeTint,_GasColor;
            float _FlowPhase,_FlowStrength,_ValveClosed;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);output.uv=input.uv;output.color=input.color;output.lightingUV=ComputeScreenPos(output.positionCS/output.positionCS.w).xy;return output;}
            half4 Frag(Varyings input):SV_Target
            {
                float3 normal=normalize(input.normalWS);
                float3 lightDirection=normalize(float3(-.45,.78,-.92));
                float facing=saturate(-normal.z);
                float light=.28+.82*saturate(dot(normal,lightDirection));
                float specular=pow(saturate(dot(reflect(-lightDirection,normal),float3(0,0,-1))),18)*.9;
                float fineBrushing=1+.027*sin(input.uv.x*165+input.uv.y*19);
                float3 metal=_PipeTint.rgb*input.color.rgb*light*fineBrushing+specular*input.color.rgb;
                metal*=lerp(.34,1,smoothstep(0,.32,facing));
                float stripe=1-smoothstep(.021,.039,abs(input.uv.y-.25));
                float segment=frac((input.uv.x-_FlowPhase)/2.15);
                float pulse=pow(saturate(1-abs(segment-.5)*13),2);
                float3 identification=lerp(_GasColor.rgb,float3(.40,.24,.14),_ValveClosed*.6);
                float3 color=lerp(metal,identification*(.56+.27*facing),stripe*.86);
                // URP 2D supplies the same screen-space light buffers used by Sprite-Lit.
                // The tiny ambient floor retains readable metal in unlit maintenance areas.
                float3 modulation=0,additive=0;
                #if USE_SHAPE_LIGHT_TYPE_0
                half3 shape0=SAMPLE_TEXTURE2D(_ShapeLightTexture0,sampler_ShapeLightTexture0,input.lightingUV).rgb;
                modulation+=shape0*_ShapeLightBlendFactors0.x;additive+=shape0*_ShapeLightBlendFactors0.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_1
                half3 shape1=SAMPLE_TEXTURE2D(_ShapeLightTexture1,sampler_ShapeLightTexture1,input.lightingUV).rgb;
                modulation+=shape1*_ShapeLightBlendFactors1.x;additive+=shape1*_ShapeLightBlendFactors1.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_2
                half3 shape2=SAMPLE_TEXTURE2D(_ShapeLightTexture2,sampler_ShapeLightTexture2,input.lightingUV).rgb;
                modulation+=shape2*_ShapeLightBlendFactors2.x;additive+=shape2*_ShapeLightBlendFactors2.y;
                #endif
                #if USE_SHAPE_LIGHT_TYPE_3
                half3 shape3=SAMPLE_TEXTURE2D(_ShapeLightTexture3,sampler_ShapeLightTexture3,input.lightingUV).rgb;
                modulation+=shape3*_ShapeLightBlendFactors3.x;additive+=shape3*_ShapeLightBlendFactors3.y;
                #endif
                float3 illuminated=max(color*.20,_HDREmulationScale*(color*modulation+additive));
                color=lerp(color,illuminated,_UseSceneLighting);
                color+=stripe*pulse*_FlowStrength*(_GasColor.rgb*1.8+.65);
                return half4(color,1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags{"LightMode"="NormalsRendering"}
            HLSLPROGRAM
            #pragma vertex NormalVertex
            #pragma fragment NormalFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes{float3 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct Varyings{float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;};
            Varyings NormalVertex(Attributes input)
            {Varyings output;output.positionCS=TransformObjectToHClip(input.positionOS);output.normalWS=TransformObjectToWorldNormal(input.normalOS);return output;}
            half4 NormalFragment(Varyings input):SV_Target
            {return half4(normalize(input.normalWS)*.5+.5,1);}
            ENDHLSL
        }
    }
}
