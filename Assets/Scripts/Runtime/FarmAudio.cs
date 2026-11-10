using System;
using UnityEngine;

namespace GrowWhatever
{
    public enum FarmSound { Click, Sow, Harvest, Trade, Unavailable, Processing, Unlock, Goal }
    [Serializable] public sealed class FarmSoundBinding { public FarmSound sound; public AudioClip clip; }

    public sealed class FarmAudio : MonoBehaviour
    {
        [SerializeField] private AudioSource source;
        [SerializeField] private FarmSoundBinding[] clips;
        [SerializeField, Range(0, 1)] private float volume = 0.5f;
        private float lastClickTime = -1;

        private void Awake()
        {
            if (clips == null) return;
            foreach (var binding in clips)
                if (binding.clip != null) binding.clip.LoadAudioData();
        }

        public void Play(FarmSound sound)
        {
            if (source == null || clips == null) return;
            if (sound == FarmSound.Click && Time.unscaledTime - lastClickTime < 0.04f) return;
            if (sound == FarmSound.Click) lastClickTime = Time.unscaledTime;
            var binding = Array.Find(clips, x => x.sound == sound);
            if (binding?.clip != null) source.PlayOneShot(binding.clip, volume);
        }
    }
}
