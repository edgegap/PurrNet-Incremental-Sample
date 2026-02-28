using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class PlayerSkinSelector : NetworkIdentity
    {
        [SerializeField] private List<GameObject> _skins = new();
        
        protected override void OnSpawned()
        {
            if (_skins.Count == 0 || !owner.HasValue)
                return;
            
            for (var i = 0; i < _skins.Count; i++)
            {
                var skin = _skins[i];
                if (skin)
                    skin.SetActive(false);
                else
                {
                    _skins.RemoveAt(i);
                    i--;
                }
            }
            
            _skins[(int)(owner.Value.id % (ulong)_skins.Count)].SetActive(true);
        }
    }
}
