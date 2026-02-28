using System;
using PurrNet;
using TMPro;
using UnityEngine;

namespace ProgressiveSample
{
    public class HUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _baseWoodCountText;
        
        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
        }

        private void OnDestroy()
        {
            if(InstanceHandler.TryGetInstance(out HUDView otherView) && otherView == this)
                InstanceHandler.UnregisterInstance<HUDView>();
        }

        public void UpdateProgressBar(float maxCapacityStep)
        {
            
        }

        public void UpdateBaseWoodCount(int count)
        {
            _baseWoodCountText.text = count.ToString();
            _baseWoodCountText.transform.TweenPunchScale(Vector3.one * 0.2f, 0.2f);
        }
    }
}
