Shader "DeepPressure/FeedbackParticle"
{
 Properties{_MainTex("Particle",2D)="white"{} [Enum(UnityEngine.Rendering.BlendMode)]_DstBlend("Destination",Float)=1}
 SubShader{
 Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
 Cull Off ZWrite Off Blend SrcAlpha [_DstBlend]
 Pass{
 Tags{"LightMode"="Universal2D"}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
 V Vert(A i){V o;o.p=TransformObjectToHClip(i.p.xyz);o.c=i.c;o.uv=i.uv;return o;}
 half4 Frag(V i):SV_Target{return i.c*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);}
 ENDHLSL
 }
 }
}
