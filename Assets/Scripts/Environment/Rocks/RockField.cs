using UnityEngine;

namespace Rocks
{
    // The container that holds one target's rocks. Its local scale cancels the target's
    // world scale, so a rock's localScale under it equals its size in world meters.
    public class RockField
    {
        public const string RootName = "RockField";

        private RockField(Transform root, Transform rockColliders, InstancedRenderer rocks, InstancedRenderer pebbles)
        {
            Root = root;
            RockColliders = rockColliders;
            Rocks = rocks;
            Pebbles = pebbles;
        }

        public Transform Root { get; }
        public Transform RockColliders { get; }
        public InstancedRenderer Rocks { get; }
        public InstancedRenderer Pebbles { get; }

        // The field starts inactive so rocks register their colliders only once, already at
        // their final scale, when Activate is called.
        public static RockField Create(Transform target, DrawSettings rocks, DrawSettings pebbles)
        {
            var rootObject = new GameObject(RootName);
            rootObject.SetActive(false);

            Transform root = rootObject.transform;
            root.SetParent(target, false);
            root.localScale = InverseScale(target.lossyScale, target.name);

            Transform rockColliders = CreateChild(root, "RockColliders");
            return new RockField(
                root,
                rockColliders,
                CreateRenderer(root, "RockVisuals", rocks),
                CreateRenderer(root, "Pebbles", pebbles));
        }

        public void Activate()
        {
            Rocks.Build();
            Pebbles.Build();
            Root.gameObject.SetActive(true);
        }

        public static void DestroyOn(Transform target)
        {
            Transform root = target.Find(RootName);
            while (root != null)
            {
                // Destroy is deferred to the end of the frame. Deactivating removes the old colliders
                // from the next physics step now, and detaching stops Find from returning it again.
                root.gameObject.SetActive(false);
                root.SetParent(null);

                if (Application.isPlaying)
                {
                    Object.Destroy(root.gameObject);
                }
                else
                {
                    Object.DestroyImmediate(root.gameObject);
                }

                root = target.Find(RootName);
            }
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static InstancedRenderer CreateRenderer(Transform parent, string name, DrawSettings settings)
        {
            var renderer = CreateChild(parent, name).gameObject.AddComponent<InstancedRenderer>();
            renderer.Initialize(settings.ChunkSize, settings.DrawDistance, settings.MinScreenPixels, settings.Lod0ScreenPixels);
            return renderer;
        }

        public readonly struct DrawSettings
        {
            public readonly float ChunkSize;
            public readonly float DrawDistance;
            public readonly float MinScreenPixels;
            public readonly float Lod0ScreenPixels;

            public DrawSettings(float chunkSize, float drawDistance, float minScreenPixels, float lod0ScreenPixels)
            {
                ChunkSize = chunkSize;
                DrawDistance = drawDistance;
                MinScreenPixels = minScreenPixels;
                Lod0ScreenPixels = lod0ScreenPixels;
            }
        }

        private static Vector3 InverseScale(Vector3 scale, string targetName)
        {
            if (Mathf.Abs(scale.x) < 1e-6f || Mathf.Abs(scale.y) < 1e-6f || Mathf.Abs(scale.z) < 1e-6f)
            {
                Debug.LogWarning($"[RockField] '{targetName}' has a zero scale axis; rocks will not be scale-corrected.");
                return Vector3.one;
            }
            return new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        }
    }
}
