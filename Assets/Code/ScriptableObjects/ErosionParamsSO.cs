using UnityEngine;

namespace Assets
{
    [CreateAssetMenu(fileName = "ErosionParamsSO", menuName = "ScriptableObjects/ErosionParamsSO", order = 1)]
    public class ErosionParamsSO : ScriptableObject
    {
        [Range(0, 100)]
        public float TimeScale = 1f;
        [Range(0, 0.05f)]
        public float TimeStep = 0.02f;
        public float Gravity = 9.81f;
        public float RainRate = 0.02f;
        public float HeightScale = 500f;
        public float MapSize = 1024f;

        [Range(0, 1)]
        public float EvaporationRate = 0.015f;
        public float SoilSuspensionRate = 0.01f;
        public float SedimentDepositionRate = 0.3f;
        public float SedimentSofteningRate = 3f;
        public float SedimentCapacity = 0.1f;
        public float MaxErosionDepth = 1f;
        public float MinHardness = 0.1f;

        public float PipeLength = 1f;
        public float PipeCrossArea = 1f;
    }
}
