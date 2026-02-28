using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class BaseMineable : NetworkIdentity, IMineable
    {
        [Header("Health")]
        [SerializeField] private int _maxHealth = 10;
        
        [Header("Drops")]
        [SerializeField] private Vector2Int _dropCount = new(1, 3);
        [SerializeField] private Loot _dropPrefab;
        [SerializeField] private float _dropSpawnRadius = 0.5f;
        
        [Header("Hit Feedback")]
        [SerializeField] private float _flashIntensity = 0.5f;
        [SerializeField] private float _flashDuration = 0.1f;
        [SerializeField] private Renderer[] _renderers;

        [Header("Audio")] 
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<AudioClip> _hitSounds;
        
        private SyncVar<int> _health = new(ownerAuth:true);
        private List<Material> _materials = new();
        private List<Color> _originalColors = new();
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        protected override void OnSpawned()
        {
            base.OnSpawned();
            CacheMaterials();
            if(isServer) 
                _health.value = _maxHealth;
        }

        private void CacheMaterials()
        {
            _materials.Clear();
            _originalColors.Clear();
            
            foreach (var renderer in _renderers)
            {
                foreach (var mat in renderer.materials)
                {
                    _materials.Add(mat);
                    _originalColors.Add(mat.GetColor(EmissionColor));
                }
            }
        }

        private void OnEnable()
        {
            _health.onChangedWithOld += OnHealthChanged;
        }

        private void OnDisable()
        {
            _health.onChangedWithOld -= OnHealthChanged;
        }

        public void OnDashHit(PlayerDash dash)
        {
            GiveOwnership(localPlayer);
            _health.value -= dash.damage;

            if (_health.value <= 0)
                Die();
        }

        private void Die()
        {
            SpawnDrops();
            Destroy(gameObject);
        }

        private void SpawnDrops()
        {
            int count = Random.Range(_dropCount.x, _dropCount.y + 1);
            
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Random.insideUnitSphere * _dropSpawnRadius;
                offset.y = Mathf.Abs(offset.y);
                
                Vector3 spawnPos = transform.position + offset;
                Loot loot = Instantiate(_dropPrefab, spawnPos, Random.rotation);
                loot.PopRandom();
            }
        }
        
        private void OnHealthChanged(int oldValue, int newValue)
        {
            if (oldValue <= newValue)
                return;

            PlayHitFeedback();
        }

        private void PlayHitFeedback()
        {
            _audioSource.PlayRandom(_hitSounds);
            transform.TweenComplete();
            transform.TweenPunchScale(Vector3.one * 0.2f, 0.2f);
            FlashMaterials();
        }

        private void FlashMaterials()
        {
            for (int i = 0; i < _materials.Count; i++)
            {
                var mat = _materials[i];
                var original = _originalColors[i];
                
                mat.EnableKeyword("_EMISSION");
                Color flashColor = Color.white * _flashIntensity;
                
                mat.TweenKill();
                mat.SetColor(EmissionColor, flashColor);
                mat.TweenColor(original, EmissionColor, _flashDuration);
            }
        }
    }
}