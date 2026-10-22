using UnityEngine;

namespace Rocks
{
    // Mesh extents of a prefab in its own root space at scale 1, measured from the mesh data
    // so it works on prefab assets that were never instantiated.
    public readonly struct RockShape
    {
        private static readonly RockShape UnitCube = new RockShape(1f, -0.5f, 0.5f);

        public readonly float Height;
        public readonly float BottomY;
        public readonly float FootprintRadius;

        private RockShape(float height, float bottomY, float footprintRadius)
        {
            Height = height;
            BottomY = bottomY;
            FootprintRadius = footprintRadius;
        }

        public static RockShape Measure(GameObject prefab)
        {
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            Bounds bounds = default;
            bool found = false;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Matrix4x4 meshToRoot = toRoot * filter.transform.localToWorldMatrix;
                Bounds mesh = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 point = meshToRoot.MultiplyPoint3x4(corner);

                    if (found)
                    {
                        bounds.Encapsulate(point);
                    }
                    else
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        found = true;
                    }
                }
            }

            if (!found || bounds.size.y < 1e-4f)
            {
                Debug.LogWarning($"[RockShape] '{prefab.name}' has no usable mesh; assuming a 1 m cube centered on its pivot.");
                return UnitCube;
            }

            // Farthest side of the bounding box from the pivot. That suits roughly round rocks; a
            // boxy rock's corners can reach past it, letting neighbors overlap slightly there.
            float footprint = Mathf.Max(
                Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)),
                Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)));
            return new RockShape(bounds.size.y, bounds.min.y, footprint);
        }
    }
}
