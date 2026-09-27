Shader "DeepPressure/GasAtmosphere"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _GasTex("O2 N2 CO2 H2O",2D)="black"{}
  _PressureTex("Pressure void height",2D)="black"{}
  _VisibilityTex("Discovery",2D)="black"{}
  _Opacity("Density",Range(0,1))=.52
  _CellSize("Cell size",Float)=1
  _GridSize("Grid",Vector)=(64,36,0,0)
 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags {"LightMode"="Universal2D"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
   #if USE_SHAPE_LIGHT_TYPE_0
   SHAPE_LIGHT(0)
   #endif
   TEXTURE2D(_GasTex);SAMPLER(sampler_GasTex);TEXTURE2D(_PressureTex);SAMPLER(sampler_PressureTex);
   TEXTURE2D(_VisibilityTex);SAMPLER(sampler_VisibilityTex);
   float4 _GridSize;float _CellSize,_Opacity;float4x4 _WorldToMap;
   struct A {float4 vertex:POSITION;};struct V {float4 vertex:SV_POSITION;float2 p:TEXCOORD0;float2 luv:TEXCOORD1;};
   V Vert(A i){V o;float3 w=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(w);o.p=mul(_WorldToMap,float4(w,1)).xy/max(.001,_CellSize);o.luv=ComputeScreenPos(o.vertex/o.vertex.w).xy;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){float s=0,a=.5;for(int k=0;k<4;k++){s+=a*noise(p);p=mul(float2x2(1.7,-1.2,1.2,1.7),p)+19;a*=.5;}return s;}
   half4 Frag(V i):SV_Target {
    float2 uv=(floor(i.p)+.5)/_GridSize.xy;float4 mix=SAMPLE_TEXTURE2D(_GasTex,sampler_GasTex,uv);float3 room=SAMPLE_TEXTURE2D(_PressureTex,sampler_PressureTex,uv).rgb;
    float visible=SAMPLE_TEXTURE2D(_VisibilityTex,sampler_VisibilityTex,uv).r;if(room.g<.5||visible<.5)discard;
    float2 p=i.p;float t=_Time.y;
    float2 curl=float2(fbm(p*.43+float2(t*.034,0)),fbm(p*.43+float2(3,-t*.027)))-.5;
    float diffuse=fbm(p*.9+curl*2.4-float2(t*.037,0));
    float vapor=pow(saturate(1-abs(fbm(float2(p.x*.42,p.y*1.25)+curl*2-float2(0,t*.14))-.48)*3.2),3);
    float heavy=fbm(p*.67+curl*3+float2(t*.016,t*.011))*(1-smoothstep(.07,.75,room.b));
    float thin=smoothstep(.35,.81,diffuse)*(.35+mix.x);
    float body=thin*.22+vapor*mix.w*2.5+heavy*mix.z*2.8;
    float3 tint=(float3(.24,.51,.59)*mix.x+float3(.19,.25,.36)*mix.y+float3(.57,.45,.35)*mix.z+float3(.65,.72,.80)*mix.w)/max(.1,dot(mix,1));
    float3 illumination=float3(.32,.36,.42);
    #if USE_SHAPE_LIGHT_TYPE_0
    illumination=max(float3(.18,.2,.24),SAMPLE_TEXTURE2D(_ShapeLightTexture0,sampler_ShapeLightTexture0,i.luv).rgb);
    #endif
    float scatter=pow(saturate(max(illumination.r,max(illumination.g,illumination.b))*.8),.6);
    float alpha=_Opacity*body*(.35+sqrt(room.r)*1.3)*(.5+scatter*.6);
    return half4(tint*(.3+illumination*.8)+vapor*mix.w*.07,saturate(alpha));
   }
   ENDHLSL
  }
 }
}
