using UnityEngine;

// Noticed that there was some jitteriness after adding suspension colliders; this is to make sure that the rover doesn't collide with itself since self collision only happen due to hitbox inaccuracies.

namespace Rover
{
    public class RoverSelfCollision : MonoBehaviour
    {
        [SerializeField] private Transform root;

        private void Awake()
        {
            var colliders = (root != null ? root : transform).GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                for (int j = i + 1; j < colliders.Length; j++)
                    Physics.IgnoreCollision(colliders[i], colliders[j]);
            }
        }
    }
}
