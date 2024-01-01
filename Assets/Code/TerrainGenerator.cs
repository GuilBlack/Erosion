using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(Terrain))]
public class TerrainGenerator : MonoBehaviour
{
    [Serializable]
    public struct FBMParameters
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
    public struct CellData
    {
        public float Height;
        public float WaterHeight;
        public float Sediment;
        public float Hardness;
        public Vector4 WaterOutflowFlux;
        public Vector2 Velocity;

        public override string ToString()
        {
            return Height.ToString() + '\n' +
                WaterHeight.ToString() + '\n' +
                Sediment.ToString() + '\n' +
                Hardness.ToString() + '\n' +
                WaterOutflowFlux.ToString("F7") + '\n' +
                Velocity.ToString("F7");
        }
    };

    [SerializeField] private ComputeShader _heightmapCompute;
    [SerializeField] private ComputeShader _erosionCompute;
    [SerializeField] private int _mapSize = 1024;
    [SerializeField] private Texture2D _heightmapTexture;
    [SerializeField] private FBMParameters _fbmParams;
    [SerializeField] private bool _isRigedFBM = false;
    [SerializeField] private bool _isCombinedFBM = false;
    [SerializeField] private int _floatToIntScalar = 1000000000;
    private int[] _globalMinMaxInt = new int[2] { 0, int.MaxValue };
    private float[,] _heightmap2D;
    private List<CellData[]> _cellDataBuffers = new List<CellData[]>(2) { new CellData[0], new CellData[0] };
    private int _currentBuffer = 0;

    public void ComputeHeightmap()
    {
        if (_heightmapCompute == null)
            return;

        RenderTexture heightmap = new RenderTexture(_mapSize, _mapSize, 24, RenderTextureFormat.RFloat);
        heightmap.enableRandomWrite = true;
        heightmap.Create();

        int kernelHandle = GetKernelHandle();

        ComputeBuffer cbFBMParams = new ComputeBuffer(1, Marshal.SizeOf(_fbmParams));
        cbFBMParams.SetData(new FBMParameters[] { _fbmParams });
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
            LoadHeightsToBuffers();
        GetComponent<Terrain>().terrainData.heightmapResolution = _mapSize;
        GetComponent<Terrain>().terrainData.SetHeights(0, 0, _heightmap2D);
    }

    public void LoadHeightsToBuffers()
    {
        if (_heightmapTexture == null)
            ComputeHeightmap();

        int processorCount = Environment.ProcessorCount;
        int chunkSize = _mapSize / processorCount;
        _heightmap2D = new float[_mapSize, _mapSize];
        _cellDataBuffers[0] = new CellData[_mapSize * _mapSize];
        _cellDataBuffers[1] = new CellData[_mapSize * _mapSize];
        Color[] data = _heightmapTexture.GetPixels();

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
                    _cellDataBuffers[0][i * _mapSize + j].Height = height;
                    _cellDataBuffers[0][i * _mapSize + j].Hardness = 0.2f;
                    _cellDataBuffers[1][i * _mapSize + j].Height = height;
                    _cellDataBuffers[1][i * _mapSize + j].Hardness = 0.2f;
                }
            }
        });
    }

    public void ErodeTerrain()
    {
        int kernelHandle = _erosionCompute.FindKernel("ErosionMain");

        for (int i = 0; i < 5; i++)
            Debug.Log(_cellDataBuffers[_currentBuffer][i + _mapSize].ToString());

        ComputeBuffer[] computeBuffers = new ComputeBuffer[2] 
        { 
            new ComputeBuffer(_mapSize * _mapSize, sizeof(float) * (4 + 4 + 2)), 
            new ComputeBuffer(_mapSize * _mapSize, sizeof(float) * (4 + 4 + 2)) 
        };
        computeBuffers[_currentBuffer].SetData(_cellDataBuffers[_currentBuffer]);
        computeBuffers[(_currentBuffer + 1) % 2].SetData(_cellDataBuffers[(_currentBuffer + 1) % 2]);

        #region Set Erosion Parameters
        _erosionCompute.SetFloat("MapSize", _mapSize);
        _erosionCompute.SetFloat("RainRate", 0.00001f);
        _erosionCompute.SetFloat("Kr", 1f);
        _erosionCompute.SetFloat("Kc", 0.0001f);
        _erosionCompute.SetFloat("KdMax", 0.002f);
        _erosionCompute.SetFloat("DeltaTime", 0.02f);
        _erosionCompute.SetFloat("Gravity", 9.81f);
        _erosionCompute.SetFloat("CrossArea", 1f);
        _erosionCompute.SetFloat("EvaporationRate", 0.02f);
        _erosionCompute.SetFloat("SoilSuspendedRate", 0.5f);
        _erosionCompute.SetFloat("SedimentDepositionRate", 0.5f);
        _erosionCompute.SetFloat("SedimentSofteningRate", 5f);
        #endregion

        for (int i = 0; i < 1000; i++)
        {
            // Update the compute buffer data for current iteration
            _erosionCompute.SetBuffer(kernelHandle, "Data", computeBuffers[_currentBuffer]);

            _erosionCompute.SetBuffer(kernelHandle, "UpdatedData", computeBuffers[(_currentBuffer + 1) % 2]);

            // Dispatch the compute shader
            _erosionCompute.Dispatch(kernelHandle, _mapSize / 16, _mapSize / 16, 1);

            // Swap the buffers
            _currentBuffer = (_currentBuffer + 1) % 2;
        }

        computeBuffers[_currentBuffer].GetData(_cellDataBuffers[_currentBuffer]);
        computeBuffers[(_currentBuffer + 1) % 2].GetData(_cellDataBuffers[(_currentBuffer + 1) % 2]);
        computeBuffers[_currentBuffer].Release();
        computeBuffers[(_currentBuffer + 1) % 2].Release();

        for (int i = 0; i < 5; i++)
            Debug.Log(_cellDataBuffers[(_currentBuffer + 1) % 2][i + _mapSize].ToString());

        _heightmap2D = new float[_mapSize, _mapSize];

        Parallel.For(0, _mapSize, i =>
        {
            for (int j = 0; j < _mapSize; j++)
            {
                _heightmap2D[i, j] = _cellDataBuffers[(_currentBuffer + 1) % 2][i * _mapSize + j].Height;
            }
        });
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
