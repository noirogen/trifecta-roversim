using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rocks
{
    public enum ScatterResult
    {
        Placed,
        Disallowed,
        NoSpace
    }

    // Size-aware Poisson disk sampling by dart throwing: every accepted disc keeps at least
    // `gap` meters from every other disc, whatever their radii.
    public class ScatterSampler
    {
        private readonly Rect area;
        private readonly float gap;
        private readonly float cellSize;
        private readonly ClusterMask mask;
        private readonly System.Random rng;
        private readonly int attempts;
        // Spatial hash of placed discs, stored as (x, z, radius).
        private readonly Dictionary<Vector2Int, List<Vector3>> cells = new Dictionary<Vector2Int, List<Vector3>>();

        public ScatterSampler(Rect area, float maxRadius, float gap, ClusterMask mask, System.Random rng, int attempts)
        {
            this.area = area;
            this.gap = gap;
            this.mask = mask;
            this.rng = rng;
            this.attempts = attempts;
            // Two discs can only overlap if their centers are closer than this, so any
            // overlap is always within the 3x3 block of cells that IsFree checks.
            cellSize = 2f * maxRadius + gap;
        }

        // A point that fails `isAllowed` (off the terrain, inside a clear zone) is dropped rather
        // than retried, so the density stays the same over the ground that is allowed.
        public ScatterResult TryPlace(float radius, Func<Vector2, float, bool> isAllowed, out Vector2 point)
        {
            for (int i = 0; i < attempts; i++)
            {
                point = RandomPoint();
                if (!isAllowed(point, radius))
                {
                    return ScatterResult.Disallowed;
                }
                if (rng.NextDouble() > mask.AcceptChance(point) || !IsFree(point, radius, gap))
                {
                    continue;
                }

                Insert(point, radius);
                return ScatterResult.Placed;
            }

            point = default;
            return ScatterResult.NoSpace;
        }

        public Vector2 RandomPoint()
        {
            return new Vector2(
                area.xMin + (float)rng.NextDouble() * area.width,
                area.yMin + (float)rng.NextDouble() * area.height);
        }

        public bool IsFree(Vector2 point, float radius)
        {
            return IsFree(point, radius, 0f);
        }

        private bool IsFree(Vector2 point, float radius, float spacing)
        {
            Vector2Int center = CellOf(point);
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (!cells.TryGetValue(new Vector2Int(center.x + x, center.y + y), out List<Vector3> discs))
                    {
                        continue;
                    }

                    foreach (Vector3 disc in discs)
                    {
                        float reach = radius + disc.z + spacing;
                        if ((new Vector2(disc.x, disc.y) - point).sqrMagnitude < reach * reach)
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private void Insert(Vector2 point, float radius)
        {
            Vector2Int cell = CellOf(point);
            if (!cells.TryGetValue(cell, out List<Vector3> discs))
            {
                discs = new List<Vector3>();
                cells.Add(cell, discs);
            }
            discs.Add(new Vector3(point.x, point.y, radius));
        }

        private Vector2Int CellOf(Vector2 point)
        {
            return new Vector2Int(Mathf.FloorToInt(point.x / cellSize), Mathf.FloorToInt(point.y / cellSize));
        }
    }
}
