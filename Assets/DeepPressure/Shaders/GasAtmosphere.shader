Shader "DeepPressure/GasAtmosphere"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _GasTex("O2 N2 CO2 H2O",2D)="black"{}
  _ReactiveTex("Methane / process vapour",2D)="black"{}
  _PressureTex("Pressure void height",2D)="black"{}
  _VisibilityTex("Discovery",2D)="black"{}
  _Opacity("Density",Range(0,1))=.52
  _CellSize("Cell size",Float)=1
  _GridSize("Grid",Vector)=(64,36,0,0)
  [HideInInspector] _PressureScaleKPa("Pressure encoding",Float)=1000
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
   TEXTURE2D(_ReactiveTex);SAMPLER(sampler_ReactiveTex);
   float4 _GridSize;float _CellSize,_Opacity,_PressureScaleKPa;float4x4 _WorldToMap;
   struct A {float4 vertex:POSITION;};struct V {float4 vertex:SV_POSITION;float2 p:TEXCOORD0;float2 luv:TEXCOORD1;};
   V Vert(A i){V o;float3 w=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(w);o.p=mul(_WorldToMap,float4(w,1)).xy/max(.001,_CellSize);o.luv=ComputeScreenPos(o.vertex/o.vertex.w).xy;return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){float s=0,a=.5;for(int k=0;k<4;k++){s+=a*noise(p);p=mul(float2x2(1.7,-1.2,1.2,1.7),p)+19;a*=.5;}return s;}
   half4 Frag(V i):SV_Target {
    float2 uv=(floor(i.p)+.5)/_GridSize.xy,flowUv=i.p/_GridSize.xy;
    float4 mix=SAMPLE_TEXTURE2D(_GasTex,sampler_GasTex,flowUv);float4 room=SAMPLE_TEXTURE2D(_PressureTex,sampler_PressureTex,flowUv);
    float cellVoid=SAMPLE_TEXTURE2D(_PressureTex,sampler_PressureTex,uv).g;
    float2 reactive=SAMPLE_TEXTURE2D(_ReactiveTex,sampler_ReactiveTex,flowUv).rg;
    float visible=SAMPLE_TEXTURE2D(_VisibilityTex,sampler_VisibilityTex,uv).r;if(cellVoid<.5||visible<.5)discard;
    float2 p=i.p;float heat=saturate((room.a-.32)*1.8);float t=_Time.y*(1+heat*.7);
    // Per-cell pressure and six finite species come from the diffusion field.
    // Fine eddies decorate the measured field without changing its concentration.
    float pressure=room.r*max(1,_PressureScaleKPa);
    float density=1-exp(-pressure/140);
    float2 largeFlow=float2(fbm(p*.17+float2(t*.022,0)),fbm(p*.19+float2(7,-t*.019)))-.5;
    float2 curl=float2(fbm(p*.47+largeFlow*2+float2(t*.041,0)),fbm(p*.41+largeFlow*2+float2(3,-t*.031)))-.5;
    float diffuse=fbm(p*.76+curl*2.2+largeFlow*3-float2(t*.045,0));
    float sheets=sin(p.y*1.65+p.x*.14+curl.y*3+largeFlow.x*5-t*.11)*.5+.5;
    float filament=smoothstep(.39,.72,fbm(float2(p.x*.38,p.y*1.38)+curl*2.8-float2(0,t*.09)));
    float localHeight=saturate(room.b+(frac(p.y)-.5)/8);
    float floorLayer=1-smoothstep(.03,.57,localHeight);
    float heavy=(.24+diffuse*.56+sheets*.2)*floorLayer;
    float vapor=filament*(.35+heat*.65);
    float rising=smoothstep(.3,.76,fbm(float2(p.x*.72,p.y*.42)+curl*2-float2(0,t*.14)));
    float reactiveBody=reactive.x*(.52+rising*.82)+reactive.y*(.7+filament*.95);
    float body=.13+diffuse*.2+sheets*.055+vapor*mix.w*1.65+heavy*mix.z*1.85+reactiveBody;
    float3 tint=(float3(.24,.59,.64)*mix.x+float3(.21,.32,.40)*mix.y+float3(.63,.40,.24)*mix.z+float3(.66,.76,.82)*mix.w+float3(.72,.62,.22)*reactive.x+float3(.64,.34,.71)*reactive.y)/max(.1,dot(mix,float4(1,1,1,1))+reactive.x+reactive.y);
    tint=lerp(tint,tint+float3(.06,.015,-.015),heat*.35);
    float3 illumination=float3(.32,.36,.42);
    #if USE_SHAPE_LIGHT_TYPE_0
    illumination=max(float3(.18,.2,.24),SAMPLE_TEXTURE2D(_ShapeLightTexture0,sampler_ShapeLightTexture0,i.luv).rgb);
    #endif
    float scatter=pow(saturate(max(illumination.r,max(illumination.g,illumination.b))*.8),.6);
    float alpha=_Opacity*body*density*(.72+scatter*.55);
    float highlight=smoothstep(.68,.83,diffuse)*.08*density;
    return half4(tint*(.52+illumination*.62)+highlight+vapor*mix.w*.075,min(.58,saturate(alpha)));
   }
   ENDHLSL
  }
 }
}
