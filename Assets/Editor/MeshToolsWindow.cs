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
                MeshType.TesselatedPlane => PlaneMeshBuilder.BuildPlaneMesh(_size, _resolution),
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
    }
}
