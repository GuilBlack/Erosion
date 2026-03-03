using UnityEngine;

namespace Assets
{
    // param range taken from: https://old.cescg.org/CESCG-2011/papers/TUBudapest-Jako-Balazs.pdf
    [CreateAssetMenu(fileName = "ErosionParamsSO", menuName = "ScriptableObjects/ErosionParamsSO", order = 1)]
    public class ErosionParamsSO : ScriptableObject
    {
        [Range(0, 100)]
        public float TimeScale = 1f;
        [Range(0, 0.05f)]
        public float TimeStep = 0.02f;
        [Range(0.1f, 20f)]
        public float Gravity = 9.81f;
        [Range(0, 0.05f)]
        public float RainRate = 0.02f;
        public float MapSizeM = 1024f;

        [Range(0, 0.05f)]
        public float EvaporationRate = 0.015f;
        [Range(0.1f, 2.0f)]
        public float SoilSuspensionRate = 0.01f;
        [Range(0.1f, 3.0f)]
        public float SedimentDepositionRate = 0.3f;
        [Range(0.1f, 10f)]
        public float SedimentSofteningRate = 3f;
        [Range(0.1f, 3f)]
        public float SedimentCapacity = 0.1f;
        [Range(0, 0.1f)]
        public float MinTiltAngle = 0.05f;
        public float MaxErosionDepth = 1f;
        public float MinHardness = 0.1f;

        public float PipeLength = 1f;
        [Range(0.1f, 30f)]
        public float PipeCrossArea = 1f;
    }
}
