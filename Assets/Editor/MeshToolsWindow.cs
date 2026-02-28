using UnityEditor;
using UnityEngine;

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Assets.MeshTools;

namespace Assets.Editor
{
    public class MeshToolsWindow : EditorWindow
    {
        public enum MeshType
        {
            TesselatedPlane,
        }

        // Plane params
        float _size = 512f;
        int _resolution = 1024;

        MeshType _meshType = MeshType.TesselatedPlane;

        string _defaultFileName = "ProceduralMesh";
        bool _overwriteIfExists = false;

        [MenuItem("Tools/Procedural Mesh/Window")]
        public static void Open() => GetWindow<MeshToolsWindow>("Procedural Mesh");

        void OnGUI()
        {
            EditorGUILayout.LabelField("Procedural Mesh Asset Generator", EditorStyles.boldLabel);

            _meshType = (MeshType)EditorGUILayout.EnumPopup("Mesh Type", _meshType);
            EditorGUILayout.Space(8);

            switch (_meshType)
            {
            case MeshType.TesselatedPlane:
                DrawPlaneUI();
                break;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Save", EditorStyles.boldLabel);
            _defaultFileName = EditorGUILayout.TextField("Default File Name", _defaultFileName);
            _overwriteIfExists = EditorGUILayout.Toggle("Overwrite If Exists", _overwriteIfExists);

            EditorGUILayout.Space(12);

            using (new EditorGUI.DisabledScope(!CanGenerate()))
            {
                if (GUILayout.Button("Generate + Save Mesh Asset..."))
                {
                    GenerateAndSave();
                }
            }
        }

        void DrawPlaneUI()
        {
            _size = EditorGUILayout.FloatField("Size", _size);
            _resolution = EditorGUILayout.IntSlider("Resolution", _resolution, 1, 4096);

            if (_size <= 0f)
                EditorGUILayout.HelpBox("Size must be > 0.", MessageType.Warning);

            long vertCount = (long)(_resolution + 1) * (_resolution + 1);
            long triCount = (long)_resolution * _resolution * 2;

            EditorGUILayout.HelpBox($"Verts: {vertCount:n0} | Tris: {triCount:n0}", MessageType.Info);
        }

        bool CanGenerate()
        {
            switch (_meshType)
            {
            case MeshType.TesselatedPlane:
                return _size > 0f && _resolution >= 1;
            default:
                return false;
            }
        }

        void GenerateAndSave()
        {
            string suggested = string.IsNullOrWhiteSpace(_defaultFileName) ? "GeneratedMesh" : _defaultFileName;

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Generated Mesh",
                suggested,
                "asset",
                "Choose where to save the generated mesh asset."
            );

            if (string.IsNullOrEmpty(path))
                return;

            Mesh mesh = _meshType switch
            {
                MeshType.TesselatedPlane => BuildPlaneMesh(_size, _resolution),
                _ => null
            };

            if (mesh == null)
            {
                Debug.LogError("Mesh generation failed.");
                return;
            }

            mesh.name = System.IO.Path.GetFileNameWithoutExtension(path);

            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                if (!_overwriteIfExists)
                {
                    Debug.LogWarning($"Mesh asset already exists at path:\n{path}\nEnable 'Overwrite If Exists' to replace it.");
                    DestroyImmediate(mesh);
                    return;
                }

                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
                EditorUtility.SetDirty(mesh);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Selection.activeObject = saved;
            EditorGUIUtility.PingObject(saved);

            Debug.Log($"Saved mesh asset: {path}");
        }

        static Mesh BuildPlaneMesh(float size, int resolution)
        {
            int vertsPerSide = resolution + 1;
            int vertCount = vertsPerSide * vertsPerSide;

            int quadCount = resolution * resolution;
            int indexCount = quadCount * 6;

            var vertices = new NativeArray<float3>(vertCount, Allocator.TempJob);
            var uvs = new NativeArray<float2>(vertCount, Allocator.TempJob);
            var normals = new NativeArray<float3>(vertCount, Allocator.TempJob);

            var job = new BuildTesselatedPlane
            {
                Resolution = resolution,
                Size = size,
                Vertices = vertices,
                UVs = uvs,
                Normals = normals
            };

            job.Schedule(vertCount, 128).Complete();

            var triangles = new NativeArray<int>(quadCount * 6, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int t = 0;
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                {
                    int i = y * (resolution + 1) + x;
                    triangles[t++] = i;
                    triangles[t++] = i + resolution + 1;
                    triangles[t++] = i + 1;

                    triangles[t++] = i + 1;
                    triangles[t++] = i + resolution + 1;
                    triangles[t++] = i + resolution + 2;
                }

            var mesh = new Mesh();

            mesh.indexFormat = (vertCount > 65535)
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;

            // Convert NativeArray data into managed arrays for Mesh API
            var v3 = new Vector3[vertCount];
            var v2 = new Vector2[vertCount];
            var n3 = new Vector3[vertCount];

            for (int i = 0; i < vertCount; i++)
            {
                float3 v = vertices[i];
                float2 uv = uvs[i];
                float3 n = normals[i];

                v3[i] = new Vector3(v.x, v.y, v.z);
                v2[i] = new Vector2(uv.x, uv.y);
                n3[i] = new Vector3(n.x, n.y, n.z);
            }

            mesh.vertices = v3;
            mesh.uv = v2;
            mesh.normals = n3;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateBounds();

            vertices.Dispose();
            uvs.Dispose();
            normals.Dispose();
            triangles.Dispose();

            return mesh;
        }

    }
}
