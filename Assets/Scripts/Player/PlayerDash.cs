using System.Collections.Generic;
using PurrNet.StateMachine;
using UnityEngine;

namespace ProgressiveSample
{
    public class PlayerDash : StateNode
    {
        [SerializeField] private int _damage = 1;
        [SerializeField] private float _dashSpeed = 25f;
        [SerializeField] private float _dashDuration = 0.15f;
        [SerializeField] private float _cooldown = 1f;
        [SerializeField] private AnimationCurve _dashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField] private PlayerMovement _movementState;
        [SerializeField] private LayerMask _resourceLayer;
        [SerializeField] private int _dashingLayer;
        [SerializeField] private float _detectionRadius = 0.5f;
        [SerializeField] private Collider _collider;
        
        [Header("Visual Squish")]
        [SerializeField] private Transform _visuals;
        [SerializeField] private Vector3 _squishScale = new Vector3(0.7f, 0.7f, 1.3f);
        [SerializeField] private AnimationCurve _squishCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Audio")] 
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<AudioClip> _dashSounds = new();

        private Rigidbody _rb;
        private float _dashTimer;
        private int _originalLayer;
        private List<Collider> _hitResources = new();
        private float _lastDashTime;
        private GhostTrailEffect _ghostTrailEffect;
        private Vector3 _originalScale;
        public int damage => _damage;

        private void Awake()
        {
            _ghostTrailEffect = GetComponentInChildren<GhostTrailEffect>();
            if (!TryGetComponent(out _rb))
                Debug.LogError("PlayerDash failed to get Rigidbody component!", this);
        }

        public override bool CanEnter()
        {
            return _lastDashTime + _cooldown < Time.time;
        }

        public override void Enter()
        {
            _ghostTrailEffect.StartSpawning();
    
            if (_visuals)
                _originalScale = _visuals.localScale;

            _audioSource.PlayRandom(_dashSounds);
    
            if (!isOwner) return;

            _dashTimer = 0f;
            _lastDashTime = Time.time;
            _hitResources.Clear();
    
            _collider.isTrigger = true;
        }

        public override void Exit()
        {
            _ghostTrailEffect.StopSpawning();
    
            if (_visuals)
                _visuals.localScale = _originalScale;
    
            if (!isOwner) return;

            _rb.linearVelocity = Vector3.zero;
    
            TryEscapeOverlap();
            _collider.isTrigger = false;
        }
        
        private void TryEscapeOverlap()
        {
            var hits = Physics.OverlapSphere(_rb.position, _detectionRadius, _resourceLayer);
            if (hits.Length == 0) return;
    
            Vector3 escapeDir = transform.forward;
            for (float dist = 0.1f; dist <= 2f; dist += 0.1f)
            {
                Vector3 testPos = _rb.position + escapeDir * dist;
                if (Physics.OverlapSphere(testPos, _detectionRadius, _resourceLayer).Length == 0)
                {
                    _rb.position = new Vector3(testPos.x, _rb.position.y, testPos.z);
                    return;
                }
            }
        }

        public override void StateUpdate()
        {
            if (!isOwner) return;

            _dashTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_dashTimer / _dashDuration);
            
            UpdateVisualSquish(t);
            
            float speedMultiplier = _dashCurve.Evaluate(t);
            
            Vector3 dashVelocity = transform.forward * (_dashSpeed * speedMultiplier);
            _rb.linearVelocity = new Vector3(dashVelocity.x, _rb.linearVelocity.y, dashVelocity.z);

            DetectResources();

            if (_dashTimer >= _dashDuration)
            {
                machine.SetState(_movementState);
            }
        }

        private void UpdateVisualSquish(float t)
        {
            if (!_visuals) return;
            
            float squishAmount = _squishCurve.Evaluate(t);
            Vector3 targetScale = Vector3.LerpUnclamped(_originalScale, Vector3.Scale(_originalScale, _squishScale), squishAmount);
            _visuals.localScale = targetScale;
        }

        private void DetectResources()
        {
            var hits = Physics.OverlapSphere(_rb.position, _detectionRadius, _resourceLayer);
            foreach (var hit in hits)
            {
                if (_hitResources.Contains(hit)) continue;
                _hitResources.Add(hit);

                if (hit.TryGetComponent<IMineable>(out var mineable))
                {
                    mineable.OnDashHit(this);
                }
            }
        }
    }
}