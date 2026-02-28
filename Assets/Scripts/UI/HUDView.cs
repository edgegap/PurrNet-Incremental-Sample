using PurrNet;
using TMPro;
using UnityEngine;

namespace ProgressiveSample
{
    public class HUDView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _baseWoodCountText;
        [SerializeField] private Transform _currentWoodFill;
        [SerializeField] private Transform _currentWoodParent;
        
        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
            _currentWoodFill.localScale = new Vector3(0, 1, 1);
        }

        private void OnDestroy()
        {
            if(InstanceHandler.TryGetInstance(out HUDView otherView) && otherView == this)
                InstanceHandler.UnregisterInstance<HUDView>();
        }

        public void UpdateProgressBar(float maxCapacityStep)
        {
            _currentWoodFill.TweenScale(new Vector3(maxCapacityStep, 1, 1), 0.35f);
            _currentWoodParent.TweenComplete();
            _currentWoodParent.TweenPunchScale(Vector3.one * 0.05f, 0.1f);
        }

        public void UpdateBaseWoodCount(int count)
        {
            _baseWoodCountText.text = count.ToString();
            _baseWoodCountText.transform.TweenComplete();
            _baseWoodCountText.transform.TweenPunchScale(Vector3.one * 0.1f, 0.2f);
        }
    }
}
