using UnityEngine;

namespace Assets
{
    [CreateAssetMenu(fileName = "FBMParamsSO", menuName = "ScriptableObjects/FBMParamsSO", order = 1)]
    public class FBMParamsSO : ScriptableObject
    {
        public int      Octaves = 8;
        public float    Persistence = 0.5f;
        public float    Lacunarity = 2;
        public float    Exponentiation = 4;
        public float    Amplitude = 0.5f;
        public float    Frequency = 1f;
        public float    Seed = 4.47f;
        public float    Scale = 1f;

        public bool     IsRidgedFBM = true;
        public bool     IsCombinedFBM = false;
    }

    // for simulation
    public struct FBMParams
    {
        public int      Octaves;
        public float    Persistence;
        public float    Lacunarity;
        public float    Exponentiation;
        public float    Amplitude;
        public float    Frequency;
        public float    Seed;
        public float    Scale;
    };
}
