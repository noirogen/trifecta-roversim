using System;
using UnityEngine;

namespace Rocks
{
    [Serializable]
    public class ClusterSettings
    {
        // Each placement samples the noise somewhere new in this range, so every field clumps differently.
        private const float NoiseOffsetRange = 1000f;

        [Tooltip("0 = evenly spread, 1 = rocks gather into patches with bare ground between.")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.6f;
        [Tooltip("Approximate size of a patch, in meters.")]
        [SerializeField, Min(1f)] private float patchSize = 30f;

        public ClusterMask CreateMask(System.Random rng)
        {
            var offset = new Vector2(
                (float)rng.NextDouble() * NoiseOffsetRange,
                (float)rng.NextDouble() * NoiseOffsetRange);
            return new ClusterMask(strength, patchSize, offset);
        }
    }

    public readonly struct ClusterMask
    {
        // Unity's Perlin noise rarely leaves this band, so it is stretched to 0-1; otherwise the
        // bare patches would never be fully bare.
        private const float NoiseLow = 0.3f;
        private const float NoiseHigh = 0.7f;

        private readonly float strength;
        private readonly float patchSize;
        private readonly Vector2 offset;

        public ClusterMask(float strength, float patchSize, Vector2 offset)
        {
            this.strength = strength;
            this.patchSize = patchSize;
            this.offset = offset;
        }

        public float AcceptChance(Vector2 point)
        {
            if (strength <= 0f)
            {
                return 1f;
            }

            float noise = Mathf.PerlinNoise(point.x / patchSize + offset.x, point.y / patchSize + offset.y);
            float patch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(NoiseLow, NoiseHigh, noise));
            return Mathf.Lerp(1f, patch, strength);
        }
    }
}
