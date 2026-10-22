using UnityEngine;

namespace Rocks
{
    // Reads ground height and normal straight from TerrainData instead of raycasting, so lookups
    // are cheap, need no colliders, and never hit rocks that were already placed.
    public class TerrainSurface
    {
        private readonly Terrain[] terrains;
        private int lastHit;

        public TerrainSurface(GameObject target)
        {
            terrains = target.GetComponentsInChildren<Terrain>();
            BoundsXZ = ComputeBounds(terrains);
        }

        public bool IsEmpty => terrains.Length == 0;
        public Rect BoundsXZ { get; }

        public bool Contains(Vector2 point)
        {
            return FindTerrain(point, out _, out _) >= 0;
        }

        public bool TrySample(Vector2 point, out Vector3 ground, out Vector3 normal)
        {
            int index = FindTerrain(point, out float u, out float v);
            if (index < 0)
            {
                ground = default;
                normal = Vector3.up;
                return false;
            }

            Terrain terrain = terrains[index];
            TerrainData data = terrain.terrainData;
            ground = new Vector3(point.x, terrain.GetPosition().y + data.GetInterpolatedHeight(u, v), point.y);
            normal = data.GetInterpolatedNormal(u, v);
            return true;
        }

        private int FindTerrain(Vector2 point, out float u, out float v)
        {
            if (TryGetUV(terrains[lastHit], point, out u, out v))
            {
                return lastHit;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                if (i != lastHit && TryGetUV(terrains[i], point, out u, out v))
                {
                    lastHit = i;
                    return i;
                }
            }
            return -1;
        }

        private static bool TryGetUV(Terrain terrain, Vector2 point, out float u, out float v)
        {
            Vector3 origin = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            u = (point.x - origin.x) / size.x;
            v = (point.y - origin.z) / size.z;
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }

        private static Rect ComputeBounds(Terrain[] terrains)
        {
            if (terrains.Length == 0)
            {
                return Rect.zero;
            }

            float xMin = float.MaxValue, zMin = float.MaxValue;
            float xMax = float.MinValue, zMax = float.MinValue;
            foreach (Terrain terrain in terrains)
            {
                Vector3 origin = terrain.GetPosition();
                Vector3 size = terrain.terrainData.size;
                xMin = Mathf.Min(xMin, origin.x);
                zMin = Mathf.Min(zMin, origin.z);
                xMax = Mathf.Max(xMax, origin.x + size.x);
                zMax = Mathf.Max(zMax, origin.z + size.z);
            }
            return Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        }
    }
}
