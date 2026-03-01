using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProgressiveSample
{
    public class UpgradeEntryView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _statText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private Button _buyButton;

        private UpgradeDefinition _definition;
        private PlayerUpgrades _upgrades;

        public void Initialize(UpgradeDefinition definition, PlayerUpgrades upgrades)
        {
            _definition = definition;
            _upgrades = upgrades;
            _titleText.text = definition.title;
            _buyButton.onClick.AddListener(OnBuyClicked);
        }

        public void Refresh(int currentWood)
        {
            if (_definition == null) return;

            int level = _upgrades.GetLevel(_definition.upgradeType);

            if (_definition.upgradeType == UpgradeType.WinGame)
            {
                if (level > 0)
                {
                    _statText.text = "Victory!";
                    _costText.text = string.Empty;
                    _buyButton.interactable = false;
                    return;
                }

                _statText.text = "Win the game";
                int winCost = _definition.GetCost(0);
                _costText.text = $"{winCost} wood";
                _buyButton.interactable = currentWood >= winCost;
                return;
            }

            float current = _definition.GetStatBonus(level);
            float next = _definition.GetStatBonus(level + 1);
            int cost = _definition.GetCost(level);

            _statText.text = $"+{Format(current)}  \u2192  +{Format(next)}";
            _costText.text = $"{cost}";
            _buyButton.interactable = currentWood >= cost;
        }

        private static string Format(float value) =>
            value % 1 == 0 ? ((int)value).ToString() : value.ToString("F1");

        private void OnBuyClicked() => _upgrades.TryPurchaseUpgrade(_definition.upgradeType);
    }
}
