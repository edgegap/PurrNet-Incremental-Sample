using System;
using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class UpgradeShopView : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;
        [SerializeField] private Transform _entriesContainer;
        [SerializeField] private UpgradeEntryView _entryPrefab;
        [SerializeField] private GameObject _winPanel;

        private readonly List<UpgradeEntryView> _entries = new();
        private PlayerUpgrades _upgrades;
        private int _currentWood;
        private bool _initialized;

        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
            _panel.SetActive(false);
            if (_winPanel) _winPanel.SetActive(false);
        }

        private void OnEnable()
        {
            InputManager.Instance.EnableMenuControls();
        }

        private void OnDisable()
        {
            InputManager.Instance.EnableGameControls();
        }

        private void OnDestroy()
        {
            InstanceHandler.UnregisterInstance<UpgradeShopView>();
            Unsubscribe();
        }

        public void Setup()
        {
            if (!_initialized)
            {
                if (InstanceHandler.TryGetInstance(out PlayerUpgrades upgrades))
                {
                    _upgrades = upgrades;
                    _upgrades.onUpgradesChanged += Refresh;
                    PopulateEntries();
                }
                _initialized = true;
            }
        }

        public void Show() => _panel.SetActive(true);
        public void Hide() => _panel.SetActive(false);

        public void ShowWinPanel()
        {
            if (_winPanel) _winPanel.SetActive(true);
        }

        private void PopulateEntries()
        {
            foreach (var entry in _entries)
                Destroy(entry.gameObject);
            foreach (Transform entry in _entriesContainer)
                Destroy(entry.gameObject);
            _entries.Clear();

            if (_upgrades == null) return;

            foreach (var def in _upgrades.definitions)
            {
                if (def == null) continue;
                var entry = Instantiate(_entryPrefab, _entriesContainer);
                entry.Initialize(def, _upgrades);
                _entries.Add(entry);
            }
        }

        private void Refresh()
        {
            if (_upgrades != null && _upgrades.gameWon && _winPanel)
                _winPanel.SetActive(true);

            foreach (var entry in _entries)
                entry.Refresh(_currentWood);
        }

        private void Unsubscribe()
        {
            if (_upgrades != null)
                _upgrades.onUpgradesChanged -= Refresh;
        }
    }
}
