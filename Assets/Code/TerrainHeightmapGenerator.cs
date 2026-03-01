using Assets;
using System.Runtime.InteropServices;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class TerrainHeightmapGenerator : MonoBehaviour
{
    [Header("Shaders")]
    [SerializeField] private Material       _terrainPreviewMaterial;
    [SerializeField] private ComputeShader  _heightmapCompute;
    [SerializeField] private ComputeShader  _blurCompute;

    [Header("Heightmap Generation")]
    [SerializeField] private RenderTexture  _heightmapTexture;
    [SerializeField] private FBMParamsSO    _heightmapGenerationParams;
    [SerializeField] private bool           _blurResult;

    [ReadOnly] private RenderTexture _heightmapTemp;

    public void ComputeHeightmap()
    {
        if (_heightmapCompute == null || _heightmapTexture == null || _blurCompute == null)
        {
            Debug.LogError("A compute shader or the heightmap texture is not assigned.");
            return;
        }
        if (_heightmapTexture.width != _heightmapTexture.height)
        {
            Debug.LogError("Heightmap texture must be square.");
            return;
        }
        int mapSize = _heightmapTexture.width; // Assuming square texture

        if (_heightmapTemp == null || _heightmapTemp.width != mapSize)
        {
            if (_heightmapTemp != null)
                _heightmapTemp.Release();
            _heightmapTemp = new RenderTexture(mapSize, mapSize, 0, RenderTextureFormat.RGFloat);
            _heightmapTemp.enableRandomWrite = true;
            _heightmapTemp.Create();
        }

        int kernelHandle = GetKernelHandle();

        ComputeBuffer cbFBMParams = new ComputeBuffer(1, Marshal.SizeOf(typeof(FBMParams)));
        FBMParams fbmParams = new FBMParams
        {
            Octaves =           _heightmapGenerationParams.Octaves,
            Persistence =       _heightmapGenerationParams.Persistence,
            Lacunarity =        _heightmapGenerationParams.Lacunarity,
            Exponentiation =    _heightmapGenerationParams.Exponentiation,
            Amplitude =         _heightmapGenerationParams.Amplitude,
            Frequency =         _heightmapGenerationParams.Frequency,
            Seed =              _heightmapGenerationParams.Seed,
            Scale =             _heightmapGenerationParams.Scale
        };
        cbFBMParams.SetData(new FBMParams[] { fbmParams });
        _heightmapCompute.SetBuffer(kernelHandle, "FBMParams", cbFBMParams);
        
        ComputeBuffer cbMinMaxInt = new ComputeBuffer(2, sizeof(int));
        cbMinMaxInt.SetData(new int[] { int.MaxValue, int.MinValue });
        _heightmapCompute.SetBuffer(kernelHandle, "GlobalMinMax", cbMinMaxInt);
        _heightmapCompute.SetTexture(kernelHandle, "Result", _heightmapTemp);

        _heightmapCompute.SetFloat("MapSize", mapSize);
        _heightmapCompute.SetBool("IsRigedFBM", _heightmapGenerationParams.IsRidgedFBM);
        _heightmapCompute.SetBool("IsCombinedFBM", _heightmapGenerationParams.IsCombinedFBM);
        _heightmapCompute.SetInt("FloatToIntScalar", 100000000);

        _heightmapCompute.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        kernelHandle = _heightmapCompute.FindKernel("RemapMain");
        _heightmapCompute.SetBuffer(kernelHandle, "FBMParams", cbFBMParams);
        _heightmapCompute.SetBuffer(kernelHandle, "GlobalMinMax", cbMinMaxInt);
        _heightmapCompute.SetTexture(kernelHandle, "Result", _heightmapTemp);
        _heightmapCompute.Dispatch(kernelHandle, mapSize / 8, mapSize / 8, 1);

        int[] minMaxInt = new int[2];
        cbMinMaxInt.GetData(minMaxInt);

        Debug.Log($"Heightmap Min: {minMaxInt[0] / 100000000f}, Max: {minMaxInt[1] / 100000000f}");

        cbFBMParams.Release();
        cbMinMaxInt.Release();

        if (_blurResult)
        {
            kernelHandle = _blurCompute.FindKernel("BlurMain");
            _blurCompute.SetTexture(kernelHandle, "SrcTexture", _heightmapTemp);
            _blurCompute.SetTexture(kernelHandle, "Result", _heightmapTexture);
            _blurCompute.SetInts("TextureSize", new int[] { mapSize, mapSize });
            _blurCompute.Dispatch(kernelHandle, mapSize / 4, mapSize / 4, 1);
        }
        else
            Graphics.Blit(_heightmapTemp, _heightmapTexture);

        if (_terrainPreviewMaterial)
            _terrainPreviewMaterial.SetTexture("HeightMap", _heightmapTexture);
    }

    private int GetKernelHandle()
    {
        if (_heightmapGenerationParams.IsCombinedFBM)
            return _heightmapCompute.FindKernel("CombinedFBMMain");

        if (_heightmapGenerationParams.IsRidgedFBM)
            return _heightmapCompute.FindKernel("RigedFBMMain");
        return _heightmapCompute.FindKernel("FBMMain");
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(TerrainHeightmapGenerator))]
class TerrainHeightmapGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        TerrainHeightmapGenerator generator = (TerrainHeightmapGenerator)target;
        if (GUILayout.Button("Compute Heightmap"))
            generator.ComputeHeightmap();
    }
}
#endif
