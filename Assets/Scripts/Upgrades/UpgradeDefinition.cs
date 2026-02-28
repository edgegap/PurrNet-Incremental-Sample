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

        [Header("Stat  ·  bonus = statBase * level + statMult * level ^ statPower")]
        public float statBase;
        public float statMult;
        public float statPower = 1f;

        [Header("Cost  ·  cost = ceil( costBase * costMult ^ level )")]
        public float costBase = 100f;
        public float costMult = 1.5f;

        public int GetCost(int level) =>
            Mathf.CeilToInt(costBase * Mathf.Pow(costMult, level));

        public float GetStatBonus(int level) =>
            statBase * level + statMult * Mathf.Pow(level, statPower);
    }
}
