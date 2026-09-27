Shader "DeepPressure/FogOfWar"
{
 Properties {
  [PerRendererData] _MainTex("Sprite",2D)="white"{}
  _VisibilityTex("Knowledge",2D)="black"{}
  _RevealTex("Reveal transition",2D)="black"{}
  [HideInInspector] _DiscoveryActive("Active",Float)=0
  [HideInInspector] _CellSize("Cell size",Float)=1
  [HideInInspector] _GridSize("Grid",Vector)=(64,36,0,0)
 }
 SubShader {
  Tags {"Queue"="Transparent+100" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}
  Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   Tags {"LightMode"="Universal2D"}
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_VisibilityTex);SAMPLER(sampler_VisibilityTex);
   TEXTURE2D(_RevealTex);SAMPLER(sampler_RevealTex);
   float4 _GridSize,_ScanOrigin;float _CellSize,_DiscoveryActive,_ScanStart;float4x4 _WorldToMap;
   struct A {float4 vertex:POSITION;};struct V {float4 vertex:SV_POSITION;float2 p:TEXCOORD0;};
   V Vert(A i){V o;float3 w=TransformObjectToWorld(i.vertex.xyz);o.vertex=TransformWorldToHClip(w);o.p=mul(_WorldToMap,float4(w,1)).xy/max(.001,_CellSize);return o;}
   float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
   float fbm(float2 p){float a=.5,s=0;for(int k=0;k<4;k++){s+=a*noise(p);p=mul(float2x2(1.7,-1.2,1.2,1.7),p)+17.2;a*=.5;}return s;}
   float vis(float2 p){return SAMPLE_TEXTURE2D(_VisibilityTex,sampler_VisibilityTex,(floor(p)+.5)/_GridSize.xy).r;}
   half4 Frag(V i):SV_Target {
    float2 p=i.p;float t=_Time.y;float known=step(.5,vis(p));
    float r=SAMPLE_TEXTURE2D(_RevealTex,sampler_RevealTex,p/_GridSize.xy).r;
    float2 warp=float2(fbm(p*.12+float2(t*.006,0)),fbm(p*.12+float2(12,t*-.009)));
    float broad=fbm(p*.27+warp*2.2+float2(-t*.012,t*.008));float smoke=fbm(p*.73+warp*3-float2(t*.018,0));
    float stratum=sin(p.y*1.7+fbm(p*.2)*4+p.x*.085)*.5+.5;
    float3 col=lerp(float3(.006,.012,.021),float3(.023,.039,.051),broad*.7+smoke*.3)*(.86+.14*stratum);
    float distance=4;
    // Distance to the unknown tile's rectangle, not its centre. At the shared
    // border it is exactly zero on both sides, so opacity has no tile-sized jump.
    for(int y=-2;y<=2;y++)for(int x=-2;x<=2;x++) {
     float2 n=floor(p)+float2(x,y)+.5;
     if(vis(n)<.5)distance=min(distance,length(max(abs(p-n)-.5,0)));
    }
    float width=1.05+smoke*.55;
    float fringe=1-smoothstep(0,width,distance);
    float alpha=known?max(1-smoothstep(0,1,r),fringe):1;
    // Independent occluding atmosphere. Unknown map pixels never show through.
    col+=float3(.015,.027,.026)*fringe*smoke;
    float elapsed=max(0,t-_ScanStart);float ring=exp(-abs(length(p-_ScanOrigin.xy)-elapsed*8)*4)*exp(-elapsed*.7)*step(.001,_ScanStart);
    col+=ring*float3(.07,.19,.16);return half4(col,saturate(alpha)*_DiscoveryActive);
   }
   ENDHLSL
  }
 }
}
