using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Rocks
{
    public class RockPlacer : MonoBehaviour
    {
        private const float MaxEmbeddedness = 0.9f;
        private const float CrowdedWarningFraction = 0.1f;
        private const float MinCellRadius = 0.01f;

        [Header("Prefabs")]
        [SerializeField] private GameObject[] rockPrefabs;
        [SerializeField] private GameObject[] pebblePrefabs;

        [Header("Placement")]
        [Tooltip("Fraction of each rock's height that is buried below the ground.")]
        [SerializeField, Range(0f, MaxEmbeddedness)] private float embeddedness = 0.2f;
        [Tooltip("0 = rocks stand upright, 1 = rocks tilt fully with the ground slope.")]
        [SerializeField, Range(0f, 1f)] private float alignToSurface = 0.6f;
        [Tooltip("Random per-axis stretch so repeated prefabs look less alike.")]
        [SerializeField, Range(0f, 0.5f)] private float shapeVariation = 0.15f;
        [Tooltip("Minimum empty space between the footprints of two rocks, in meters.")]
        [SerializeField, Min(0f)] private float minGap = 0.1f;
        [Tooltip("Random spots tried per rock before it is skipped for lack of space.")]
        [SerializeField, Min(1)] private int attemptsPerRock = 30;

        [Header("Sizes (height in meters)")]
        [SerializeField] private RockSizeDistribution rockSizes = new RockSizeDistribution(0.15f, 1.5f, 2.5f);
        [SerializeField] private RockSizeDistribution pebbleSizes = new RockSizeDistribution(0.03f, 0.12f, 1.5f);
        [Tooltip("Pebble density as a multiple of the rock density passed to PlaceRocks.")]
        [SerializeField, Min(0f)] private float pebblesPerRock = 4f;

        [Header("Draw distance (meters from each camera)")]
        [Tooltip("Set this at or past where the height fog reaches its Max Opacity. Colliders exist at every distance.")]
        [SerializeField, Min(1f)] private float rockDrawDistance = 400f;
        [SerializeField, Min(1f)] private float pebbleDrawDistance = 150f;
        [Tooltip("Rocks and pebbles whose bounds would span fewer pixels than this are not drawn. 0 draws everything within the draw distance.")]
        [SerializeField, Min(0f)] private float minScreenPixels = 3f;
        [Tooltip("On-screen size where meshes with Mesh LODs get full detail. Lower levels take over once they give as many triangles per pixel as full detail does at this size. 0 always uses full detail.")]
        [SerializeField, Min(0f)] private float lod0ScreenPixels = 150f;

        [Header("Clustering")]
        [SerializeField] private ClusterSettings clustering = new ClusterSettings();

        [Header("Safety limits")]
        [SerializeField, Min(1)] private int maxRocks = 50000;
        [SerializeField, Min(0)] private int maxPebbles = 500000;

        [Header("Preview (context menu)")]
        [SerializeField] private GameObject previewTarget;
        [SerializeField, Min(0f)] private float previewDensity = 0.05f;

        private readonly Dictionary<GameObject, RockShape> shapeCache = new Dictionary<GameObject, RockShape>();

        public float Embeddedness
        {
            get => embeddedness;
            set => embeddedness = Mathf.Clamp(value, 0f, MaxEmbeddedness);
        }

        public RockPlacementResult PlaceRocks(float rockDensity, GameObject target)
        {
            return PlaceRocks(rockDensity, target, null);
        }

        public RockPlacementResult PlaceRocks(float rockDensity, GameObject target, RockPlacementOptions options)
        {
            if (target == null)
            {
                Debug.LogError("[RockPlacer] No target given.");
                return default;
            }

            ClearRocks(target);

            if (rockDensity < 0f)
            {
                Debug.LogWarning($"[RockPlacer] Negative density {rockDensity} treated as 0.");
            }
            if (rockDensity <= 0f)
            {
                return default;
            }

            if (rockPrefabs == null || rockPrefabs.Length == 0)
            {
                Debug.LogError("[RockPlacer] No rock prefabs assigned.");
                return default;
            }

            var surface = new TerrainSurface(target);
            if (surface.IsEmpty)
            {
                Debug.LogError($"[RockPlacer] '{target.name}' has no Terrain on it or its children.");
                return default;
            }

            Rect area = ResolveArea(surface.BoundsXZ, options);
            if (area.width <= 0f || area.height <= 0f)
            {
                Debug.LogWarning("[RockPlacer] Placement area does not overlap the terrain.");
                return default;
            }

            Stopwatch timer = Stopwatch.StartNew();
            int seed = options?.Seed ?? System.Environment.TickCount;
            var rng = new System.Random(seed);
            var spawner = new RockSpawner(shapeCache, embeddedness, alignToSurface, shapeVariation);

            float areaSize = area.width * area.height;
            List<RockSpec> rocks = CreateRockSpecs(spawner, rockDensity * areaSize, rng);
            List<RockSpec> pebbles = CreatePebbleSpecs(spawner, rockDensity * pebblesPerRock * areaSize, rng);

            float maxRadius = MaxRadius(rocks, pebbles);
            var sampler = new ScatterSampler(area, maxRadius, minGap, clustering.CreateMask(rng), rng, attemptsPerRock);
            RockField field = RockField.Create(
                target.transform,
                new RockField.DrawSettings(
                    InstancedRenderer.ChunkSizeFor(rocks.Count, areaSize, rockPrefabs.Length), rockDrawDistance, minScreenPixels, lod0ScreenPixels),
                new RockField.DrawSettings(
                    InstancedRenderer.ChunkSizeFor(pebbles.Count, areaSize, PrefabCount(pebblePrefabs)), pebbleDrawDistance, minScreenPixels, lod0ScreenPixels));
            using var colliderTemplates = new ColliderTemplates();

            bool IsAllowed(Vector2 point, float radius) =>
                surface.Contains(point) && !InClearZone(options, point, radius);

            int rockCount = 0;
            int crowded = 0;
            foreach (RockSpec spec in rocks)
            {
                ScatterResult result = sampler.TryPlace(spec.FootprintRadius, IsAllowed, out Vector2 point);
                if (result == ScatterResult.NoSpace)
                {
                    crowded++;
                }
                if (result != ScatterResult.Placed || !surface.TrySample(point, out Vector3 ground, out Vector3 normal))
                {
                    continue;
                }

                RockPose pose = spawner.PoseOn(spec, ground, normal, rng);
                spawner.Spawn(colliderTemplates.For(spec.Prefab), pose, field.RockColliders);
                field.Rocks.Add(spec.Prefab, pose.Matrix);
                rockCount++;
            }
            long rocksDone = timer.ElapsedMilliseconds;

            int pebbleCount = 0;
            foreach (RockSpec spec in pebbles)
            {
                Vector2 point = sampler.RandomPoint();
                if (!sampler.IsFree(point, spec.FootprintRadius) || !surface.TrySample(point, out Vector3 ground, out Vector3 normal))
                {
                    continue;
                }

                field.Pebbles.Add(spec.Prefab, spawner.PoseOn(spec, ground, normal, rng).Matrix);
                pebbleCount++;
            }
            long pebblesDone = timer.ElapsedMilliseconds;

            field.Activate();
            long total = timer.ElapsedMilliseconds;

            if (crowded > rocks.Count * CrowdedWarningFraction)
            {
                Debug.LogWarning($"[RockPlacer] {crowded} of {rocks.Count} rocks found no free space. " +
                                 "Lower the density, the max rock size, or the clustering strength.");
            }

            Debug.Log($"[RockPlacer] Placed {rockCount} rocks and {pebbleCount} pebbles on '{target.name}' " +
                      $"in {total} ms (rocks {rocksDone} ms, pebbles {pebblesDone - rocksDone} ms, " +
                      $"activation {total - pebblesDone} ms, seed {seed}).");

            return new RockPlacementResult(rockCount, pebbleCount, seed);
        }

        public void ClearRocks(GameObject target)
        {
            if (target != null)
            {
                RockField.DestroyOn(target.transform);
            }
        }

        private List<RockSpec> CreateRockSpecs(RockSpawner spawner, float expectedCount, System.Random rng)
        {
            if (expectedCount > maxRocks)
            {
                Debug.LogWarning($"[RockPlacer] {expectedCount:N0} rocks requested, capped at {maxRocks}. " +
                                 "Pass a smaller AreaXZ in RockPlacementOptions to keep the full density.");
            }

            int count = CountFor(expectedCount, maxRocks, rng);
            var specs = new List<RockSpec>(count);
            for (int i = 0; i < count; i++)
            {
                specs.Add(spawner.CreateSpec(rockPrefabs, rockSizes, rng));
            }

            // Largest first, so big rocks claim space before small ones fill the gaps around them.
            specs.Sort((a, b) => b.FootprintRadius.CompareTo(a.FootprintRadius));
            return specs;
        }

        private List<RockSpec> CreatePebbleSpecs(RockSpawner spawner, float expectedCount, System.Random rng)
        {
            if (pebblePrefabs == null || pebblePrefabs.Length == 0)
            {
                return new List<RockSpec>();
            }

            int count = CountFor(expectedCount, maxPebbles, rng);
            var specs = new List<RockSpec>(count);
            for (int i = 0; i < count; i++)
            {
                specs.Add(spawner.CreateSpec(pebblePrefabs, pebbleSizes, rng));
            }
            return specs;
        }

        private static Rect ResolveArea(Rect terrainBounds, RockPlacementOptions options)
        {
            if (options?.AreaXZ == null)
            {
                return terrainBounds;
            }

            Rect requested = options.AreaXZ.Value;
            float xMin = Mathf.Max(terrainBounds.xMin, requested.xMin);
            float yMin = Mathf.Max(terrainBounds.yMin, requested.yMin);
            float xMax = Mathf.Min(terrainBounds.xMax, requested.xMax);
            float yMax = Mathf.Min(terrainBounds.yMax, requested.yMax);
            return xMax > xMin && yMax > yMin ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : Rect.zero;
        }

        private static bool InClearZone(RockPlacementOptions options, Vector2 point, float radius)
        {
            if (options == null)
            {
                return false;
            }

            foreach (RockClearZone zone in options.ClearZones)
            {
                if (zone.Overlaps(point, radius))
                {
                    return true;
                }
            }
            return false;
        }

        private static float MaxRadius(List<RockSpec> rocks, List<RockSpec> pebbles)
        {
            float max = rocks.Count > 0 ? rocks[0].FootprintRadius : 0f;
            foreach (RockSpec pebble in pebbles)
            {
                max = Mathf.Max(max, pebble.FootprintRadius);
            }
            return Mathf.Max(max, MinCellRadius);
        }

        private static int PrefabCount(GameObject[] prefabs)
        {
            return prefabs == null ? 0 : prefabs.Length;
        }

        // Capping before rounding keeps huge expected counts from overflowing an int.
        private static int CountFor(float expectedCount, int cap, System.Random rng)
        {
            return expectedCount >= cap ? cap : StochasticRound(expectedCount, rng);
        }

        private static int StochasticRound(float value, System.Random rng)
        {
            int whole = Mathf.FloorToInt(value);
            return rng.NextDouble() < value - whole ? whole + 1 : whole;
        }

        [ContextMenu("Place Rocks On Preview Target")]
        private void PlacePreview()
        {
            PlaceRocks(previewDensity, previewTarget);
        }

        [ContextMenu("Clear Preview Rocks")]
        private void ClearPreview()
        {
            ClearRocks(previewTarget);
        }
    }
}
