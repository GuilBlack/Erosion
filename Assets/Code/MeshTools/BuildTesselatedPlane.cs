using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.MeshTools
{
    [BurstCompile]
    public struct BuildTesselatedPlane : IJobParallelFor
    {
        public int Resolution;
        public double Size;

        public NativeArray<float3> Vertices;
        public NativeArray<float2> UVs;
        public NativeArray<float3> Normals;

        public void Execute(int index)
        {
            int vertsPerSide = Resolution + 1;
            int y = index / vertsPerSide;
            int x = index % vertsPerSide;

            double fx = (double)x / Resolution;
            double fy = (double)y / Resolution;

            double xPos = (fx - 0.5) * Size;
            double zPos = (fy - 0.5) * Size;

            Vertices[index] = new float3((float)xPos, 0, (float)zPos);
            UVs[index] = new float2((float)fx, (float)fy);
            Normals[index] = new float3(0, 1, 0);
        }
    }

    public class PlaneMeshBuilder
    {
        public static Mesh BuildPlaneMesh(float size, int resolution)
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
