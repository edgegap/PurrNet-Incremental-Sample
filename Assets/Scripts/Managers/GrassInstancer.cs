using System;
using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProgressiveSample
{
    public class GrassInstancer : MonoBehaviour
    {
        [Header("Grass Settings")]
        public Mesh grassMesh;
        public Material grassMaterial;
        
        [Header("Distribution")]
        [Range(1f, 100f)] public float radius = 30f;
        [Range(0.1f, 5f)] public float minDistance = 0.5f;
        [Range(0.01f, 1f)] public float density = 0.3f;
        [Range(0f, 1f)] public float noiseScale = 0.1f;
        [Range(0f, 1f)] public float noiseThreshold = 0.3f;
        
        [Header("Appearance")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
        public ShadowCastingMode shadowCasting = ShadowCastingMode.On;
        public bool receiveShadows = true;
        public float groundHeight = 0f;
        
        [Header("Performance")]
        [Range(1, 20)] public int cellSize = 5;
        
        private Transform player;
        private Matrix4x4[] matrices;
        private RenderParams renderParams;
        private Vector3 lastPlayerCell;
        private int instanceCount;
        private const int MAX_INSTANCES_PER_BATCH = 1023;
        private bool needsRebuild;

        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
        }

        private void OnDestroy()
        {
            InstanceHandler.UnregisterInstance<GrassInstancer>();
        }

        public void Initialize(Transform target)
        {
            player = target;
            if (!player)
                player = Camera.main?.transform;
            
            int maxInstances = Mathf.CeilToInt((radius * 2f / minDistance) * (radius * 2f / minDistance) * density);
            matrices = new Matrix4x4[Mathf.Min(maxInstances, MAX_INSTANCES_PER_BATCH * 10)];
            
            renderParams = new RenderParams(grassMaterial)
            {
                shadowCastingMode = shadowCasting,
                receiveShadows = receiveShadows,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * radius * 3f)
            };
            
            lastPlayerCell = Vector3.one * float.MaxValue;
            needsRebuild = false;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            needsRebuild = true;
        }
#endif

        void Update()
        {
            if (player == null) return;

            if (needsRebuild)
            {
                Initialize(player);
            }
            
            Vector3 currentCell = new Vector3(
                Mathf.Floor(player.position.x / cellSize),
                0,
                Mathf.Floor(player.position.z / cellSize)
            );
            
            if (currentCell != lastPlayerCell)
            {
                lastPlayerCell = currentCell;
                GenerateGrassPositions();
            }

            if (instanceCount == 0) return;
            
            renderParams.worldBounds = new Bounds(player.position, Vector3.one * radius * 3f);
            
            int rendered = 0;
            while (rendered < instanceCount)
            {
                int batch = Mathf.Min(MAX_INSTANCES_PER_BATCH, instanceCount - rendered);
                Graphics.RenderMeshInstanced(renderParams, grassMesh, 0, matrices, batch, rendered);
                rendered += batch;
            }
        }

        void GenerateGrassPositions()
        {
            Vector3 playerPos = player.position;
            float radiusSq = radius * radius;
            float step = minDistance / Mathf.Max(density, 0.01f);
            
            instanceCount = 0;
            
            int minGridX = Mathf.FloorToInt((playerPos.x - radius) / step);
            int maxGridX = Mathf.CeilToInt((playerPos.x + radius) / step);
            int minGridZ = Mathf.FloorToInt((playerPos.z - radius) / step);
            int maxGridZ = Mathf.CeilToInt((playerPos.z + radius) / step);
            
            for (int gx = minGridX; gx <= maxGridX && instanceCount < matrices.Length; gx++)
            {
                for (int gz = minGridZ; gz <= maxGridZ && instanceCount < matrices.Length; gz++)
                {
                    float baseX = gx * step;
                    float baseZ = gz * step;
                    
                    uint hash = Hash(gx, gz);
                    
                    float noise01 = (hash & 0xFFFF) / 65535f;
                    if (noise01 < noiseThreshold) continue;
                    
                    float jitterX = (((hash >> 8) & 0xFFFF) / 65535f - 0.5f) * step * 0.8f;
                    float jitterZ = (((hash >> 16) & 0xFFFF) / 65535f - 0.5f) * step * 0.8f;
                    
                    float finalX = baseX + jitterX;
                    float finalZ = baseZ + jitterZ;
                    
                    float dx = finalX - playerPos.x;
                    float dz = finalZ - playerPos.z;
                    if (dx * dx + dz * dz > radiusSq) continue;
                    
                    uint hash2 = Hash(gx + 7919, gz + 4391);
                    float rotation = (hash2 & 0xFFFF) / 65535f * 360f;
                    float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, ((hash2 >> 16) & 0xFFFF) / 65535f);
                    
                    matrices[instanceCount++] = Matrix4x4.TRS(
                        new Vector3(finalX, groundHeight, finalZ),
                        Quaternion.Euler(0, rotation, 0),
                        Vector3.one * scale
                    );
                }
            }
        }

        uint Hash(int x, int z)
        {
            uint h = (uint)(x * 374761393 + z * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }

        void OnDrawGizmosSelected()
        {
            Transform t = player != null ? player : transform;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(t.position, radius);
        }
    }
}