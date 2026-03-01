using UnityEngine;
using Assets;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ErosionSimulation : MonoBehaviour
{
    [Header("Simu Visualization")]
    public Material _terrainMaterial;
    public Material _waterMaterial;

    [Header("Simu Properties")]
    public ComputeShader    erosionComputeShader;
    public RenderTexture    heightMap;
    public ErosionParamsSO  simulationParams;
    public bool             debugFlux;

    int[]           _kernelHandles;
    bool            _isSimulating = false;
    ComputeBuffer   _cellData;
    RenderTexture   _debugFluxTexture;

    private void FixedUpdate()
    {
        if (_isSimulating == false)
            return;

        SimulateErosion();
    }

    private void OnDestroy()
    {
        if (_cellData != null)
            _cellData.Release();

        if (_debugFluxTexture != null)
        {
            _debugFluxTexture.Release();
            _debugFluxTexture = null;
        }
    }

    public void StartSimulation()
    {
        if (erosionComputeShader == null || heightMap == null)
        {
            Debug.LogError("Erosion Compute Shader or Height Map is not assigned.");
            return;
        }
        if (_terrainMaterial == null || _waterMaterial == null)
            Debug.LogWarning("Terrain or Water material is not assigned. " +
                "Simulation visualization may not work properly.");

        _kernelHandles = new int[7];
        _kernelHandles[0] = erosionComputeShader.FindKernel("WaterIncrement");
        _kernelHandles[1] = erosionComputeShader.FindKernel("WaterOutFlow");
        _kernelHandles[2] = erosionComputeShader.FindKernel("WaterInFlow");
        _kernelHandles[3] = erosionComputeShader.FindKernel("ErosionDeposition");
        _kernelHandles[4] = erosionComputeShader.FindKernel("SedimentTransportation");
        _kernelHandles[5] = erosionComputeShader.FindKernel("Evaporation");
        _kernelHandles[6] = erosionComputeShader.FindKernel("DebugFlux");

        // Check if all kernels are found
        for (int i = 0; i < _kernelHandles.Length; i++)
        {
            if (_kernelHandles[i] < 0)
            {
                Debug.LogError($"Kernel {_kernelHandles[i]} not found in the erosion compute shader.");
                return;
            }
        }

        // create cell data buffer
        _cellData = new ComputeBuffer(heightMap.width * heightMap.height, sizeof(float) * (4*2));
        Debug.Log($"CellData buffer created with {heightMap.width * heightMap.height} elements.");

        if (debugFlux)
        {
            _debugFluxTexture = new RenderTexture(heightMap.width, heightMap.height, 0, RenderTextureFormat.ARGBFloat);
            _debugFluxTexture.enableRandomWrite = true;
            _debugFluxTexture.Create();
        }

        // set properties that don't change per kernel
        erosionComputeShader.SetFloat("MapSizeP", heightMap.width);
        erosionComputeShader.SetFloat("CellSizeM", heightMap.width / simulationParams.MapSizeM);

        erosionComputeShader.SetFloat("TimeStep", simulationParams.TimeStep);

        erosionComputeShader.SetFloat("RainRate", simulationParams.RainRate);
        erosionComputeShader.SetFloat("EvaporationRate", simulationParams.EvaporationRate);
        erosionComputeShader.SetFloat("Gravity", simulationParams.Gravity);
        erosionComputeShader.SetFloat("PipeCrossSectionArea", simulationParams.PipeCrossArea);
        erosionComputeShader.SetFloat("PipeLength", simulationParams.PipeLength);

        // Set buffer/texture references for all kernels
        for (int i = 0; i < _kernelHandles.Length; i++)
        {
            erosionComputeShader.SetTexture(_kernelHandles[i], "TerrainAndWaterHeights", heightMap);
            erosionComputeShader.SetBuffer(_kernelHandles[i], "CellDataBuffer", _cellData);
        }

        if (debugFlux)
            erosionComputeShader.SetTexture(_kernelHandles[6], "DebugFluxTexture", _debugFluxTexture);

        _isSimulating = true;

        _terrainMaterial.SetTexture("HeightMap", heightMap);
    }

    public void StopSimulation()
    {
        _isSimulating = false;
        if (_cellData != null)
            _cellData.Release();

        if (_debugFluxTexture != null)
        {
            _debugFluxTexture.Release();
            _debugFluxTexture = null;
        }
    }

    private void SimulateErosion()
    {
        int kernelHandle = erosionComputeShader.FindKernel("WaterIncrement");
        erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);

        kernelHandle = erosionComputeShader.FindKernel("WaterOutFlow");
        erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);

        kernelHandle = erosionComputeShader.FindKernel("WaterInFlow");
        erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);

        kernelHandle = erosionComputeShader.FindKernel("Evaporation");
        erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);

        kernelHandle = _kernelHandles[6];
        if (debugFlux)
            erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(ErosionSimulation))]
public class ErosionSimulationEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        ErosionSimulation erosionSim = (ErosionSimulation)target;
        if (GUILayout.Button("Start Simulation"))
        {
            erosionSim.StartSimulation();
        }
        if (GUILayout.Button("Stop Simulation"))
        {
            erosionSim.StopSimulation();
        }
    }
}
#endif
