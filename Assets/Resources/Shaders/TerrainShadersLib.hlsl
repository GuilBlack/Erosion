void HeightToNormal_float(UnityTexture2D heightMap, float2 uv, float textureDim, float terrainSize, float texelX, float texelY, out float3 normal)
{
    float texel = 1.0 / textureDim;
    float mPerTexels = terrainSize / textureDim;

    float hL = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(-texel, 0), 0).r;
    float hR = heightMap.SampleLevel(heightMap.samplerstate, uv + float2( texel, 0), 0).r;
    float hD = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0, -texel), 0).r;
    float hU = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0,  texel), 0).r;

    float dhdx = (hR - hL) / (2.0 * mPerTexels);
    float dhdz = (hU - hD) / (2.0 * mPerTexels);

    normal = normalize(float3(-dhdx, 1.0, -dhdz));
}

void SampleHeight_float(UnityTexture2D heightMap, float2 uv, out float height)
{
    height = heightMap.SampleLevel(heightMap.samplerstate, uv, 0).r;
    height = height;
}

void NormalTSToObject_float(float3 normalTS, float3 tangent, float3 bitangent, float3 normal, out float3 normalOS)
{
    float3x3 TBN = float3x3(tangent, bitangent, normal);
    normalOS = normalize(mul(normalTS, TBN));
}

void SampleWaterHeight_float(UnityTexture2D terrainWaterHeightmap, float2 uv, out float height)
{
    float2 terrainHeight = terrainWaterHeightmap.SampleLevel(terrainWaterHeightmap.samplerstate, uv, 0).xy;
    height = terrainHeight.x + terrainHeight.y; // add terrain and water height
}

void WaterHeightToNormal_float(UnityTexture2D heightMap, float2 uv, float textureDim, float terrainSize, out float3 normal)
{
    float texel = 1.0 / textureDim;
    float mPerTexels = terrainSize / textureDim;
    
    float2 temp = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(-texel, 0), 0).rg;
    float hL = temp.x + temp.y;
    temp = heightMap.SampleLevel(heightMap.samplerstate, uv + float2( texel, 0), 0).rg;
    float hR = temp.x + temp.y;
    temp = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0, -texel), 0).rg;
    float hD = temp.x + temp.y;
    temp = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0,  texel), 0).rg;
    float hU = temp.x + temp.y;

    float dHx = (hR - hL) / (2.0 * mPerTexels);
    float dHy = (hU - hD) / (2.0 * mPerTexels);

    normal = normalize(float3(-dHx, 1.0, -dHy));
}