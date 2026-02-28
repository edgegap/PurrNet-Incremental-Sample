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
        private readonly Dictionary<Vector2Int, GameObject> _spawnedResources = new();
        private readonly Dictionary<Vector2Int, float> _respawnCells = new();
        private readonly List<float> _layerWeights = new();
        private readonly List<Vector2Int> _toRemoveCells = new();
        private readonly List<Vector2Int> _toCleanupCells = new();

        private Collider[] _overlapBuffer = new Collider[16];
        private float _spawnTimer;
        private float _noiseOffsetX;
        private float _noiseOffsetZ;
        private bool _initialized;

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
                BurstSpawnAroundPlayer(_pendingBurst[i]);
            _pendingBurst.Clear();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
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
            _toRemoveCells.Clear();
            foreach (var kvp in _spawnedResources)
            {
                if (kvp.Value == resource)
                {
                    _toRemoveCells.Add(kvp.Key);
                    break;
                }
            }

            if (_toRemoveCells.Count == 0) return;

            var cell = _toRemoveCells[0];
            _spawnedResources.Remove(cell);
            _occupiedCells.Remove(cell);
            _attemptedCells.Add(cell);
            _respawnCells[cell] = Time.time + respawnDelay;
        }

        private void BurstSpawnAroundPlayer(Transform player)
        {
            Vector3 playerPos = player.position;
            int cellRadius = Mathf.CeilToInt(spawnRadius / minSpacing);
            Vector2Int playerCell = WorldToCell(playerPos);
            float spawnRadiusSqr = spawnRadius * spawnRadius;

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int z = -cellRadius; z <= cellRadius; z++)
                {
                    Vector2Int cell = new Vector2Int(playerCell.x + x, playerCell.y + z);
                    if ((CellToWorld(cell) - playerPos).sqrMagnitude > spawnRadiusSqr)
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
                CleanupDistantResources();
            }
        }

        private void ProcessSpawning()
        {
            _toRemoveCells.Clear();
            foreach (var kvp in _respawnCells)
            {
                if (kvp.Value <= Time.time)
                    _toRemoveCells.Add(kvp.Key);
            }

            for (int i = 0; i < _toRemoveCells.Count; i++)
            {
                var cell = _toRemoveCells[i];
                _respawnCells.Remove(cell);
                _attemptedCells.Remove(cell);
            }

            int spawned = 0;
            float spawnRadiusSqr = spawnRadius * spawnRadius;

            for (int p = 0; p < _players.Count && spawned < maxSpawnsPerFrame; p++)
            {
                Vector3 playerPos = _players[p].position;
                int cellRadius = Mathf.CeilToInt(spawnRadius / minSpacing);
                Vector2Int playerCell = WorldToCell(playerPos);

                for (int x = -cellRadius; x <= cellRadius && spawned < maxSpawnsPerFrame; x++)
                {
                    for (int z = -cellRadius; z <= cellRadius && spawned < maxSpawnsPerFrame; z++)
                    {
                        Vector2Int cell = new Vector2Int(playerCell.x + x, playerCell.y + z);
                        if ((CellToWorld(cell) - playerPos).sqrMagnitude > spawnRadiusSqr) continue;
                        if (_occupiedCells.Contains(cell)) continue;
                        if (_attemptedCells.Contains(cell)) continue;
                        if (TrySpawnAtCell(cell)) spawned++;
                    }
                }
            }
        }

        private bool TrySpawnAtCell(Vector2Int cell)
        {
            if (_occupiedCells.Contains(cell)) return false;
            if (_attemptedCells.Contains(cell)) return false;

            _attemptedCells.Add(cell);

            Vector3 position = CellToWorld(cell);

            float noise = Mathf.PerlinNoise(
                position.x * noiseScale + _noiseOffsetX,
                position.z * noiseScale + _noiseOffsetZ);

            if (noise < densityThreshold) return false;
            if (IsInExclusionZone(position)) return false;

            SpawnLayer layer = SelectLayer(position);
            if (layer == null) return false;
            if (!IsPositionClear(position, layer.minDistanceBetween)) return false;

            GameObject prefab = layer.GetPrefab();
            if (prefab == null) return false;

            Vector3 jitter = new Vector3(
                Random.Range(-minSpacing * 0.4f, minSpacing * 0.4f),
                0f,
                Random.Range(-minSpacing * 0.4f, minSpacing * 0.4f));

            Vector3 finalPos = position + jitter;
            GameObject instance = Instantiate(prefab, finalPos, layer.GetRotation(finalPos));
            instance.transform.localScale = layer.GetScale(finalPos);

            _occupiedCells.Add(cell);
            _spawnedResources[cell] = instance;

            return true;
        }

        private void CleanupDistantResources()
        {
            float despawnRadiusSqr = despawnRadius * despawnRadius;

            _toCleanupCells.Clear();
            foreach (var kvp in _spawnedResources)
            {
                var cell = kvp.Key;
                var instance = kvp.Value;

                if (!instance)
                {
                    _toCleanupCells.Add(cell);
                    continue;
                }

                Vector3 resourcePos = instance.transform.position;
                bool nearAnyPlayer = false;

                for (int p = 0; p < _players.Count; p++)
                {
                    if ((_players[p].position - resourcePos).sqrMagnitude <= despawnRadiusSqr)
                    {
                        nearAnyPlayer = true;
                        break;
                    }
                }

                if (!nearAnyPlayer)
                    _toCleanupCells.Add(cell);
            }

            for (int i = 0; i < _toCleanupCells.Count; i++)
            {
                var cell = _toCleanupCells[i];
                if (_spawnedResources.TryGetValue(cell, out var instance) && instance)
                    Destroy(instance);

                _spawnedResources.Remove(cell);
                _occupiedCells.Remove(cell);
                _attemptedCells.Remove(cell);
            }

            _toCleanupCells.Clear();
            foreach (var cell in _attemptedCells)
            {
                if (_respawnCells.ContainsKey(cell)) continue;

                Vector3 cellWorld = CellToWorld(cell);
                bool nearAny = false;

                for (int p = 0; p < _players.Count; p++)
                {
                    if ((_players[p].position - cellWorld).sqrMagnitude <= despawnRadiusSqr)
                    {
                        nearAny = true;
                        break;
                    }
                }

                if (!nearAny)
                    _toCleanupCells.Add(cell);
            }

            for (int i = 0; i < _toCleanupCells.Count; i++)
                _attemptedCells.Remove(_toCleanupCells[i]);
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

            if (totalWeight <= 0f) return null;

            float random = Random.value * totalWeight;
            float cumulative = 0f;

            for (int i = 0; i < layers.Length; i++)
            {
                cumulative += _layerWeights[i];
                if (random <= cumulative)
                    return layers[i];
            }

            return layers[^1];
        }

        private bool IsInExclusionZone(Vector3 position)
        {
            for (int i = 0; i < _exclusionZones.Count; i++)
            {
                var zone = _exclusionZones[i];
                if (!zone) continue;
                if ((zone.transform.position - position).sqrMagnitude <= zone.radius * zone.radius)
                    return true;
            }
            return false;
        }

        private bool IsPositionClear(Vector3 position, float minDistance)
        {
            return Physics.OverlapSphereNonAlloc(position, minDistance, _overlapBuffer, overlapMask) == 0;
        }

        private Vector2Int WorldToCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / minSpacing),
                Mathf.FloorToInt(position.z / minSpacing));
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                (cell.x + 0.5f) * minSpacing,
                0f,
                (cell.y + 0.5f) * minSpacing);
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