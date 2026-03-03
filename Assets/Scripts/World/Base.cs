using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace ProgressiveSample
{
    public class Base : NetworkIdentity
    {
        [Header("Deposit Animation")]
        [SerializeField] private float _baseDepositInterval = 0.3f;
        [SerializeField] private float _intervalReductionPerLoot = 0.01f;
        [SerializeField] private float _minDepositInterval = 0.05f;
        [SerializeField] private float _lootFlyDuration = 0.6f;
        [SerializeField] private float _archHeight = 2f;
        
        [Header("Juice")]
        [SerializeField] private float _spiralStrength = 3f;
        [SerializeField] private float _wobbleFrequency = 8f;
        [SerializeField] private float _wobbleAmplitude = 0.15f;
        [SerializeField] private AnimationCurve _flyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        
        [Header("Audio")] 
        [SerializeField] private List<AudioClip> _pickupSounds = new();
        [SerializeField] private Vector2 _pitchRange = new(0.7f, 1.3f);
        
        [Header("Misc")]
        [SerializeField] private GameObject _openShopCanvas;

        private PlayerSuck _playerInRange;
        private readonly List<FlyingLoot> _flyingLoot = new();
        private readonly List<int> _toRemoveIndices = new();
        private float _depositTimer;
        private bool _isDepositing;
        private int _startingLootCount;

        private SyncVar<int> _woodCount = new(-1);

        private struct FlyingLoot
        {
            public Loot Loot;
            public Vector3 StartPosition;
            public float Progress;
            public float SpiralAngle;
        }

        private void Awake()
        {
            if(_openShopCanvas)
                _openShopCanvas.SetActive(false);
        }

        private void OnEnable()
        {
            _woodCount.onChangedWithOld += OnWoodChanged;
        }

        private void OnDisable()
        {
            _woodCount.onChangedWithOld -= OnWoodChanged;
        }

        public int woodCount => _woodCount.value;

        public bool TrySpendWood(int amount)
        {
            if (!isServer || _woodCount.value < amount) return false;
            _woodCount.value -= amount;
            return true;
        }

        public event Action<int> onWoodChanged;

        private void OnWoodChanged(int oldWood, int newWood)
        {
            if(InstanceHandler.TryGetInstance(out HUDView hudView))
                hudView.UpdateBaseWoodCount(newWood);
            onWoodChanged?.Invoke(newWood);
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            if(isServer)
                _woodCount.value = 0;
        }

        private void OnTriggerEnter(Collider other)
        {
            var playerSuck = other.GetComponentInParent<PlayerSuck>();
            if (playerSuck && playerSuck.isOwner)
            {
                _playerInRange = playerSuck;
                _startingLootCount = playerSuck.currentCapacity;
                _depositTimer = 0f;

                if (InstanceHandler.TryGetInstance(out UpgradeShopView shop))
                {
                    InputManager.Instance.GameControls.EnterShop.performed += OnClickedShop;
                    if(_openShopCanvas)
                        _openShopCanvas.SetActive(true);
                }
                return;
            }

            var loot = other.GetComponentInParent<Loot>();
            if (loot && loot.isOwner && !IsAlreadyFlying(loot))
            {
                StartLootFlight(loot);
            }
        }
        
        private void OnTriggerExit(Collider other)
        {
            var playerSuck = other.GetComponentInParent<PlayerSuck>();
            if (!playerSuck || !playerSuck.isOwner)
                return;

            _playerInRange = null;

            if (InstanceHandler.TryGetInstance(out UpgradeShopView shop))
            {
                InputManager.Instance.GameControls.EnterShop.performed -= OnClickedShop;
                if(_openShopCanvas)
                    _openShopCanvas.SetActive(false);
            }
        }

        private void OnClickedShop(InputAction.CallbackContext obj)
        {
            if (InstanceHandler.TryGetInstance(out UpgradeShopView shop))
            {
                shop.Setup();
                shop.Show();
            }
        }

        private void Update()
        {
            ProcessDraining();
            ProcessFlyingLoot();
        }

        private void ProcessDraining()
        {
            if (!_playerInRange)
            {
                SetDepositing(false);
                return;
            }
    
            if (_playerInRange.currentCapacity == 0)
            {
                SetDepositing(false);
                return;
            }

            SetDepositing(true);

            float interval = Mathf.Max(
                _minDepositInterval,
                _baseDepositInterval - (_startingLootCount * _intervalReductionPerLoot)
            );

            _depositTimer += Time.deltaTime;
    
            while (_depositTimer >= interval && _playerInRange.currentCapacity > 0)
            {
                _depositTimer -= interval;
        
                var loot = _playerInRange.TakeOneLoot();
                StartLootFlight(loot);
            }
        }
        
        private void SetDepositing(bool depositing)
        {
            if (_isDepositing == depositing)
                return;
    
            _isDepositing = depositing;
    
            if (!PlayerCamera.TryGetLocal(out var cam))
                return;
    
            if (depositing)
                cam.StartZoom();
            else
                cam.StopZoom();
        }

        private bool IsAlreadyFlying(Loot loot)
        {
            for (int i = 0; i < _flyingLoot.Count; i++)
            {
                if (_flyingLoot[i].Loot == loot)
                    return true;
            }
            return false;
        }

        private void StartLootFlight(Loot loot)
        {
            var rb = loot.rigidbody;
            if (rb)
            {
                rb.isKinematic = true;
            }

            loot.ToggleColliders(false);

            _flyingLoot.Add(new FlyingLoot
            {
                Loot = loot,
                StartPosition = loot.transform.position,
                Progress = 0f,
                SpiralAngle = Random.Range(0f, 360f)
            });
        }

        private void ProcessFlyingLoot()
        {
            _toRemoveIndices.Clear();
            var basePos = transform.position;

            for (int i = 0; i < _flyingLoot.Count; i++)
            {
                var flying = _flyingLoot[i];
                
                if (flying.Loot == null)
                {
                    _toRemoveIndices.Add(i);
                    continue;
                }

                flying.Progress += Time.deltaTime / _lootFlyDuration;
                flying.SpiralAngle += Time.deltaTime * 360f * _spiralStrength;

                if (flying.Progress >= 1f)
                {
                    Destroy(flying.Loot.gameObject);
                    _toRemoveIndices.Add(i);
                    OnAddLoot();
                    continue;
                }

                float t = _flyCurve.Evaluate(flying.Progress);
                var basePosition = Vector3.Lerp(flying.StartPosition, basePos, t);
                
                float archOffset = Mathf.Sin(flying.Progress * Mathf.PI) * _archHeight;
                basePosition.y += archOffset;

                var toBase = (basePos - flying.StartPosition).normalized;
                var perpendicular = Vector3.Cross(toBase, Vector3.up).normalized;
                if (perpendicular.sqrMagnitude < 0.01f)
                    perpendicular = Vector3.Cross(toBase, Vector3.right).normalized;

                float spiralFalloff = 1f - flying.Progress;
                var spiral = Quaternion.AngleAxis(flying.SpiralAngle, toBase) * perpendicular;
                var spiralOffset = spiral * (spiralFalloff * 0.5f);

                var wobble = Mathf.Sin(Time.time * _wobbleFrequency + flying.Loot.GetInstanceID()) * _wobbleAmplitude;
                var wobbleOffset = perpendicular * (wobble * spiralFalloff);

                flying.Loot.transform.position = basePosition + spiralOffset + wobbleOffset;
                flying.Loot.transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);

                _flyingLoot[i] = flying;
            }

            for (int i = _toRemoveIndices.Count - 1; i >= 0; i--)
            {
                _flyingLoot.RemoveAt(_toRemoveIndices[i]);
            }
        }

        private void OnAddLoot()
        {
            transform.TweenComplete();
            transform.TweenPunchScale(Vector3.one * 0.1f, 0.15f);
            if(InstanceHandler.TryGetInstance(out NetworkAudioHelper helper))
                helper.PlayRandom(_pickupSounds, transform.position, Random.Range(_pitchRange.x, _pitchRange.y));
            AddWood();
        }

        [ServerRpc]
        private void AddWood()
        {
            _woodCount.value++;
        }
    }
}