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

        public static float Damage { get; private set; }
        public static float DashDistance { get; private set; }
        public static float MoveSpeed { get; private set; }
        public static float InventorySpace { get; private set; }
        public static float WoodPerTree { get; private set; }

        public event Action onUpgradesChanged;

        public UpgradeDefinition[] definitions => _definitions;
        public bool gameWon => _levelWinGame.value > 0;
        public int currentWood => _base != null ? _base.woodCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Damage = 0f;
            DashDistance = 0f;
            MoveSpeed = 0f;
            InventorySpace = 0f;
            WoodPerTree = 0f;
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            InstanceHandler.RegisterInstance(this);

            _levelDamage.onChangedWithOld += (_, __) => OnLevelChanged();
            _levelDashDistance.onChangedWithOld += (_, __) => OnLevelChanged();
            _levelMoveSpeed.onChangedWithOld += (_, __) => OnLevelChanged();
            _levelInventorySpace.onChangedWithOld += (_, __) => OnLevelChanged();
            _levelWoodPerTree.onChangedWithOld += (_, __) => OnLevelChanged();
            _levelWinGame.onChangedWithOld += (_, __) => OnLevelChanged();
            _base.onWoodChanged += _ => onUpgradesChanged?.Invoke();

            RefreshCache();
        }

        protected override void OnDespawned()
        {
            base.OnDespawned();
            InstanceHandler.UnregisterInstance<PlayerUpgrades>();
            ResetStatics();
        }

        private void OnLevelChanged()
        {
            RefreshCache();
            onUpgradesChanged?.Invoke();
        }

        private void RefreshCache()
        {
            Damage = GetStatValue(UpgradeType.Damage);
            DashDistance = GetStatValue(UpgradeType.DashDistance);
            MoveSpeed = GetStatValue(UpgradeType.MoveSpeed);
            InventorySpace = GetStatValue(UpgradeType.InventorySpace);
            WoodPerTree = GetStatValue(UpgradeType.WoodPerTree);
        }

        public int GetLevel(UpgradeType type) => GetLevelVar(type).value;

        public float GetStatValue(UpgradeType type)
        {
            var def = GetDefinition(type);
            return def == null ? 0f : def.GetStatValue(GetLevel(type));
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
