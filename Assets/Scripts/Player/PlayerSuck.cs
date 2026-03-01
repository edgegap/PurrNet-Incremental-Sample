using System;
using System.Collections.Generic;
using PurrNet;
using PurrNet.StateMachine;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace ProgressiveSample
{
    public class PlayerSuck : StateNode
    {
        [SerializeField] private PlayerMovement _playerMovement;
        
        [Header("Cone Detection")]
        [SerializeField] private float _coneRange = 8f;
        [SerializeField] private float _coneAngle = 45f;
        [SerializeField] private LayerMask _suckableLayers;
        
        [Header("Suck Physics")]
        [SerializeField] private float _suckForce = 50f;
        [SerializeField] private float _maxSuckSpeed = 20f;
        [SerializeField] private float _captureDistance = 1f;
        [SerializeField] private AnimationCurve _forceCurve = AnimationCurve.EaseInOut(0f, 0.2f, 1f, 1f);
        
        [Header("Juice")]
        [SerializeField] private float _spiralStrength = 2f;
        [SerializeField] private float _wobbleFrequency = 8f;
        [SerializeField] private float _wobbleAmplitude = 0.3f;

        [Header("Player visual")] 
        [SerializeField] private Transform _visual;
        [SerializeField] private Vector3 _squishScale = new(1.3f, 0.5f, 0.7f);
        [SerializeField] private float _squishTime = 0.2f;

        [Header("Audio")] 
        [SerializeField] private List<AudioClip> _pickupSounds = new();
        [SerializeField] private Vector2 _pitchRange = new(0.7f, 1.3f);

        [Header("Debug")]
        [SerializeField] private bool _debugMode = true;

        private readonly List<ISuckable> _capturedItems = new();
        private readonly List<ISuckable> _beingSuckedKeys = new();
        private readonly List<SuckingState> _beingSuckedValues = new();
        private readonly List<int> _toRemoveIndices = new();
        private Collider[] _overlapResults;
        private Transform _suckPoint;
        private Vector3 _originalScale;

        private struct SuckingState
        {
            public Rigidbody Rigidbody;
            public bool WasGravityEnabled;
            public float WasDrag;
            public float SuckProgress;
            public float SpiralAngle;
        }

        public event Action<int> onCapturedItemsChanged;

        private int maxCapacity => Mathf.Max(1, (int)PlayerUpgrades.InventorySpace);

        public int currentCapacity => _capturedItems.Count;
        public float maxCapacityStep => (float)_capturedItems.Count / maxCapacity;

        private void Awake()
        {
            _suckPoint = transform;
            _overlapResults = new Collider[64];
            _originalScale = _visual.localScale;
            onCapturedItemsChanged += OnItemsChanged;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            onCapturedItemsChanged -= OnItemsChanged;
        }

        private void OnItemsChanged(int newCount)
        {
            if(InstanceHandler.TryGetInstance(out HUDView hudView))
                hudView.UpdateProgressBar(maxCapacityStep);
        }

        public override bool CanEnter() => maxCapacityStep < 1;

        public override void Enter()
        {
            base.Enter();
            if (!isOwner) return;
            InputManager.Instance.GameControls.Suck.canceled += OnStopSuck;
            _visual.TweenComplete();
            _visual.TweenScale(_squishScale, _squishTime);
            
            if (_debugMode) Debug.Log("[PlayerSuck] Entered suck state");
        }

        public override void Exit()
        {
            base.Exit();
            if (!isOwner) return;
            InputManager.Instance.GameControls.Suck.canceled -= OnStopSuck;
            ReleaseAllBeingSucked();
            _visual.TweenComplete();
            _visual.TweenScale(_originalScale, _squishTime);
            
            if (_debugMode) Debug.Log("[PlayerSuck] Exited suck state");
        }

        public override void StateUpdate()
        {
            base.StateUpdate();
            _playerMovement.HandlePlayerMovement(false, true);
        }

        private void FixedUpdate()
        {
            if (!isCurrentState)
                return;
            if (!isOwner) return;
            
            DetectSuckables();
            ProcessSucking();
        }

        private void DetectSuckables()
        {
            if (_capturedItems.Count >= maxCapacity) return;

            int count = Physics.OverlapSphereNonAlloc(_suckPoint.position, _coneRange, _overlapResults, _suckableLayers);
            
            if (_debugMode && count > 0)
            {
                var names = "";
                for (int i = 0; i < count; i++)
                    names += _overlapResults[i].name + (i < count - 1 ? ", " : "");
                Debug.Log($"[PlayerSuck] OverlapSphere found {count} colliders: {names}");
            }

            for (int i = 0; i < count; i++)
            {
                var col = _overlapResults[i];
                var suckable = col.GetComponentInParent<ISuckable>();
                if (suckable == null) continue;
                
                if (_debugMode) Debug.Log($"[PlayerSuck] Found ISuckable: {col.name}");
                
                if (IsBeingSucked(suckable) || _capturedItems.Contains(suckable)) continue;
                if (!suckable.rigidbody)
                {
                    if (_debugMode) Debug.LogWarning($"[PlayerSuck] {col.name} has no rigidbody!");
                    continue;
                }
                
                var dirToTarget = (suckable.rigidbody.position - _suckPoint.position).normalized;
                var angle = Vector3.Angle(_suckPoint.forward, dirToTarget);
                
                if (_debugMode) Debug.Log($"[PlayerSuck] {col.name} angle: {angle:F1} (max: {_coneAngle})");
                
                if (angle > _coneAngle) continue;

                StartSucking(suckable);
            }
        }

        private bool IsBeingSucked(ISuckable suckable)
        {
            for (int i = 0; i < _beingSuckedKeys.Count; i++)
            {
                if (ReferenceEquals(_beingSuckedKeys[i], suckable)) return true;
            }
            return false;
        }

        private void StartSucking(ISuckable suckable)
        {
            var rb = suckable.rigidbody;
            
            if (_debugMode) Debug.Log($"[PlayerSuck] Starting to suck: {rb.name}");
            
            suckable.networkIdentity.GiveOwnership(localPlayer);
            _beingSuckedKeys.Add(suckable);
            _beingSuckedValues.Add(new SuckingState
            {
                Rigidbody = rb,
                WasGravityEnabled = rb.useGravity,
                WasDrag = rb.linearDamping,
                SuckProgress = 0f,
                SpiralAngle = Random.Range(0f, 360f)
            });
            
            rb.useGravity = false;
            rb.linearDamping = 2f;
        }

        private void ProcessSucking()
        {
            _toRemoveIndices.Clear();
            
            for (int i = 0; i < _beingSuckedKeys.Count; i++)
            {
                var suckable = _beingSuckedKeys[i];
                var state = _beingSuckedValues[i];
                var rb = state.Rigidbody;
                
                if (rb == null)
                {
                    _toRemoveIndices.Add(i);
                    continue;
                }

                var targetPos = _suckPoint.position;
                var currentPos = rb.position;
                var toTarget = targetPos - currentPos;
                var distance = toTarget.magnitude;

                if (distance < _captureDistance)
                {
                    CaptureItem(suckable, state);
                    _toRemoveIndices.Add(i);
                    continue;
                }

                state.SuckProgress = Mathf.Clamp01(state.SuckProgress + Time.fixedDeltaTime * 2f);
                state.SpiralAngle += Time.fixedDeltaTime * 360f * _spiralStrength;

                var normalizedDist = Mathf.Clamp01(distance / _coneRange);
                var forceMult = _forceCurve.Evaluate(1f - normalizedDist) * state.SuckProgress;

                var baseDir = toTarget.normalized;
                
                var perpendicular = Vector3.Cross(baseDir, Vector3.up).normalized;
                if (perpendicular.sqrMagnitude < 0.01f)
                    perpendicular = Vector3.Cross(baseDir, Vector3.right).normalized;
                
                var spiral = Quaternion.AngleAxis(state.SpiralAngle, baseDir) * perpendicular;
                var spiralOffset = spiral * (1f - state.SuckProgress) * 0.5f;
                
                var wobble = Mathf.Sin(Time.time * _wobbleFrequency + rb.GetInstanceID()) * _wobbleAmplitude;
                var wobbleOffset = perpendicular * wobble * (1f - state.SuckProgress);

                var finalDir = (baseDir + spiralOffset + wobbleOffset).normalized;
                var force = finalDir * (_suckForce * forceMult);

                rb.AddForce(force, ForceMode.Acceleration);
                
                if (rb.linearVelocity.magnitude > _maxSuckSpeed)
                    rb.linearVelocity = rb.linearVelocity.normalized * _maxSuckSpeed;

                var spinTorque = baseDir * 10f * forceMult;
                rb.AddTorque(spinTorque, ForceMode.Acceleration);
                
                _beingSuckedValues[i] = state;
            }

            for (int i = _toRemoveIndices.Count - 1; i >= 0; i--)
            {
                int idx = _toRemoveIndices[i];
                _beingSuckedKeys.RemoveAt(idx);
                _beingSuckedValues.RemoveAt(idx);
            }
        }

        private void CaptureItem(ISuckable suckable, SuckingState state)
        {
            if (_capturedItems.Count < maxCapacity)
            {
                _capturedItems.Add(suckable);
                suckable.SetVisible(false);
                if(InstanceHandler.TryGetInstance(out NetworkAudioHelper helper))
                    helper.PlayRandom(_pickupSounds, transform.position, Random.Range(_pitchRange.x, _pitchRange.y));
                onCapturedItemsChanged?.Invoke(_capturedItems.Count);
                
                if (_debugMode && state.Rigidbody != null) 
                    Debug.Log($"[PlayerSuck] Captured: {state.Rigidbody.name}");
            }
            else
            {
                RestoreRigidbody(state);
            }

            if (maxCapacityStep >= 1)
                OnStopSuck(default);
        }

        private void ReleaseAllBeingSucked()
        {
            for (int i = 0; i < _beingSuckedValues.Count; i++)
            {
                RestoreRigidbody(_beingSuckedValues[i]);
            }
            _beingSuckedKeys.Clear();
            _beingSuckedValues.Clear();
        }

        private void RestoreRigidbody(SuckingState state)
        {
            if (state.Rigidbody == null) return;
            
            state.Rigidbody.useGravity = state.WasGravityEnabled;
            state.Rigidbody.linearDamping = state.WasDrag;
        }

        public Loot TakeOneLoot()
        {
            if (_capturedItems.Count == 0) return null;
            
            int lastIndex = _capturedItems.Count - 1;
            var item = _capturedItems[lastIndex];
            _capturedItems.RemoveAt(lastIndex);
            onCapturedItemsChanged?.Invoke(_capturedItems.Count);
            
            return item as Loot;
        }

        public void ReleaseCapturedItems()
        {
            for (int i = 0; i < _capturedItems.Count; i++)
            {
                var item = _capturedItems[i];
                if (item is Component comp && comp != null)
                {
                    comp.gameObject.SetActive(true);
                    if (comp.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.useGravity = true;
                        rb.linearDamping = 0f;
                    }
                }
            }
            _capturedItems.Clear();
            onCapturedItemsChanged?.Invoke(0);
        }

        private void OnStopSuck(InputAction.CallbackContext obj)
        {
            machine.SetState(_playerMovement);
        }

        private void OnDrawGizmos()
        {
            if (!_debugMode) return;
            if (!isCurrentState) return;
            
            var origin = _suckPoint != null ? _suckPoint.position : transform.position;
            var forward = _suckPoint != null ? _suckPoint.forward : transform.forward;
            
            Gizmos.color = Color.cyan;
            var endCenter = origin + forward * _coneRange;
            var endRadius = Mathf.Tan(_coneAngle * Mathf.Deg2Rad) * _coneRange;
            
            for (int i = 0; i < 16; i++)
            {
                var angle = i * 22.5f * Mathf.Deg2Rad;
                var nextAngle = (i + 1) * 22.5f * Mathf.Deg2Rad;
                
                var right = Vector3.Cross(forward, Vector3.up).normalized;
                if (right.sqrMagnitude < 0.01f)
                    right = Vector3.Cross(forward, Vector3.right).normalized;
                var up = Vector3.Cross(right, forward);
                
                var p1 = endCenter + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * endRadius;
                var p2 = endCenter + (right * Mathf.Cos(nextAngle) + up * Mathf.Sin(nextAngle)) * endRadius;
                
                Gizmos.DrawLine(origin, p1);
                Gizmos.DrawLine(p1, p2);
            }
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(origin, _captureDistance);
            
            Gizmos.color = Color.yellow;
            for (int i = 0; i < _beingSuckedValues.Count; i++)
            {
                var state = _beingSuckedValues[i];
                if (state.Rigidbody != null)
                {
                    Gizmos.DrawLine(origin, state.Rigidbody.position);
                    Gizmos.DrawWireSphere(state.Rigidbody.position, 0.3f);
                }
            }
        }
    }
}