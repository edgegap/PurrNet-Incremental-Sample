using System.Collections.Generic;
using UnityEngine;

namespace ProgressiveSample
{
    public static class AudioHelper
    {
        public static void PlayRandom(this AudioSource source, List<AudioClip> clips)
        {
            if (!source || clips == null || clips.Count == 0)
                return;
            
            source.PlayOneShot(clips[Random.Range(0, clips.Count)]);
        }
    }
}
