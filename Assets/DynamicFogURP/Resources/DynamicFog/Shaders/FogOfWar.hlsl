#ifndef DYNAMIC_FOG_2_FOW
#define DYNAMIC_FOG_2_FOW

#if !defined(SHADER_API_PS5) && !defined(SHADER_API_PS4)
CBUFFER_START(DynamicFog2FogOfWarBuffers)
#endif
    sampler2D _FogOfWarTex;
    float3 _FogOfWarCenter;
    float3 _FogOfWarSize;
    float3 _FogOfWarCenterAdjusted;
#if !defined(SHADER_API_PS5) && !defined(SHADER_API_PS4)
CBUFFER_END
#endif


half4 ApplyFogOfWar(float3 wpos) {
    float2 fogTexCoord = wpos.xz / _FogOfWarSize.xz - _FogOfWarCenterAdjusted.xz;
    half4 fowColor = tex2Dlod(_FogOfWarTex, float4(fogTexCoord, 0, 0));
    return half4(fowColor.rgb, fowColor.a);
}

#endif // DYNAMIC_FOG_2_FOW