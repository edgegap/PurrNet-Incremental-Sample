using System;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class PlayerUpgrades : NetworkIdentity
    {
        [SerializeField] private UpgradeDefinition[] _definitions;
        [SerializeField] private Base _base;

        private readonly SyncVar<int> _levelDamage = new(0);
        private readonly SyncVar<int> _levelDashDistance = new(0);
        private readonly SyncVar<int> _levelMoveSpeed = new(0);
        private readonly SyncVar<int> _levelInventorySpace = new(0);
        private readonly SyncVar<int> _levelWoodPerTree = new(0);
        private readonly SyncVar<int> _levelWinGame = new(0);

        public event Action onUpgradesChanged;

        public UpgradeDefinition[] definitions => _definitions;
        public bool gameWon => _levelWinGame.value > 0;
        public int currentWood => _base != null ? _base.woodCount : 0;

        protected override void OnSpawned()
        {
            base.OnSpawned();
            InstanceHandler.RegisterInstance(this);

            _levelDamage.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _levelDashDistance.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _levelMoveSpeed.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _levelInventorySpace.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _levelWoodPerTree.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _levelWinGame.onChangedWithOld += (_, __) => onUpgradesChanged?.Invoke();
            _base.onWoodChanged += _ => onUpgradesChanged?.Invoke();
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();
            InstanceHandler.UnregisterInstance<PlayerUpgrades>();
        }

        public int GetLevel(UpgradeType type) => GetLevelVar(type).value;

        public float GetStatBonus(UpgradeType type)
        {
            var def = GetDefinition(type);
            return def == null ? 0f : def.GetStatBonus(GetLevel(type));
        }

        public UpgradeDefinition GetDefinition(UpgradeType type)
        {
            for (int i = 0; i < _definitions.Length; i++)
                if (_definitions[i] != null && _definitions[i].upgradeType == type)
                    return _definitions[i];
            return null;
        }

        [ServerRpc(requireOwnership: false)]
        public void TryPurchaseUpgrade(UpgradeType type)
        {
            var def = GetDefinition(type);
            if (def == null) return;

            int level = GetLevel(type);
            if (type == UpgradeType.WinGame && level > 0) return;

            int cost = def.GetCost(level);
            if (!_base.TrySpendWood(cost)) return;

            GetLevelVar(type).value = level + 1;

            if (type == UpgradeType.WinGame)
                BroadcastWin();
        }

        [ObserversRpc(bufferLast: true)]
        private void BroadcastWin()
        {
            if (InstanceHandler.TryGetInstance(out UpgradeShopView shop))
                shop.ShowWinPanel();
        }

        private SyncVar<int> GetLevelVar(UpgradeType type) => type switch
        {
            UpgradeType.Damage => _levelDamage,
            UpgradeType.DashDistance => _levelDashDistance,
            UpgradeType.MoveSpeed => _levelMoveSpeed,
            UpgradeType.InventorySpace => _levelInventorySpace,
            UpgradeType.WoodPerTree => _levelWoodPerTree,
            UpgradeType.WinGame => _levelWinGame,
            _ => _levelDamage
        };
    }
}
