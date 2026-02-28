using UnityEngine;

namespace ProgressiveSample
{
    public abstract class SpawnLayer : ScriptableObject
    {
        [Header("Prefabs")]
        public GameObject[] prefabs;
    
        [Header("Spacing")]
        public float minDistanceBetween = 2f;
        public float exclusionRadius = 1f;
    
        public abstract float GetWeight(SpawnContext context);
    
        public virtual GameObject GetPrefab()
        {
            if (prefabs == null || prefabs.Length == 0) return null;
            return prefabs[Random.Range(0, prefabs.Length)];
        }
    
        public virtual Quaternion GetRotation(Vector3 position)
        {
            return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }
    
        public virtual Vector3 GetScale(Vector3 position)
        {
            return Vector3.one;
        }
    }
}
