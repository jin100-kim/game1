using System.Collections.Generic;
using EJR.Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace EJR.Game.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        private sealed class ActiveVoice
        {
            public AudioSource Source;
            public AudioCueCatalog.Entry Entry;
            public float VolumeScale;
            public AudioBus Bus;
            public float StopAtUnscaledTime;
        }

        private const string CatalogResourcePath = "AudioCueCatalog";
        private const string MasterVolumeKey = "settings.audio.master";
        private const string BgmVolumeKey = "settings.audio.bgm";
        private const string SfxVolumeKey = "settings.audio.sfx";
        private const float DefaultMasterVolume = 1f;
        private const float DefaultBgmVolume = 0.75f;
        private const float DefaultSfxVolume = 0.9f;
        private const int SfxVoicePoolSize = 12;
        private const int UiVoicePoolSize = 4;

        private static AudioService s_instance;

        private readonly Dictionary<AudioCueId, float> _lastCuePlayedAt = new();
        private readonly List<ActiveVoice> _sfxVoices = new(SfxVoicePoolSize);
        private readonly List<ActiveVoice> _uiVoices = new(UiVoicePoolSize);

        private AudioCueCatalog _catalog;
        private AudioSource _musicSource;
        private AudioCueId _activeMusicCue = AudioCueId.None;
        private float _masterVolume = DefaultMasterVolume;
        private float _bgmVolume = DefaultBgmVolume;
        private float _sfxVolume = DefaultSfxVolume;
        private bool _nonBgmPaused;

        public static AudioService Instance => EnsureInstance();
        public static bool HasInstance => s_instance != null;

        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;
        public bool NonBgmPaused => _nonBgmPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static AudioService EnsureInstance()
        {
            if (s_instance != null)
            {
                return s_instance;
            }

            s_instance = FindFirstObjectByType<AudioService>();
            if (s_instance != null)
            {
                return s_instance;
            }

            var root = new GameObject("AudioService");
            s_instance = root.AddComponent<AudioService>();
            return s_instance;
        }

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _catalog = Resources.Load<AudioCueCatalog>(CatalogResourcePath);
            LoadSettings();
            BuildSources();
            ApplySettingsToLiveSources();
            SetNonBgmPaused(false);
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                AudioListener.pause = false;
            }
        }

        private void Update()
        {
            if (_nonBgmPaused)
            {
                return;
            }

            CleanupInactiveVoices(_sfxVoices);
            CleanupInactiveVoices(_uiVoices);
        }

        public void PlayUi(AudioCueId cueId)
        {
            PlayCue(cueId, _uiVoices, AudioBus.Ui, 1f);
        }

        public void PlaySfx(AudioCueId cueId, float volumeScale = 1f)
        {
            PlayCue(cueId, _sfxVoices, AudioBus.Sfx, volumeScale);
        }

        public void PlaySfxClip(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (_nonBgmPaused || clip == null)
            {
                return;
            }

            var voice = GetAvailableVoice(_sfxVoices);
            if (voice == null || voice.Source == null)
            {
                return;
            }

            voice.Entry = null;
            voice.Bus = AudioBus.Sfx;
            voice.VolumeScale = Mathf.Clamp01(volumeScale);
            voice.StopAtUnscaledTime = 0f;

            voice.Source.Stop();
            voice.Source.clip = clip;
            voice.Source.loop = false;
            voice.Source.pitch = Mathf.Max(0.01f, pitch);
            voice.Source.outputAudioMixerGroup = GetResolvedBusGroup(AudioBus.Sfx);
            voice.Source.volume = ComputeFinalVolume(null, AudioBus.Sfx, voice.VolumeScale);
            voice.Source.Play();
        }

        public void PlayWeaponSound(WeaponSoundRequest request)
        {
            switch (request.WeaponId)
            {
                case WeaponUpgradeId.Fireball:
                    PlaySfx(AudioCueId.WeaponFireball);
                    break;
                case WeaponUpgradeId.Slash:
                    PlaySfx(AudioCueId.WeaponKatana);
                    break;
                case WeaponUpgradeId.LightningBolt:
                    PlaySfx(AudioCueId.WeaponChainAttack);
                    break;
                case WeaponUpgradeId.IceSpike:
                    PlaySfx(AudioCueId.WeaponRifle, 0.8f);
                    break;
                case WeaponUpgradeId.WindBlade:
                    PlaySfx(AudioCueId.WeaponKatana, 1.2f);
                    break;
                case WeaponUpgradeId.Bubble:
                    PlaySfx(AudioCueId.WeaponBatFlap, 1.5f);
                    break;
            }
        }

        public void PlayMusic(AudioCueId cueId)
        {
            if (_musicSource == null)
            {
                return;
            }

            if (!_catalogReady || !_catalog.TryGetEntry(cueId, out var entry) || entry.clip == null)
            {
                return;
            }

            if (_activeMusicCue == cueId && _musicSource.isPlaying && _musicSource.clip == entry.clip)
            {
                RefreshMusicSource(entry);
                return;
            }

            _activeMusicCue = cueId;
            _musicSource.clip = entry.clip;
            _musicSource.loop = true;
            _musicSource.pitch = 1f;
            _musicSource.outputAudioMixerGroup = entry.mixerGroup != null ? entry.mixerGroup : _catalog.GetBusGroup(AudioBus.Bgm);
            _musicSource.volume = ComputeFinalVolume(entry, AudioBus.Bgm, 1f);
            _musicSource.Play();
        }

        public void StopMusic()
        {
            _activeMusicCue = AudioCueId.None;
            if (_musicSource != null)
            {
                _musicSource.Stop();
            }
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, _masterVolume);
            PlayerPrefs.Save();
            ApplySettingsToLiveSources();
        }

        public void SetBgmVolume(float value)
        {
            _bgmVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(BgmVolumeKey, _bgmVolume);
            PlayerPrefs.Save();
            ApplySettingsToLiveSources();
        }

        public void SetSfxVolume(float value)
        {
            _sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, _sfxVolume);
            PlayerPrefs.Save();
            ApplySettingsToLiveSources();
        }

        public void SetNonBgmPaused(bool paused)
        {
            if (_nonBgmPaused == paused && AudioListener.pause == paused)
            {
                return;
            }

            _nonBgmPaused = paused;
            if (_musicSource != null)
            {
                _musicSource.ignoreListenerPause = true;
            }

            SetVoicePoolListenerPauseBypass(_sfxVoices, false);
            SetVoicePoolListenerPauseBypass(_uiVoices, false);
            AudioListener.pause = paused;
        }

        private bool _catalogReady => _catalog != null;

        private void LoadSettings()
        {
            _masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
            _bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
        }

        private void BuildSources()
        {
            _musicSource = CreateSource("Music", _catalog != null ? _catalog.GetBusGroup(AudioBus.Bgm) : null);
            _musicSource.loop = true;
            _musicSource.ignoreListenerPause = true;

            for (var i = 0; i < SfxVoicePoolSize; i++)
            {
                _sfxVoices.Add(new ActiveVoice
                {
                    Source = CreateSource($"SfxVoice{i + 1}", _catalog != null ? _catalog.GetBusGroup(AudioBus.Sfx) : null),
                    Bus = AudioBus.Sfx,
                    VolumeScale = 1f,
                });
            }

            for (var i = 0; i < UiVoicePoolSize; i++)
            {
                _uiVoices.Add(new ActiveVoice
                {
                    Source = CreateSource($"UiVoice{i + 1}", _catalog != null ? _catalog.GetBusGroup(AudioBus.Ui) : null),
                    Bus = AudioBus.Ui,
                    VolumeScale = 1f,
                });
            }
        }

        private AudioSource CreateSource(string name, AudioMixerGroup outputGroup)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = false;
            source.spatialBlend = 0f;
            source.loop = false;
            source.outputAudioMixerGroup = outputGroup;
            return source;
        }

        private void PlayCue(AudioCueId cueId, List<ActiveVoice> pool, AudioBus fallbackBus, float volumeScale)
        {
            if (_nonBgmPaused)
            {
                return;
            }

            if (!_catalogReady || !_catalog.TryGetEntry(cueId, out var entry) || entry.clip == null)
            {
                return;
            }

            if (!CanPlayCue(cueId, entry, pool))
            {
                return;
            }

            var voice = GetAvailableVoice(pool);
            if (voice == null)
            {
                return;
            }

            voice.Entry = entry;
            voice.Bus = entry.bus;
            voice.VolumeScale = volumeScale;
            voice.StopAtUnscaledTime = entry.loop || entry.maxPlaybackDuration <= 0f
                ? 0f
                : Time.unscaledTime + entry.maxPlaybackDuration;
            voice.Source.clip = entry.clip;
            voice.Source.loop = entry.loop;
            voice.Source.pitch = 1f + Random.Range(-entry.pitchVariance, entry.pitchVariance);
            voice.Source.outputAudioMixerGroup = entry.mixerGroup != null ? entry.mixerGroup : GetResolvedBusGroup(entry.bus);
            voice.Source.volume = ComputeFinalVolume(entry, fallbackBus, volumeScale);
            voice.Source.Play();
            _lastCuePlayedAt[cueId] = Time.unscaledTime;
        }

        private bool CanPlayCue(AudioCueId cueId, AudioCueCatalog.Entry entry, List<ActiveVoice> pool)
        {
            if (_lastCuePlayedAt.TryGetValue(cueId, out var lastPlayedAt)
                && entry.minRetriggerInterval > 0f
                && Time.unscaledTime < lastPlayedAt + entry.minRetriggerInterval)
            {
                return false;
            }

            var activeCount = 0;
            for (var i = 0; i < pool.Count; i++)
            {
                var voice = pool[i];
                if (voice.Entry != null && voice.Entry.cueId == cueId && voice.Source != null && voice.Source.isPlaying)
                {
                    activeCount++;
                }
            }

            return activeCount < Mathf.Max(1, entry.maxVoices);
        }

        private static ActiveVoice GetAvailableVoice(List<ActiveVoice> pool)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var voice = pool[i];
                if (voice.Source == null || voice.Source.isPlaying)
                {
                    continue;
                }

                return voice;
            }

            return pool.Count > 0 ? pool[0] : null;
        }

        private void CleanupInactiveVoices(List<ActiveVoice> pool)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var voice = pool[i];
                if (voice.Source == null)
                {
                    continue;
                }

                if (voice.Source.isPlaying
                    && voice.StopAtUnscaledTime > 0f
                    && Time.unscaledTime >= voice.StopAtUnscaledTime)
                {
                    voice.Source.Stop();
                }

                if (voice.Source.isPlaying)
                {
                    continue;
                }

                voice.Entry = null;
                voice.VolumeScale = 1f;
                voice.StopAtUnscaledTime = 0f;
            }
        }

        private void ApplySettingsToLiveSources()
        {
            if (_musicSource != null && _catalogReady && _catalog.TryGetEntry(_activeMusicCue, out var musicEntry))
            {
                RefreshMusicSource(musicEntry);
            }

            RefreshVoicePool(_sfxVoices);
            RefreshVoicePool(_uiVoices);
        }

        private void RefreshMusicSource(AudioCueCatalog.Entry entry)
        {
            if (_musicSource == null || entry == null)
            {
                return;
            }

            _musicSource.ignoreListenerPause = true;
            _musicSource.outputAudioMixerGroup = entry.mixerGroup != null ? entry.mixerGroup : _catalog.GetBusGroup(AudioBus.Bgm);
            _musicSource.volume = ComputeFinalVolume(entry, AudioBus.Bgm, 1f);
        }

        private static void SetVoicePoolListenerPauseBypass(List<ActiveVoice> pool, bool ignoreListenerPause)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var source = pool[i].Source;
                if (source != null)
                {
                    source.ignoreListenerPause = ignoreListenerPause;
                }
            }
        }

        private void RefreshVoicePool(List<ActiveVoice> pool)
        {
            for (var i = 0; i < pool.Count; i++)
            {
                var voice = pool[i];
                if (voice.Source == null)
                {
                    continue;
                }

                if (voice.Entry != null)
                {
                    voice.Source.outputAudioMixerGroup = voice.Entry.mixerGroup != null ? voice.Entry.mixerGroup : GetResolvedBusGroup(voice.Entry.bus);
                    voice.Source.volume = ComputeFinalVolume(voice.Entry, voice.Bus, voice.VolumeScale);
                    continue;
                }

                voice.Source.outputAudioMixerGroup = GetResolvedBusGroup(voice.Bus);
                voice.Source.volume = ComputeFinalVolume(null, voice.Bus, voice.VolumeScale);
            }
        }

        private AudioMixerGroup GetResolvedBusGroup(AudioBus bus)
        {
            return _catalog != null ? _catalog.GetBusGroup(bus) : null;
        }

        private float ComputeFinalVolume(AudioCueCatalog.Entry entry, AudioBus fallbackBus, float volumeScale)
        {
            var bus = entry != null ? entry.bus : fallbackBus;
            var busVolume = bus == AudioBus.Bgm ? _bgmVolume : _sfxVolume;
            var cueVolume = entry != null ? Mathf.Clamp01(entry.volume) : 1f;
            return Mathf.Clamp01(_masterVolume * busVolume * cueVolume * Mathf.Clamp01(volumeScale));
        }
    }
}
