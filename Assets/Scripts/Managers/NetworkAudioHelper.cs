using System.Collections.Generic;
using PurrNet;
using UnityEngine;

namespace ProgressiveSample
{
    //Just some quick audio source pooling so we can play audio for things that are being destroyed
    public class NetworkAudioHelper : NetworkIdentity
    {
        [SerializeField] private AudioSource _sourcePrefab;
        [SerializeField] private int _initialPoolSize = 8;

        private readonly List<AudioSource> _pool = new();

        private void Awake()
        {
            InstanceHandler.RegisterInstance(this);
            for (int i = 0; i < _initialPoolSize; i++)
                CreateSource();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (InstanceHandler.TryGetInstance(out NetworkAudioHelper helper) && helper == this)
                InstanceHandler.UnregisterInstance<NetworkAudioHelper>();
        }

        private AudioSource CreateSource()
        {
            var source = Instantiate(_sourcePrefab, transform);
            source.gameObject.SetActive(false);
            _pool.Add(source);
            return source;
        }

        private AudioSource GetAvailable()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                var source = _pool[i];
                if (!source.isPlaying)
                    return source;
            }

            return CreateSource();
        }

        [ObserversRpc(runLocally:true)]
        public void PlayClip(AudioClip clip, Vector3 position, float? pitch = null)
        {
            if (!clip) return;
            var source = GetAvailable();
            source.transform.position = position;
            source.gameObject.SetActive(true);
            source.clip = clip;
            if (pitch.HasValue)
                source.pitch = pitch.Value;
            else
                source.pitch = _sourcePrefab.pitch;
            source.Play();
        }

        public void PlayRandom(List<AudioClip> clips, Vector3 position, float? pitch = null)
        {
            if (clips == null || clips.Count == 0) return;
            PlayClip(clips[Random.Range(0, clips.Count)], position, pitch);
        }
    }
}