using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class ResourceSpawner : NetworkIdentity
    {
        [Header("Layers")]
        public SpawnLayer[] layers;

        [Header("Spawn Settings")]
        public float spawnRadius = 50f;
        public float despawnRadius = 100f;
        public int maxSpawnsPerFrame = 5;
        public float spawnInterval = 0.5f;

        [Header("Density")]
        public float noiseScale = 0.05f;
        public float densityThreshold = 0.4f;
        public float minSpacing = 3f;

        [Header("Respawn Settings")]
        public float respawnDelay = 30f;

        [Header("Validation")]
        public LayerMask overlapMask;
        public int maxPlacementAttempts = 10;

        private readonly List<Transform> _players = new();
        private readonly List<Transform> _pendingBurst = new();
        private readonly List<SpawnExclusionZone> _exclusionZones = new();
        private readonly HashSet<Vector2Int> _occupiedCells = new();
        private readonly HashSet<Vector2Int> _attemptedCells = new();
        private readonly List<SpawnedResource> _spawnedResources = new();
        private readonly List<float> _layerWeights = new();

        private Collider[] _overlapBuffer = new Collider[16];
        private float _spawnTimer;
        private readonly Queue<float> _respawnQueue = new();
        private float _noiseOffsetX;
        private float _noiseOffsetZ;

        private bool _initialized;

        private struct SpawnedResource
        {
            public GameObject Instance;
            public Vector2Int Cell;
        }

        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();

            if (isServer)
                InitSeed(Random.Range(-10000, 10000));
        }

        [ObserversRpc(bufferLast: true)]
        private void InitSeed(int seed)
        {
            Random.InitState(seed);
            _noiseOffsetX = Random.Range(0f, 10000f);
            _noiseOffsetZ = Random.Range(0f, 10000f);
            _initialized = true;

            for (int i = 0; i < _pendingBurst.Count; i++)
            {
                BurstSpawnAroundPlayer(_pendingBurst[i]);
            }
            _pendingBurst.Clear();
        }

        private void OnDestroy()
        {
            InstanceHandler.UnregisterInstance<ResourceSpawner>();
        }

        public void RegisterPlayer(Transform player)
        {
            if (_players.Contains(player)) return;

            _players.Add(player);

            if (_initialized)
                BurstSpawnAroundPlayer(player);
            else
                _pendingBurst.Add(player);
        }

        public void UnregisterPlayer(Transform player)
        {
            _players.Remove(player);
            _pendingBurst.Remove(player);
        }

        public void RegisterExclusionZone(SpawnExclusionZone zone)
        {
            if (!_exclusionZones.Contains(zone))
                _exclusionZones.Add(zone);
        }

        public void UnregisterExclusionZone(SpawnExclusionZone zone)
        {
            _exclusionZones.Remove(zone);
        }

        public void OnResourceHarvested(GameObject resource)
        {
            for (int i = _spawnedResources.Count - 1; i >= 0; i--)
            {
                if (_spawnedResources[i].Instance == resource)
                {
                    Vector2Int cell = _spawnedResources[i].Cell;
                    _occupiedCells.Remove(cell);
                    _attemptedCells.Remove(cell);
                    _spawnedResources.RemoveAt(i);
                    _respawnQueue.Enqueue(Time.time + respawnDelay);
                    break;
                }
            }
        }

        private void BurstSpawnAroundPlayer(Transform player)
        {
            Vector3 playerPos = player.position;
            int cellRadius = Mathf.CeilToInt(spawnRadius / minSpacing);
            Vector2Int playerCell = WorldToCell(playerPos);

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector2Int cell = new Vector2Int(playerCell.x + x, playerCell.y + z);
                    Vector3 worldPos = CellToWorld(cell);

                    float distSqr = (worldPos - playerPos).sqrMagnitude;
                    if (distSqr > spawnRadius * spawnRadius)
                        continue;

                    TrySpawnAtCell(cell);
                }
            }
        }

        private void Update()
        {
            if (!_initialized) return;
            if (_players.Count == 0) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = spawnInterval;
                ProcessSpawning();
            }

            CleanupDistantResources();
        }

        private void ProcessSpawning()
        {
            int spawned = 0;

            while (_respawnQueue.Count > 0 && _respawnQueue.Peek() <= Time.time && spawned < maxSpawnsPerFrame)
            {
                _respawnQueue.Dequeue();
                
                for (int p = 0; p < _players.Count; p++)
                {
                    Transform player = _players[p];
                    Vector3 playerPos = player.position;

                    for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
                    {
                        Vector2 offset = Random.insideUnitCircle * spawnRadius;
                        Vector3 worldPos = new Vector3(playerPos.x + offset.x, 0f, playerPos.z + offset.y);
                        Vector2Int cell = WorldToCell(worldPos);

                        if (TrySpawnAtCell(cell))
                        {
                            spawned++;
                            goto nextRespawn;
                        }
                    }
                }
                
                nextRespawn:;
            }
        }

        private bool TrySpawnAtCell(Vector2Int cell)
        {
            if (_occupiedCells.Contains(cell))
                return false;

            if (_attemptedCells.Contains(cell))
                return false;

            _attemptedCells.Add(cell);

            Vector3 position = CellToWorld(cell);

            float noise = Mathf.PerlinNoise(
                position.x * noiseScale + _noiseOffsetX,
                position.z * noiseScale + _noiseOffsetZ
            );

            if (noise < densityThreshold)
                return false;

            if (IsInExclusionZone(position))
                return false;

            SpawnLayer layer = SelectLayer(position);
            if (layer == null)
                return false;

            if (!IsPositionClear(position, layer.minDistanceBetween))
                return false;

            GameObject prefab = layer.GetPrefab();
            if (prefab == null)
                return false;

            Vector3 jitter = new Vector3(
                Random.Range(-minSpacing * 0.4f, minSpacing * 0.4f),
                0f,
                Random.Range(-minSpacing * 0.4f, minSpacing * 0.4f)
            );
            Vector3 finalPos = position + jitter;

            Quaternion rotation = layer.GetRotation(finalPos);
            Vector3 scale = layer.GetScale(finalPos);

            GameObject instance = Instantiate(prefab, finalPos, rotation);
            instance.transform.localScale = scale;

            _occupiedCells.Add(cell);
            _spawnedResources.Add(new SpawnedResource
            {
                Instance = instance,
                Cell = cell
            });

            return true;
        }

        private SpawnLayer SelectLayer(Vector3 position)
        {
            float distanceFromCenter = new Vector2(position.x, position.z).magnitude;

            SpawnContext context = new SpawnContext
            {
                Position = position,
                DistanceFromCenter = distanceFromCenter,
                GameTime = Time.time
            };

            _layerWeights.Clear();
            float totalWeight = 0f;

            for (int i = 0; i < layers.Length; i++)
            {
                float weight = layers[i].GetWeight(context);
                _layerWeights.Add(weight);
                totalWeight += weight;
            }

            if (totalWeight <= 0f)
                return null;

            float random = Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < layers.Length; i++)
            {
                cumulative += _layerWeights[i];
                if (random <= cumulative)
                    return layers[i];
            }

            return layers[layers.Length - 1];
        }

        private bool IsInExclusionZone(Vector3 position)
        {
            for (int i = 0; i < _exclusionZones.Count; i++)
            {
                SpawnExclusionZone zone = _exclusionZones[i];
                if (zone == null) continue;

                Vector3 diff = zone.transform.position - position;
                if (diff.sqrMagnitude <= zone.radius * zone.radius)
                    return true;
            }

            return false;
        }

        private bool IsPositionClear(Vector3 position, float minDistance)
        {
            int count = Physics.OverlapSphereNonAlloc(position, minDistance, _overlapBuffer, overlapMask);
            return count == 0;
        }

        private void CleanupDistantResources()
        {
            float despawnRadiusSqr = despawnRadius * despawnRadius;

            for (int i = _spawnedResources.Count - 1; i >= 0; i--)
            {
                SpawnedResource resource = _spawnedResources[i];

                if (resource.Instance == null)
                {
                    _occupiedCells.Remove(resource.Cell);
                    _attemptedCells.Remove(resource.Cell);
                    _spawnedResources.RemoveAt(i);
                    continue;
                }

                Vector3 resourcePos = resource.Instance.transform.position;
                bool nearAnyPlayer = false;

                for (int p = 0; p < _players.Count; p++)
                {
                    Vector3 diff = _players[p].position - resourcePos;
                    if (diff.sqrMagnitude <= despawnRadiusSqr)
                    {
                        nearAnyPlayer = true;
                        break;
                    }
                }

                if (!nearAnyPlayer)
                {
                    _occupiedCells.Remove(resource.Cell);
                    _attemptedCells.Remove(resource.Cell);
                    Destroy(resource.Instance);
                    _spawnedResources.RemoveAt(i);
                }
            }
        }

        private Vector2Int WorldToCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / minSpacing),
                Mathf.FloorToInt(position.z / minSpacing)
            );
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                (cell.x + 0.5f) * minSpacing,
                0f,
                (cell.y + 0.5f) * minSpacing
            );
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, despawnRadius);
        }
    }
}