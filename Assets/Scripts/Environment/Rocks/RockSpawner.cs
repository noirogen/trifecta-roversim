using System.Collections.Generic;
using UnityEngine;

namespace Rocks
{
    public readonly struct RockSpec
    {
        public readonly GameObject Prefab;
        public readonly RockShape Shape;
        public readonly Vector3 Scale;
        public readonly float FootprintRadius;

        public RockSpec(GameObject prefab, RockShape shape, Vector3 scale)
        {
            Prefab = prefab;
            Shape = shape;
            Scale = scale;
            FootprintRadius = shape.FootprintRadius * Mathf.Max(scale.x, scale.z);
        }
    }

    public readonly struct RockPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;

        public RockPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            Rotation = rotation;
            Scale = scale;
        }

        public Matrix4x4 Matrix => Matrix4x4.TRS(Position, Rotation, Scale);
    }

    public class RockSpawner
    {
        // Caps the extra sinking on cliffs, where tan(tilt) grows without bound.
        private const float MaxTiltRadians = 60f * Mathf.Deg2Rad;

        private readonly Dictionary<GameObject, RockShape> shapeCache;
        private readonly float embeddedness;
        private readonly float alignToSurface;
        private readonly float shapeVariation;

        public RockSpawner(Dictionary<GameObject, RockShape> shapeCache, float embeddedness, float alignToSurface, float shapeVariation)
        {
            this.shapeCache = shapeCache;
            this.embeddedness = embeddedness;
            this.alignToSurface = alignToSurface;
            this.shapeVariation = shapeVariation;
        }

        public RockSpec CreateSpec(GameObject[] prefabs, RockSizeDistribution sizes, System.Random rng)
        {
            GameObject prefab = prefabs[rng.Next(prefabs.Length)];
            RockShape shape = ShapeOf(prefab);

            float uniform = sizes.Sample(rng) / shape.Height;
            var scale = new Vector3(
                uniform * Vary(rng),
                uniform * Vary(rng),
                uniform * Vary(rng));
            return new RockSpec(prefab, shape, scale);
        }

        public RockPose PoseOn(RockSpec spec, Vector3 ground, Vector3 groundNormal, System.Random rng)
        {
            Vector3 up = Vector3.Slerp(Vector3.up, groundNormal, alignToSurface).normalized;
            Quaternion yaw = Quaternion.AngleAxis((float)rng.NextDouble() * 360f, Vector3.up);
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, up) * yaw;

            // Where the rock is not tilted to match the slope, its downhill edge sits lower than
            // its center, so sink it further to keep that edge from floating.
            float tilt = Mathf.Min(Vector3.Angle(up, groundNormal) * Mathf.Deg2Rad, MaxTiltRadians);
            float slopeSink = spec.FootprintRadius * Mathf.Tan(tilt);

            float height = spec.Shape.Height * spec.Scale.y;
            // BottomY is negative when the pivot sits above the mesh bottom, which lifts the
            // pivot so the bottom, not the pivot, ends up embeddedness * height underground.
            float bottomOffset = spec.Shape.BottomY * spec.Scale.y;
            Vector3 position = ground - up * (embeddedness * height + slopeSink + bottomOffset);

            return new RockPose(position, rotation, spec.Scale);
        }

        public GameObject Spawn(GameObject prefab, RockPose pose, Transform parent)
        {
            GameObject rock = Object.Instantiate(prefab, pose.Position, pose.Rotation, parent);
            rock.transform.localScale = pose.Scale;
            return rock;
        }

        private RockShape ShapeOf(GameObject prefab)
        {
            if (!shapeCache.TryGetValue(prefab, out RockShape shape))
            {
                shape = RockShape.Measure(prefab);
                shapeCache.Add(prefab, shape);
            }
            return shape;
        }

        private float Vary(System.Random rng)
        {
            return 1f + ((float)rng.NextDouble() * 2f - 1f) * shapeVariation;
        }
    }
}
