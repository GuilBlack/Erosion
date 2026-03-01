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

    int[]   _kernelHandles;
    float   _simulationTime = 0f;
    float   _simulationDelta = 0f;
    bool    _isSimulating = false;

    private void FixedUpdate()
    {
        if (_isSimulating == false)
            return;

        SimulateErosion();
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

        _kernelHandles = new int[6];
        _kernelHandles[0] = erosionComputeShader.FindKernel("WaterIncrement");
        _kernelHandles[1] = erosionComputeShader.FindKernel("WaterOutFlow");
        _kernelHandles[2] = erosionComputeShader.FindKernel("WaterInFlow");
        _kernelHandles[3] = erosionComputeShader.FindKernel("ErosionDeposition");
        _kernelHandles[4] = erosionComputeShader.FindKernel("SedimentTransportation");
        _kernelHandles[5] = erosionComputeShader.FindKernel("Evaporation");

        // Check if all kernels are found
        for (int i = 0; i < _kernelHandles.Length; i++)
        {
            if (_kernelHandles[i] < 0)
            {
                Debug.LogError($"Kernel {_kernelHandles[i]} not found in the erosion compute shader.");
                return;
            }
        }

        // set properties that don't change per kernel
        erosionComputeShader.SetFloat("MapSizeP", heightMap.width);
        //erosionComputeShader.SetFloat("CellSizeM", simulationParams);
        erosionComputeShader.SetFloat("HeightScale", simulationParams.HeightScale);

        erosionComputeShader.SetFloat("TimeStep", simulationParams.TimeStep);

        erosionComputeShader.SetFloat("RainRate", simulationParams.RainRate);
        erosionComputeShader.SetFloat("EvaporationRate", simulationParams.EvaporationRate);

        // Set buffer/texture references for all kernels
        for (int i = 0; i < _kernelHandles.Length; i++)
        {
            erosionComputeShader.SetTexture(_kernelHandles[i], "TerrainAndWaterHeights", heightMap);
        }

        _isSimulating = true;
        _simulationTime = 0f;
        _simulationDelta = 0f;

        _terrainMaterial.SetTexture("HeightMap", heightMap);
    }

    public void StopSimulation()
    {
        _isSimulating = false;
    }

    private void SimulateErosion()
    {
        int kernelHandle = erosionComputeShader.FindKernel("WaterIncrement");
        erosionComputeShader.Dispatch(kernelHandle, heightMap.width / 8, heightMap.height / 8, 1);

        kernelHandle = erosionComputeShader.FindKernel("Evaporation");
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
