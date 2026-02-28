using System;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public class HUDView : MonoBehaviour
    {
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
    }
}
