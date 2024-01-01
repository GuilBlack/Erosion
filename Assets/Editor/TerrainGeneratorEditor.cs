using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TerrainGenerator))]
public class TerrainGeneratorEditor : Editor
{
    TerrainGenerator heightmapGenerator;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        heightmapGenerator = target as TerrainGenerator;

        if (heightmapGenerator == null)
            return;

        if (GUILayout.Button("Generate"))
        {
            heightmapGenerator.ComputeHeightmap();
        }

        if (GUILayout.Button("Save"))
        {
            heightmapGenerator.SaveHeightmapToAssets();
        }

        if (GUILayout.Button("LoadHeightsToBuffers"))
        {
            heightmapGenerator.LoadHeightsToBuffers();
        }

        if (GUILayout.Button("SetTerrainHeightmap"))
        {
            heightmapGenerator.SetTerrainHeightmap();
        }
    }
}
