using System.Collections.Generic;
using UnityEngine;

namespace ProgressiveSample
{
    public class PlayerFootstep : MonoBehaviour
    {
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private List<AudioClip> _footsteps = new();
        [SerializeField] private Vector2 _pitchRange = new(0.75f, 1.25f);

        public void PlayFootstep()
        {
            _audioSource.PlayRandom(_footsteps, Random.Range(_pitchRange.x, _pitchRange.y));
        }
    }
}
