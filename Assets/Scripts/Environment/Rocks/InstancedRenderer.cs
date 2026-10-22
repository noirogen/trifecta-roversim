using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Rocks
{
    // Draws many copies of a few prefabs with GPU instancing instead of a renderer per object.
    // Instances are grouped into spatial chunks for culling. Each frame, every camera collects
    // what it can see from the nearby chunks and draws it in a few large batches per mesh.
    // Within a chunk, instances are sorted largest first, so leaving out the ones too small to
    // see is just taking fewer from the front, and each Mesh LOD level is a slice of what is left.
    [ExecuteAlways]
    public class InstancedRenderer : MonoBehaviour
    {
        // Unity's limit on instances per RenderMeshInstanced call.
        private const int MaxBatchSize = 1023;

        // Chunks only decide culling, not draw calls. Smaller chunks cull more precisely but
        // make the per-frame chunk loop longer.
        private const float TargetInstancesPerChunk = 256f;
        private const float MinChunkSize = 16f;
        private const float MaxChunkSize = 64f;

        private readonly Dictionary<GameObject, MeshPart[]> partsByPrefab = new Dictionary<GameObject, MeshPart[]>();
        private readonly List<MeshPart> parts = new List<MeshPart>();
        private readonly Plane[] frustum = new Plane[6];
        private float chunkSize = MaxChunkSize;
        private float drawDistance = float.PositiveInfinity;
        private float minScreenPixels;
        private float lod0ScreenPixels;

        public static float ChunkSizeFor(int instanceCount, float area, int prefabCount)
        {
            float perPrefabDensity = instanceCount / Mathf.Max(area, 1f) / Mathf.Max(prefabCount, 1);
            if (perPrefabDensity <= 0f)
            {
                return MaxChunkSize;
            }
            return Mathf.Clamp(Mathf.Sqrt(TargetInstancesPerChunk / perPrefabDensity), MinChunkSize, MaxChunkSize);
        }

        public void Initialize(float chunkSize, float drawDistance, float minScreenPixels, float lod0ScreenPixels)
        {
            this.chunkSize = chunkSize;
            this.drawDistance = drawDistance;
            this.minScreenPixels = minScreenPixels;
            this.lod0ScreenPixels = lod0ScreenPixels;
        }

        public void Add(GameObject prefab, Matrix4x4 rootToWorld)
        {
            foreach (MeshPart part in PartsOf(prefab))
            {
                part.Add(rootToWorld * part.MeshToRoot, chunkSize);
            }
        }

        public void Build()
        {
            parts.Clear();
            foreach (MeshPart[] prefabParts in partsByPrefab.Values)
            {
                foreach (MeshPart part in prefabParts)
                {
                    if (part.Build())
                    {
                        parts.Add(part);
                    }
                }
            }
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Draw;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Draw;
        }

        private void Draw(ScriptableRenderContext context, Camera camera)
        {
            if (parts.Count == 0 || camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection)
            {
                return;
            }

            GeometryUtility.CalculateFrustumPlanes(camera, frustum);
            Vector3 eye = camera.transform.position;
            float shadowDistance = ShadowDistance();
            float pixelsPerRadius = PixelsPerRadius(camera);

            foreach (MeshPart part in parts)
            {
                if ((camera.cullingMask & (1 << part.Layer)) == 0)
                {
                    continue;
                }

                part.ClearLists();
                Collect(part, eye, shadowDistance, pixelsPerRadius);

                for (int lod = 0; lod < part.LodCount; lod++)
                {
                    Submit(part, lod, part.ListFor(DrawPass.WithShadows, lod), part.ShadowMode, camera);
                    Submit(part, lod, part.ListFor(DrawPass.WithoutShadows, lod), ShadowCastingMode.Off, camera);
                    Submit(part, lod, part.ListFor(DrawPass.ShadowsOnly, lod), ShadowCastingMode.ShadowsOnly, camera);
                }
            }
        }

        private void Collect(MeshPart part, Vector3 eye, float shadowDistance, float pixelsPerRadius)
        {
            KeyRange(eye.x, part.KeyMin.x, part.KeyMax.x, out int xFrom, out int xTo);
            KeyRange(eye.z, part.KeyMin.y, part.KeyMax.y, out int zFrom, out int zTo);
            float maxSqrDistance = drawDistance * drawDistance;

            for (int x = xFrom; x <= xTo; x++)
            {
                for (int z = zFrom; z <= zTo; z++)
                {
                    if (!part.TryGetChunk(new Vector2Int(x, z), out Chunk chunk))
                    {
                        continue;
                    }

                    float sqrDistance = chunk.Bounds.SqrDistance(eye);
                    if (sqrDistance > maxSqrDistance)
                    {
                        continue;
                    }

                    float distance = Mathf.Sqrt(sqrDistance);
                    int count = chunk.CountAtLeast(RadiusForPixels(minScreenPixels, distance, pixelsPerRadius));
                    if (count == 0)
                    {
                        continue;
                    }

                    // A chunk that is off screen can still throw shadows into view, so it goes
                    // into the shadow maps only, and only while it is inside the shadow distance.
                    bool onScreen = GeometryUtility.TestPlanesAABB(frustum, chunk.Bounds);
                    bool castsHere = part.ShadowMode != ShadowCastingMode.Off && distance <= shadowDistance;
                    if (!onScreen && !castsHere)
                    {
                        continue;
                    }

                    DrawPass pass = !onScreen ? DrawPass.ShadowsOnly
                        : castsHere ? DrawPass.WithShadows
                        : DrawPass.WithoutShadows;
                    AppendByLod(part, pass, chunk, count, distance, pixelsPerRadius);
                }
            }
        }

        // Instances are sorted largest first, so each level is the slice of instances that are
        // still bigger on screen than the size where the next level takes over.
        private void AppendByLod(MeshPart part, DrawPass pass, Chunk chunk, int count, float distance, float pixelsPerRadius)
        {
            int start = 0;
            for (int lod = 0; lod < part.LodCount && start < count; lod++)
            {
                int end = count;
                if (lod < part.LodCount - 1)
                {
                    float nextLevelPixels = lod0ScreenPixels * part.LodStartScale[lod + 1];
                    end = Mathf.Clamp(chunk.CountAtLeast(RadiusForPixels(nextLevelPixels, distance, pixelsPerRadius)), start, count);
                }

                if (end > start)
                {
                    part.ListFor(pass, lod).Append(chunk, start, end - start);
                }
                start = end;
            }
        }

        // The chunk keys within the draw distance of `center` on one axis, clamped to the keys
        // that exist so an unlimited draw distance stays a finite loop.
        private void KeyRange(float center, int keyMin, int keyMax, out int from, out int to)
        {
            from = Mathf.FloorToInt(Mathf.Clamp((center - drawDistance) / chunkSize, keyMin, keyMax));
            to = Mathf.FloorToInt(Mathf.Clamp((center + drawDistance) / chunkSize, keyMin, keyMax));
        }

        private static void Submit(MeshPart part, int lod, DrawList list, ShadowCastingMode shadows, Camera camera)
        {
            if (list.Count == 0)
            {
                return;
            }

            foreach (SubmeshDraw draw in part.Draws)
            {
                RenderParams renderParams = draw.Params;
                renderParams.camera = camera;
                renderParams.worldBounds = list.Bounds;
                renderParams.shadowCastingMode = shadows;
                renderParams.forceMeshLod = lod;

                for (int start = 0; start < list.Count; start += MaxBatchSize)
                {
                    int count = Mathf.Min(MaxBatchSize, list.Count - start);
                    Graphics.RenderMeshInstanced(renderParams, part.Mesh, draw.Submesh, list.Matrices, count, start);
                }
            }
        }

        // A bounding sphere of radius r at a given distance spans r * pixelsPerRadius / distance
        // pixels on screen, from 2r / (2 * distance * tan(fov / 2)) * pixelHeight.
        // Zero means sizes are not compared, as with orthographic cameras.
        private static float PixelsPerRadius(Camera camera)
        {
            if (camera.orthographic)
            {
                return 0f;
            }
            float tanHalfFov = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return camera.pixelHeight / tanHalfFov;
        }

        // The smallest radius that still spans `pixels` on screen at this distance.
        private static float RadiusForPixels(float pixels, float distance, float pixelsPerRadius)
        {
            return pixelsPerRadius > 0f && pixels > 0f ? pixels * distance / pixelsPerRadius : 0f;
        }

        private static float ShadowDistance()
        {
            return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp
                ? urp.shadowDistance
                : QualitySettings.shadowDistance;
        }

        private MeshPart[] PartsOf(GameObject prefab)
        {
            if (partsByPrefab.TryGetValue(prefab, out MeshPart[] parts))
            {
                return parts;
            }

            var found = new List<MeshPart>();
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix;
            foreach (Renderer renderer in DrawnRenderers(prefab))
            {
                if (!(renderer is MeshRenderer) || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                {
                    continue;
                }

                var part = new MeshPart(filter.sharedMesh, renderer, toRoot * filter.transform.localToWorldMatrix, filter.gameObject.layer);
                if (part.Draws.Length > 0)
                {
                    found.Add(part);
                }
            }

            if (found.Count == 0)
            {
                Debug.LogWarning($"[InstancedRenderer] '{prefab.name}' has no drawable mesh; its copies will be invisible.");
            }

            parts = found.ToArray();
            partsByPrefab.Add(prefab, parts);
            return parts;
        }

        // With a LODGroup, only the most detailed level is drawn; otherwise every level would
        // be drawn on top of each other.
        private static IEnumerable<Renderer> DrawnRenderers(GameObject prefab)
        {
            LODGroup lodGroup = prefab.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null || lodGroup.lodCount == 0)
            {
                return prefab.GetComponentsInChildren<Renderer>(true);
            }

            var renderers = new List<Renderer>();
            foreach (Renderer renderer in lodGroup.GetLODs()[0].renderers)
            {
                if (renderer != null)
                {
                    renderers.Add(renderer);
                }
            }
            return renderers;
        }

        private const int PassCount = 3;

        private enum DrawPass
        {
            WithShadows,
            WithoutShadows,
            ShadowsOnly
        }

        private readonly struct SubmeshDraw
        {
            public readonly int Submesh;
            public readonly RenderParams Params;

            public SubmeshDraw(int submesh, RenderParams renderParams)
            {
                Submesh = submesh;
                Params = renderParams;
            }
        }

        private sealed class Chunk
        {
            public readonly Matrix4x4[] Matrices;
            public readonly Bounds Bounds;
            private readonly float[] radii;

            public Chunk(Matrix4x4[] matrices, float[] radii, Bounds bounds)
            {
                Matrices = matrices;
                Bounds = bounds;
                this.radii = radii;
            }

            // Radii are sorted largest first, so this is the length of the prefix worth drawing.
            public int CountAtLeast(float minRadius)
            {
                int low = 0;
                int high = radii.Length;
                while (low < high)
                {
                    int mid = (low + high) / 2;
                    if (radii[mid] >= minRadius)
                    {
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid;
                    }
                }
                return low;
            }
        }

        // Reused every frame so collecting visible instances allocates nothing once it has grown.
        // Each mesh has its own lists, so one mesh's draw data is never overwritten by the next.
        private sealed class DrawList
        {
            public Matrix4x4[] Matrices = new Matrix4x4[MaxBatchSize];
            public Bounds Bounds;
            public int Count;

            public void Clear()
            {
                Count = 0;
            }

            public void Append(Chunk chunk, int start, int count)
            {
                if (Count + count > Matrices.Length)
                {
                    Array.Resize(ref Matrices, Mathf.Max(Matrices.Length * 2, Count + count));
                }

                Array.Copy(chunk.Matrices, start, Matrices, Count, count);
                if (Count == 0)
                {
                    Bounds = chunk.Bounds;
                }
                else
                {
                    Bounds.Encapsulate(chunk.Bounds);
                }
                Count += count;
            }
        }

        private sealed class MeshPart
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 MeshToRoot;
            public readonly int Layer;
            public readonly ShadowCastingMode ShadowMode;
            public readonly SubmeshDraw[] Draws;
            public readonly int LodCount;

            // Where each level takes over, as a fraction of lod0ScreenPixels. A level is used once
            // the instance is small enough that the level's triangles per pixel match what LOD0
            // has at lod0ScreenPixels: screen area scales with triangles, so size with their root.
            public readonly float[] LodStartScale;

            private readonly DrawList[] lists;

            private readonly Dictionary<Vector2Int, List<Matrix4x4>> pending = new Dictionary<Vector2Int, List<Matrix4x4>>();
            private readonly Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

            public MeshPart(Mesh mesh, Renderer renderer, Matrix4x4 meshToRoot, int layer)
            {
                Mesh = mesh;
                MeshToRoot = meshToRoot;
                Layer = layer;
                ShadowMode = renderer.shadowCastingMode;
                Draws = CreateDraws(mesh, renderer, layer);
                LodCount = Mathf.Max(1, mesh.lodCount);
                LodStartScale = LodStartScales(mesh, LodCount, Draws);

                lists = new DrawList[PassCount * LodCount];
                for (int i = 0; i < lists.Length; i++)
                {
                    lists[i] = new DrawList();
                }
            }

            private static float[] LodStartScales(Mesh mesh, int lodCount, SubmeshDraw[] draws)
            {
                var scales = new float[lodCount];
                float lod0Triangles = TrianglesIn(mesh, 0, draws);
                for (int lod = 0; lod < lodCount; lod++)
                {
                    scales[lod] = lod0Triangles > 0f ? Mathf.Sqrt(TrianglesIn(mesh, lod, draws) / lod0Triangles) : 1f;
                }
                return scales;
            }

            private static float TrianglesIn(Mesh mesh, int lod, SubmeshDraw[] draws)
            {
                if (mesh.lodCount <= 1)
                {
                    return 1f;
                }

                float triangles = 0f;
                foreach (SubmeshDraw draw in draws)
                {
                    triangles += mesh.GetLod(draw.Submesh, lod).indexCount / 3f;
                }
                return triangles;
            }

            public DrawList ListFor(DrawPass pass, int lod)
            {
                return lists[(int)pass * LodCount + lod];
            }

            public void ClearLists()
            {
                foreach (DrawList list in lists)
                {
                    list.Clear();
                }
            }

            public Vector2Int KeyMin { get; private set; }
            public Vector2Int KeyMax { get; private set; }

            public void Add(Matrix4x4 meshToWorld, float chunkSize)
            {
                var key = new Vector2Int(
                    Mathf.FloorToInt(meshToWorld.m03 / chunkSize),
                    Mathf.FloorToInt(meshToWorld.m23 / chunkSize));

                if (!pending.TryGetValue(key, out List<Matrix4x4> list))
                {
                    list = new List<Matrix4x4>();
                    pending.Add(key, list);
                }
                list.Add(meshToWorld);
            }

            public bool TryGetChunk(Vector2Int key, out Chunk chunk)
            {
                return chunks.TryGetValue(key, out chunk);
            }

            // Turns everything added so far into sorted chunks. Returns false when there is nothing to draw.
            public bool Build()
            {
                Bounds meshBounds = Mesh.bounds;
                float meshRadius = meshBounds.extents.magnitude;
                var keyMin = new Vector2Int(int.MaxValue, int.MaxValue);
                var keyMax = new Vector2Int(int.MinValue, int.MinValue);

                foreach (KeyValuePair<Vector2Int, List<Matrix4x4>> entry in pending)
                {
                    Matrix4x4[] matrices = entry.Value.ToArray();
                    float[] radii = new float[matrices.Length];
                    for (int i = 0; i < matrices.Length; i++)
                    {
                        radii[i] = -RadiusOf(matrices[i], meshRadius);
                    }
                    Array.Sort(radii, matrices);
                    for (int i = 0; i < radii.Length; i++)
                    {
                        radii[i] = -radii[i];
                    }

                    chunks[entry.Key] = new Chunk(matrices, radii, BoundsOf(matrices, radii, meshBounds.center));
                    keyMin = Vector2Int.Min(keyMin, entry.Key);
                    keyMax = Vector2Int.Max(keyMax, entry.Key);
                }

                pending.Clear();
                KeyMin = keyMin;
                KeyMax = keyMax;
                return chunks.Count > 0;
            }

            private static float RadiusOf(Matrix4x4 meshToWorld, float meshRadius)
            {
                Vector3 scale = meshToWorld.lossyScale;
                return meshRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            }

            private static Bounds BoundsOf(Matrix4x4[] matrices, float[] radii, Vector3 meshCenter)
            {
                Bounds bounds = default;
                for (int i = 0; i < matrices.Length; i++)
                {
                    var instance = new Bounds(matrices[i].MultiplyPoint3x4(meshCenter), Vector3.one * (2f * radii[i]));
                    if (i == 0)
                    {
                        bounds = instance;
                    }
                    else
                    {
                        bounds.Encapsulate(instance);
                    }
                }
                return bounds;
            }

            private static SubmeshDraw[] CreateDraws(Mesh mesh, Renderer renderer, int layer)
            {
                var draws = new List<SubmeshDraw>();
                Material[] materials = renderer.sharedMaterials;
                int count = Mathf.Min(mesh.subMeshCount, materials.Length);

                for (int i = 0; i < count; i++)
                {
                    Material material = materials[i];
                    if (material == null)
                    {
                        continue;
                    }
                    if (!material.enableInstancing)
                    {
                        Debug.LogWarning($"[InstancedRenderer] Turn on 'Enable GPU Instancing' on material '{material.name}' to draw with it.");
                        continue;
                    }

                    draws.Add(new SubmeshDraw(i, new RenderParams(material)
                    {
                        layer = layer,
                        receiveShadows = renderer.receiveShadows
                    }));
                }
                return draws.ToArray();
            }
        }
    }
}
