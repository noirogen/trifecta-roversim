using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Rocks
{
    // Copies of the rock prefabs with every visual component removed. Rocks are spawned from
    // these, so each one carries only its colliders while InstancedRenderer draws it.
    public sealed class ColliderTemplates : IDisposable
    {
        private readonly GameObject holder;
        private readonly Dictionary<GameObject, GameObject> templates = new Dictionary<GameObject, GameObject>();

        public ColliderTemplates()
        {
            holder = new GameObject("RockColliderTemplates") { hideFlags = HideFlags.HideAndDontSave };
            holder.SetActive(false);
        }

        public GameObject For(GameObject prefab)
        {
            if (!templates.TryGetValue(prefab, out GameObject template))
            {
                template = Object.Instantiate(prefab, holder.transform);
                template.name = prefab.name;
                StripVisuals(template);
                templates.Add(prefab, template);
            }
            return template;
        }

        public void Dispose()
        {
            if (holder != null)
            {
                Object.DestroyImmediate(holder);
            }
        }

        // DestroyImmediate, because the template is copied straight away and a deferred
        // Destroy would still leave the renderers in place at that point.
        private static void StripVisuals(GameObject template)
        {
            foreach (LODGroup lodGroup in template.GetComponentsInChildren<LODGroup>(true))
            {
                Object.DestroyImmediate(lodGroup);
            }
            foreach (Renderer renderer in template.GetComponentsInChildren<Renderer>(true))
            {
                Object.DestroyImmediate(renderer);
            }
            foreach (MeshFilter filter in template.GetComponentsInChildren<MeshFilter>(true))
            {
                Object.DestroyImmediate(filter);
            }
        }
    }
}
