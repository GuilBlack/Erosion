using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    [Serializable]
    public struct FBMParams
    {
        public int Octaves;
        public float Persistence;
        public float Lacunarity;
        public float Exponentiation;
        public float Amplitude;
        public float Frequency;
        public float Seed;
        public float Scale;
    };

    [Serializable]
    public class ErosionParams
    {
        [Range(0, 100)]
        public float TimeScale = 1f;
        public float Gravity = 9.81f;
        public float RainRate = 0.02f;
        public float HeightScale = 500f;

        [Range(0, 1)]
        public float EvaporationRate = 0.015f;
        public float SoilSuspensionRate = 0.01f;
        public float SedimentDepositionRate = 0.3f;
        public float SedimentSofteningRate = 3f;
        public float SedimentCapacity = 0.1f;
        public float MaxErosionDepth = 1f;
        public float MinHardness = 0.1f;

        public float PipeLength = 1f;
        public float PipeCrossArea = 4f;
        public Vector2 CellSize = new Vector2(1f, 1f);
    };

    struct CellData
    {
        public float TerrainHeight;
        public float WaterHeight;
        public float Sediment;
        public float Hardness;
        public float4 WaterOutflowFlux;
        public float2 Velocity;

        public override string ToString()
        {
            return string.Format(
            "TerrainHeight: {0}, WaterHeight: {1}, Sediment: {2}, Hardness: {3},\n" +
            "WaterOutflowFlux: {4}, Velocity: {5}",
            TerrainHeight.ToString("F5"),
            WaterHeight.ToString("F5"),
            Sediment.ToString("F5"),
            Hardness.ToString("F5"), 
            WaterOutflowFlux.ToString("F5", CultureInfo.InvariantCulture.NumberFormat),
            Velocity.ToString("F5", CultureInfo.InvariantCulture.NumberFormat));
        }
    };

    [SerializeField] private TerrainData _terrainData;
    [SerializeField] private ComputeShader _heightmapCompute;
    [SerializeField] private ComputeShader _erosionCompute;
    [SerializeField] private int _mapSize = 1024;
    [SerializeField] private Texture2D _heightmapTexture;
    [SerializeField] private FBMParams _fbmParams;
    [SerializeField] private bool _isRigedFBM = false;
    [SerializeField] private bool _isCombinedFBM = false;
    [SerializeField] private int _floatToIntScalar = 1000000000;
    private int[] _globalMinMaxInt = new int[2] { 0, int.MaxValue };
    [SerializeField] private CellData[] _cellData;
    [SerializeField] private ErosionParams _erosionParams;
    private float[,] _heightmap2D;

    public void ComputeHeightmap()
    {
        if (_heightmapCompute == null)
            return;

        RenderTexture heightmap = new RenderTexture(_mapSize, _mapSize, 24, RenderTextureFormat.RFloat);
        heightmap.enableRandomWrite = true;
        heightmap.Create();

        int kernelHandle = GetKernelHandle();

        ComputeBuffer cbFBMParams = new ComputeBuffer(1, Marshal.SizeOf(_fbmParams));
        cbFBMParams.SetData(new FBMParams[] { _fbmParams });
        _heightmapCompute.SetBuffer(kernelHandle, "FBMParams", cbFBMParams);

        ComputeBuffer cbMinMaxInt = new ComputeBuffer(2, sizeof(int));
        cbMinMaxInt.SetData(new int[] { int.MaxValue, int.MinValue });
        _heightmapCompute.SetBuffer(kernelHandle, "GlobalMinMaxInt", cbMinMaxInt);

        // I'm doing this with a texture for visualization purposes, but it's not necessary nor efficient
        _heightmapCompute.SetTexture(kernelHandle, "Result", heightmap);
        SetGlobalData(_heightmapCompute);
        
        _heightmapCompute.Dispatch(kernelHandle, _mapSize / 16, _mapSize / 16, 1);

        cbMinMaxInt.GetData(_globalMinMaxInt);
        
        kernelHandle = _heightmapCompute.FindKernel("RemapMain");

        _heightmapCompute.SetBuffer(kernelHandle, "FBMParams", cbFBMParams);
        _heightmapCompute.SetTexture(kernelHandle, "Result", heightmap);
        _heightmapCompute.SetBuffer(kernelHandle, "GlobalMinMaxInt", cbMinMaxInt);

        ComputeBuffer cbMinMaxFloat = new ComputeBuffer(2, sizeof(float));
        cbMinMaxFloat.SetData(new float[] { _globalMinMaxInt[0] / (float)_floatToIntScalar, _globalMinMaxInt[1] / (float)_floatToIntScalar });
        _heightmapCompute.SetBuffer(kernelHandle, "GlobalMinMaxFloat", cbMinMaxFloat);

        _heightmapCompute.Dispatch(kernelHandle, _mapSize / 16, _mapSize / 16, 1);

        cbMinMaxFloat.Release();
        cbMinMaxInt.Release();
        cbFBMParams.Release();

        _heightmapTexture = GetHeightmapTexture(heightmap);
    }

    void SetGlobalData(ComputeShader cs)
    {
        cs.SetFloat("MapSize", _mapSize);
        cs.SetInt("FloatToIntScalar", _floatToIntScalar);
        cs.SetBool("IsRigedFBM", _isRigedFBM);
        cs.SetBool("IsCombinedFBM", _isCombinedFBM);
    }

    private int GetKernelHandle()
    {
        if (_isCombinedFBM)
            return _heightmapCompute.FindKernel("CombinedFBMMain");
        
        if (_isRigedFBM)
            return _heightmapCompute.FindKernel("RigedFBMMain");
        return _heightmapCompute.FindKernel("FBMMain");
    }

    private Texture2D GetHeightmapTexture(RenderTexture heightmap)
    {
        Texture2D heightmapTexture = new Texture2D(_mapSize, _mapSize, TextureFormat.RFloat, false);
        RenderTexture.active = heightmap;
        heightmapTexture.ReadPixels(new Rect(0, 0, heightmap.width, heightmap.height), 0, 0);
        heightmapTexture.Apply();
        return heightmapTexture;
    }

    public void SetTerrainHeightmap()
    {
        if (_heightmap2D == null)
            InitBufferHeights();

        int processorCount = Environment.ProcessorCount;
        int chunkSize = _mapSize / processorCount;

        Parallel.For(0, processorCount, chunkIndex =>
        {
            int start = chunkIndex * chunkSize;
            int end = (chunkIndex == processorCount - 1) ? _mapSize : start + chunkSize; // Ensure the last chunk goes to the end

            for (int i = start; i < end; i++)
            {
                for (int j = 0; j < _mapSize; j++)
                {
                    _heightmap2D[i, j] = _cellData[i * _mapSize + j].TerrainHeight;
                }
            }
        });

        _terrainData.SetHeights(0, 0, _heightmap2D);
    }

    public void InitBufferHeights()
    {
        if (_heightmapTexture == null)
            ComputeHeightmap();

        int processorCount = Environment.ProcessorCount;
        int chunkSize = _mapSize / processorCount;
        _heightmap2D = new float[_mapSize, _mapSize];
        Color[] data = _heightmapTexture.GetPixels();
        _cellData = new CellData[_mapSize * _mapSize];

        Parallel.For(0, processorCount, chunkIndex =>
        {
            int start = chunkIndex * chunkSize;
            int end = (chunkIndex == processorCount - 1) ? _mapSize : start + chunkSize; // Ensure the last chunk goes to the end

            for (int i = start; i < end; i++)
            {
                for (int j = 0; j < _mapSize; j++)
                {
                    float height = data[i * _mapSize + j].r;
                    _heightmap2D[i, j] = height;
                    _cellData[i * _mapSize + j].Hardness = 1.0f;
                    _cellData[i * _mapSize + j].TerrainHeight = height;
                }
            }
        });

        CellData tempDebug = new CellData();
        for (int i = 0; i < 5; i++)
        {
            tempDebug = _cellData[5 * _mapSize + i];
            tempDebug.TerrainHeight *= _terrainData.heightmapScale.y;
            Debug.Log(tempDebug);
        }
    }

    public void Erode()
    {
        if (_heightmapTexture == null)
            ComputeHeightmap();

        List<int> kernels = new List<int>()
        {
            _erosionCompute.FindKernel("WaterIncrement"),
            _erosionCompute.FindKernel("OutflowFluxComputation"),
            _erosionCompute.FindKernel("VectorFieldAndWaterComputation"),
            _erosionCompute.FindKernel("ErosionComputation"),
            _erosionCompute.FindKernel("SedimentTransportationAndEvaporation"),
        };

        ComputeBuffer cbCellData = new ComputeBuffer(_cellData.Length, Marshal.SizeOf(typeof(CellData)));
        cbCellData.SetData(_cellData);

        foreach (var kernel in kernels)
        {
            _erosionCompute.SetBuffer(kernel, "Data", cbCellData);
        }

        #region SetConstants
        _erosionCompute.SetVector("CellSize", _erosionParams.CellSize);
        _erosionCompute.SetInt("MapSize", _mapSize);
        _erosionCompute.SetFloat("DeltaTime", 0.016f * _erosionParams.TimeScale);
        _erosionCompute.SetFloat("HeightScale", _erosionParams.HeightScale);
        _erosionCompute.SetFloat("Gravity", _erosionParams.TimeScale);
        _erosionCompute.SetFloat("RainRate", _erosionParams.RainRate);
        _erosionCompute.SetFloat("EvaporationRate", _erosionParams.EvaporationRate);
        _erosionCompute.SetFloat("SoilSuspensionRate", _erosionParams.SoilSuspensionRate);
        _erosionCompute.SetFloat("SedimentDepositionRate", _erosionParams.SedimentDepositionRate);
        _erosionCompute.SetFloat("PipeLength", _erosionParams.PipeLength);
        _erosionCompute.SetFloat("PipeCrossArea", _erosionParams.PipeCrossArea);
        _erosionCompute.SetFloat("SedimentCapacity", _erosionParams.SedimentCapacity);
        _erosionCompute.SetFloat("MaxErosionDepth", _erosionParams.MaxErosionDepth);
        _erosionCompute.SetFloat("MinHardness", _erosionParams.MinHardness);
        #endregion

        for (int i = 0; i < 1000; i++)
        {
            foreach (var kernel in kernels)
                _erosionCompute.Dispatch(kernel, _mapSize / 16, _mapSize / 16, 1);
        }

        cbCellData.GetData(_cellData);
        cbCellData.Release();

        CellData tempDebug = new CellData();
        for (int i = 0; i < 5; i++)
        {
            tempDebug = _cellData[5 * _mapSize + i];
            tempDebug.TerrainHeight *= _terrainData.heightmapScale.y;
            Debug.Log(tempDebug);
        }
    }

#if UNITY_EDITOR
    public void SaveHeightmapToAssets()
    {
        if (_heightmapTexture == null)
            return;

        string path = "Assets/Resources/Textures/Heightmap.asset";
        UnityEditor.AssetDatabase.CreateAsset(_heightmapTexture, path);
        UnityEditor.AssetDatabase.SaveAssets();
    }
#endif
}
