using System.Collections.Generic;
using UnityEngine;

namespace Rocks
{
    public class RockPlacementOptions
    {
        // World-space XZ rectangle (Rect.y maps to world Z). Null means the whole terrain.
        public Rect? AreaXZ;
        public int? Seed;
        public readonly List<RockClearZone> ClearZones = new List<RockClearZone>();

        public RockPlacementOptions KeepClear(Vector3 worldCenter, float radius)
        {
            ClearZones.Add(new RockClearZone(worldCenter, radius));
            return this;
        }
    }

    public readonly struct RockClearZone
    {
        public readonly Vector2 Center;
        public readonly float Radius;

        public RockClearZone(Vector3 worldCenter, float radius)
        {
            Center = new Vector2(worldCenter.x, worldCenter.z);
            Radius = radius;
        }

        public bool Overlaps(Vector2 point, float rockRadius)
        {
            float reach = Radius + rockRadius;
            return (point - Center).sqrMagnitude < reach * reach;
        }
    }

    public readonly struct RockPlacementResult
    {
        public readonly int RockCount;
        public readonly int PebbleCount;
        public readonly int Seed;

        public RockPlacementResult(int rockCount, int pebbleCount, int seed)
        {
            RockCount = rockCount;
            PebbleCount = pebbleCount;
            Seed = seed;
        }
    }
}
