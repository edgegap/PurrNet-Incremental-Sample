using System.Collections.Generic;
using UnityEngine;

namespace ProgressiveSample
{
    public class GhostTrailEffect : MonoBehaviour
    {
        [SerializeField] private Material _ghostMaterial;
        [SerializeField] private float _ghostDuration = 0.4f;
        [SerializeField] private float _spawnInterval = 0.05f;
        [SerializeField, Range(0.5f, 0.99f)] private float _scaleMultiplier = 0.95f;
        [SerializeField] private int _maxGhosts = 20;

        private readonly List<GhostData> _ghosts = new();
        private MeshFilter[] _meshFilters;
        private SkinnedMeshRenderer[] _skinnedRenderers;
        private Mesh[] _bakedMeshes;
        private Matrix4x4[][] _matrices;
        private MaterialPropertyBlock _propertyBlock;
        private float _spawnTimer;
        private bool _isSpawning;
        private Color _initialColor;

        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private struct GhostData
        {
            public Matrix4x4[] matrices;
            public float elapsed;
        }

        private void Awake()
        {
            _meshFilters = GetComponentsInChildren<MeshFilter>();
            _skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
            
            _bakedMeshes = new Mesh[_skinnedRenderers.Length];
            for (int i = 0; i < _skinnedRenderers.Length; i++)
            {
                _bakedMeshes[i] = new Mesh();
            }

            if (_ghostMaterial != null)
            {
                _initialColor = _ghostMaterial.GetColor(BaseColor);
                _ghostMaterial.enableInstancing = true;
            }
        }

        private void LateUpdate()
        {
            for (int i = 0; i < _skinnedRenderers.Length; i++)
            {
                _skinnedRenderers[i].BakeMesh(_bakedMeshes[i]);
            }
            
            for (int i = _ghosts.Count - 1; i >= 0; i--)
            {
                var ghost = _ghosts[i];
                ghost.elapsed += Time.deltaTime;
                
                if (ghost.elapsed >= _ghostDuration)
                {
                    _ghosts.RemoveAt(i);
                    continue;
                }
                
                _ghosts[i] = ghost;
                
                float alpha = Mathf.Lerp(_initialColor.a, 0f, ghost.elapsed / _ghostDuration);
                Color color = _initialColor;
                color.a = alpha;
                _propertyBlock.SetColor(BaseColor, color);

                for (int m = 0; m < _bakedMeshes.Length; m++)
                {
                    Graphics.DrawMesh(_bakedMeshes[m], ghost.matrices[m], _ghostMaterial, 0, null, 0, _propertyBlock);
                }
                
                for (int m = 0; m < _meshFilters.Length; m++)
                {
                    int idx = _bakedMeshes.Length + m;
                    Graphics.DrawMesh(_meshFilters[m].sharedMesh, ghost.matrices[idx], _ghostMaterial, 0, null, 0, _propertyBlock);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_isSpawning) return;
            
            _spawnTimer -= Time.fixedDeltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnGhost();
                _spawnTimer = _spawnInterval;
            }
        }

        public void StartSpawning()
        {
            if (!enabled) return;
            _isSpawning = true;
            _spawnTimer = 0f;
            SpawnGhost();
        }

        public void StopSpawning() => _isSpawning = false;

        private void SpawnGhost()
        {
            if (_ghosts.Count >= _maxGhosts) return;

            var matrices = new Matrix4x4[_skinnedRenderers.Length + _meshFilters.Length];
            
            for (int i = 0; i < _skinnedRenderers.Length; i++)
            {
                var t = _skinnedRenderers[i].transform;
                matrices[i] = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale * _scaleMultiplier);
            }
            
            for (int i = 0; i < _meshFilters.Length; i++)
            {
                var t = _meshFilters[i].transform;
                matrices[_skinnedRenderers.Length + i] = Matrix4x4.TRS(t.position, t.rotation, t.lossyScale * _scaleMultiplier);
            }

            _ghosts.Add(new GhostData { matrices = matrices, elapsed = 0f });
        }
    }
}