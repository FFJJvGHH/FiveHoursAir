#ifndef DEEP_TERRAIN_BLEND_INCLUDED
#define DEEP_TERRAIN_BLEND_INCLUDED
TEXTURE2D(_TerrainPaletteTex); SAMPLER(sampler_TerrainPaletteTex);
TEXTURE2D(_TerrainSourcePaletteTex); SAMPLER(sampler_TerrainSourcePaletteTex);
float _TerrainBlendEnabled;
float4 _TerrainMapSize;
float4x4 _TerrainWorldToLocal;

half3 DeepTerrainColor(half3 color, float3 positionWS)
{
    if (_TerrainBlendEnabled < .5) return color;
    float2 p = mul(_TerrainWorldToLocal,float4(positionWS,1)).xy / max(_TerrainMapSize.z,.001);
    half4 original = SAMPLE_TEXTURE2D(_TerrainSourcePaletteTex,sampler_TerrainSourcePaletteTex,(floor(p)+.5)/_TerrainMapSize.xy);
    if (original.a < .5) return color; // Manufactured metal and air keep their authored boundaries.
    float2 warp = float2(sin(p.y*2.71+p.x*.37)+.42*sin(p.y*7.83+p.x*.91),
        sin(p.x*2.31+p.y*.43)+.42*sin(p.x*8.37+p.y*.79)) * .17;
    float2 sampleCell=p+warp-.5, baseCell=floor(sampleCell);
    float2 blend=smoothstep(.37,.63,frac(sampleCell));
    half4 a=SAMPLE_TEXTURE2D(_TerrainPaletteTex,sampler_TerrainPaletteTex,(baseCell+float2(.5,.5))/_TerrainMapSize.xy);
    half4 b=SAMPLE_TEXTURE2D(_TerrainPaletteTex,sampler_TerrainPaletteTex,(baseCell+float2(1.5,.5))/_TerrainMapSize.xy);
    half4 c=SAMPLE_TEXTURE2D(_TerrainPaletteTex,sampler_TerrainPaletteTex,(baseCell+float2(.5,1.5))/_TerrainMapSize.xy);
    half4 d=SAMPLE_TEXTURE2D(_TerrainPaletteTex,sampler_TerrainPaletteTex,(baseCell+float2(1.5,1.5))/_TerrainMapSize.xy);
    half4 weights=half4((1-blend.x)*(1-blend.y),blend.x*(1-blend.y),(1-blend.x)*blend.y,blend.x*blend.y)*half4(a.a,b.a,c.a,d.a);
    half total=dot(weights,half4(1,1,1,1));
    half3 target=(a.rgb*weights.x+b.rgb*weights.y+c.rgb*weights.z+d.rgb*weights.w)/max(total,.001);
    return total>.001 ? color*target/max(original.rgb,half3(.02,.02,.02)) : color;
}
#endif
