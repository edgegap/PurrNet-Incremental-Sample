using UnityEngine;

namespace ProgressiveSample
{
    [CreateAssetMenu(menuName = "Spawning/Layers/Tree Layer")]
    public class TreeSpawnLayer : SpawnLayer
    {
        [Header("Weight Settings")]
        public float baseWeight = 1f;
        public float minDistance = 0f;
        public float maxDistance = float.MaxValue;
    
        [Header("Scale Variation")]
        public float minScale = 0.8f;
        public float maxScale = 1.2f;
    
        public override float GetWeight(SpawnContext context)
        {
            if (context.DistanceFromCenter < minDistance || context.DistanceFromCenter > maxDistance)
                return 0f;
        
            return baseWeight;
        }
    
        public override Vector3 GetScale(Vector3 position)
        {
            float scale = Random.Range(minScale, maxScale);
            return new Vector3(scale, scale, scale);
        }
    }
}
