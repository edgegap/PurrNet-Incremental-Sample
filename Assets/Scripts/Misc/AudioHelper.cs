using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    public static class AudioHelper
    {
        public static void PlayRandom(this AudioSource source, List<AudioClip> clips, float? pitch = null)
        {
            if (!source || clips == null || clips.Count == 0)
                return;
            if (pitch.HasValue)
                source.pitch = pitch.Value;
            source.PlayOneShot(clips[Random.Range(0, clips.Count)]);
        }
    }
}
