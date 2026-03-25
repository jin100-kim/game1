using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace EJR.Game.Audio
{
    public enum AudioBus
    {
        Bgm = 0,
        Sfx = 1,
        Ui = 2,
    }

    [CreateAssetMenu(menuName = "EJR/Audio/Cue Catalog", fileName = "AudioCueCatalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public AudioCueId cueId = AudioCueId.None;
            public AudioClip clip;
            public AudioMixerGroup mixerGroup;
            public AudioBus bus = AudioBus.Sfx;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0f, 0.5f)] public float pitchVariance;
            [Min(0f)] public float minRetriggerInterval;
            [Min(0f)] public float maxPlaybackDuration;
            [Min(1)] public int maxVoices = 1;
            public bool loop;
        }

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup masterGroup;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup uiGroup;
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        private Dictionary<AudioCueId, Entry> _entryLookup;

        public AudioMixer Mixer => mixer;
        public AudioMixerGroup MasterGroup => masterGroup;
        public AudioMixerGroup BgmGroup => bgmGroup;
        public AudioMixerGroup SfxGroup => sfxGroup;
        public AudioMixerGroup UiGroup => uiGroup;
        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetEntry(AudioCueId cueId, out Entry entry)
        {
            if (_entryLookup == null || _entryLookup.Count != entries.Length)
            {
                RebuildLookup();
            }

            return _entryLookup.TryGetValue(cueId, out entry);
        }

        public AudioMixerGroup GetBusGroup(AudioBus bus)
        {
            return bus switch
            {
                AudioBus.Bgm => bgmGroup != null ? bgmGroup : masterGroup,
                AudioBus.Ui => uiGroup != null ? uiGroup : masterGroup,
                _ => sfxGroup != null ? sfxGroup : masterGroup,
            };
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _entryLookup = new Dictionary<AudioCueId, Entry>(entries.Length);
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.cueId == AudioCueId.None)
                {
                    continue;
                }

                _entryLookup[entry.cueId] = entry;
            }
        }

#if UNITY_EDITOR
        public void Configure(
            AudioMixer audioMixer,
            AudioMixerGroup resolvedMasterGroup,
            AudioMixerGroup resolvedBgmGroup,
            AudioMixerGroup resolvedSfxGroup,
            AudioMixerGroup resolvedUiGroup,
            Entry[] nextEntries)
        {
            mixer = audioMixer;
            masterGroup = resolvedMasterGroup;
            bgmGroup = resolvedBgmGroup;
            sfxGroup = resolvedSfxGroup;
            uiGroup = resolvedUiGroup;
            entries = nextEntries ?? Array.Empty<Entry>();
            RebuildLookup();
        }
#endif
    }
}
