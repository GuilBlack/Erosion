using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

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
}
