using UnityEngine;

namespace ProgressiveSample
{
    public enum UpgradeType
    {
        Damage,
        DashDistance,
        MoveSpeed,
        InventorySpace,
        WoodPerTree,
        WinGame
    }

    [CreateAssetMenu(menuName = "Upgrades/Upgrade Definition")]
    public class UpgradeDefinition : ScriptableObject
    {
        public string title;
        public UpgradeType upgradeType;

        [Header("Stat  ·  value = statBase + statMult * level ^ statPower")]
        [Tooltip("The value at level 0 (no upgrades purchased). Set this to the base game value for the stat.")]
        public float statBase;
        [Tooltip("Scales the per-level growth.")]
        public float statMult;
        [Tooltip("Controls the growth curve. 1 = linear, 2 = quadratic, etc.")]
        public float statPower = 1f;

        [Header("Cost  ·  cost = ceil( costBase * costMult ^ level )")]
        public float costBase = 100f;
        public float costMult = 1.5f;

        public int GetCost(int level) =>
            Mathf.CeilToInt(costBase * Mathf.Pow(costMult, level));

        public float GetStatValue(int level) =>
            statBase + statMult * Mathf.Pow(level, statPower);
    }
}
