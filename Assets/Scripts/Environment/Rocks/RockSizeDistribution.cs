using System;
using UnityEngine;

namespace Rocks
{
    // Truncated power law: the number of rocks at least D tall falls off as D^-exponent,
    // so small rocks are common and large ones rare. Higher exponent = fewer large rocks.
    [Serializable]
    public class RockSizeDistribution
    {
        [SerializeField, Min(0.001f)] private float minSize;
        [SerializeField, Min(0.001f)] private float maxSize;
        [Tooltip("Higher values make large rocks rarer.")]
        [SerializeField, Range(0.1f, 5f)] private float exponent;

        public RockSizeDistribution(float minSize, float maxSize, float exponent)
        {
            this.minSize = minSize;
            this.maxSize = maxSize;
            this.exponent = exponent;
        }

        public float Sample(System.Random rng)
        {
            float lo = Mathf.Min(minSize, maxSize);
            float hi = Mathf.Max(minSize, maxSize);
            if (hi - lo < 1e-4f)
            {
                return lo;
            }

            // Inverse transform sampling: maps a uniform u in [0, 1) onto the power law.
            float a = Mathf.Pow(lo, -exponent);
            float b = Mathf.Pow(hi, -exponent);
            float u = (float)rng.NextDouble();
            return Mathf.Pow(a - u * (a - b), -1f / exponent);
        }
    }
}
