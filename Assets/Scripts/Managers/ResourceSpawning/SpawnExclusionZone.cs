using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class SpawnExclusionZone : MonoBehaviour
    {
        public float radius = 5f;
    
        private void Start()
        {
            if (InstanceHandler.TryGetInstance(out ResourceSpawner spawner))
                spawner.RegisterExclusionZone(this);
        }
    
        private void OnDestroy()
        {
            if (InstanceHandler.TryGetInstance(out ResourceSpawner spawner))
                spawner.UnregisterExclusionZone(this);
        }
    
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(transform.position, radius);
        }
    }
}
