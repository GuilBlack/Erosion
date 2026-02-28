void HeightToNormal_float(UnityTexture2D heightMap, float2 uv, float2 elevationRange, float textureDim, float terrainSize, out float3 normalTS)
{
    float strength = (elevationRange.y - elevationRange.x) / (terrainSize / textureDim);

    float heightL = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(-1.0 / textureDim, 0), 0).r;
    float heightR = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(1.0 / textureDim, 0), 0).r;

    float heightU = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0, 1.0 / textureDim), 0).r;
    float heightD = heightMap.SampleLevel(heightMap.samplerstate, uv + float2(0, -1.0 / textureDim), 0).r;
    float dHx = (heightR - heightL);
    float dHy = (heightU - heightD);

    normalTS = normalize(float3(-dHx * strength, -dHy * strength, 1.0));
}

void SampleHeight_float(UnityTexture2D heightMap, float2 uv, float2 elevationRange, out float height)
{
    height = heightMap.SampleLevel(heightMap.samplerstate, uv, 0).r;
    height = height * (elevationRange.y - elevationRange.x) + elevationRange.x;
}

void NormalTSToObject_float(float3 normalTS, float3 tangent, float3 bitangent, float3 normal, out float3 normalOS)
{
    float3x3 TBN = float3x3(tangent, bitangent, normal);
    normalOS = mul(TBN, normalTS);
}
