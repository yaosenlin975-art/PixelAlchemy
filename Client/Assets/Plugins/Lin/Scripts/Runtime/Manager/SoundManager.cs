/*
┌────────────────────────────┐
│　Description: 音效控制
│　Remark: 
└────────────────────────────┘
┌──────────────┐                                   
│　ClassName: SoundManager
└──────────────┘
*/
using Cysharp.Threading.Tasks;
using Lin.Runtime.DesignPattern.Singleton;
using DG.Tweening;
using UnityEngine;
using Lin.Runtime.Resource;
using UnityEngine.Pool;
using System.Collections.Generic;
using Lin.Runtime.Helper;
using System.Threading.Tasks;

namespace Lin.Runtime.Manager
{
    public class SoundManager : MonoSingleton<SoundManager>
    {
        #region - Fields -

        private SingleSound bgmSource;

        /// <summary> The volume of the audio source (0.0 to 1.0) </summary>
        public float BgmVolume
        {
            get => bgmSource.Volume;
            set
            {
                bgmSource.Volume = value;
                PrefsHelper.Set(nameof(BgmVolume), value);
            }
        }

        private SingleSound envSource;

        /// <summary> The volume of the audio source (0.0 to 1.0) </summary>
        public float EnvVolume
        {
            get => envSource.Volume;
            set
            {
                envSource.Volume = value;
                PrefsHelper.Set(nameof(EnvVolume), value);
            }
        }

        private ObjectPool<AudioSource> effectSources;
        private List<AudioSource> usingEffects;

        private ObjectPool<AudioSource> voiceSources;
        private List<AudioSource> usingVoices;

        private float effectVolume = 1f;
        /// <summary> The volume of the audio source (0.0 to 1.0) </summary>
        public float EffectVolume
        {
            get => effectVolume;
            set
            {
                foreach (var audio in usingEffects)
                    audio.volume = value;

                effectVolume = value;
                PrefsHelper.Set(nameof(EffectVolume), value);
            }
        }

        private float voiceVolume = 1f;
        /// <summary> The volume of the audio source (0.0 to 1.0) </summary>
        public float VoiceVolume
        {
            get => voiceVolume;
            set
            {
                foreach (var audio in usingVoices)
                    audio.volume = value;

                voiceVolume = value;
                PrefsHelper.Set(nameof(VoiceVolume), value);
            }
        }

        #endregion

        #region - Life Cycle -

        protected override void Init()
        {
            var bgmAudio = CreateSource("Background Source", true);
            bgmSource = new SingleSound(bgmAudio);

            var envAudio = CreateSource("Environment Source", true);
            envSource = new SingleSound(envAudio);

            effectSources = new ObjectPool<AudioSource>(CreateEffect, OnEffectGet, OnEffectRelease);
            usingEffects = new List<AudioSource>();

            voiceSources = new ObjectPool<AudioSource>(CreateVoice, OnVoiceGet, OnVoiceRelease);
            usingVoices = new List<AudioSource>();

            BgmVolume = PrefsHelper.Get(nameof(BgmVolume), 1);
            EnvVolume = PrefsHelper.Get(nameof(EnvVolume), 1);
            EffectVolume = PrefsHelper.Get(nameof(EffectVolume), 1);
            VoiceVolume = PrefsHelper.Get(nameof(VoiceVolume), 1);
        }

        private void OnVoiceRelease(AudioSource source) => usingVoices.Remove(source);

        private void OnVoiceGet(AudioSource source) => usingVoices.Add(source);

        private void OnEffectRelease(AudioSource source) => usingEffects.Remove(source);

        private void OnEffectGet(AudioSource source) => usingEffects.Add(source);

        private AudioSource CreateSource(string name, bool loop)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(transform, false);
            var result = gameObject.AddComponent<AudioSource>();
            result.loop = loop;
            return result;
        }

        private AudioSource CreateVoice() => CreateSource("Effect Source", false);

        private AudioSource CreateEffect() => CreateSource("Voice Source", false);

        #endregion

        #region - Public Methods -

        public async UniTask PlayMusicAsync(string musicPath)
        {
            var clip = await ResLoader.LoadAudioClipAsync(musicPath);
            await bgmSource.PlayAsync(clip);
        }

        public async UniTask PlayEffectAsync(string clipPath, Vector3 position, float volumeScale = 1)
        {
            var clip = await ResLoader.LoadAudioClipAsync(clipPath);
            await Play(clip, volumeScale, position, effectSources);
        }

        public void PlayEffect(AudioClip clip, Vector3 position, float volumeScale = 1) => Play(clip, volumeScale, position, effectSources).Forget();

        public void PlayVoice(AudioClip clip, Vector3 position, float volumeScale = 1) => Play(clip, volumeScale, position, voiceSources).Forget();

        private async UniTask Play(AudioClip clip, float volumeScale, Vector3 position, ObjectPool<AudioSource> pool)
        {
            using var item = pool.Get(out var source);
            source.transform.position = position;
            source.clip = clip;
            source.Play();
            await UniTask.WaitForSeconds(clip.length);
        }

        #endregion

        private struct SingleSound
        {
            public SingleSound(AudioSource audio)
            {
                this.audio = audio;
                volume = 1;
            }

            private AudioSource audio;
            private float volume;

            public float Volume
            {
                get => volume;
                set
                {
                    volume = value;
                    audio.volume = value;
                }
            }

            public async UniTask PlayAsync(AudioClip clip)
            {
                if (audio.isPlaying)
                {
                    audio.DOFade(0, 0.5f);
                    await UniTask.Delay(500);
                    audio.Stop();
                }
                else
                    audio.volume = 0;

                audio.clip = clip;
                audio.Play();
                audio.DOFade(volume, 0.5f);
            }
        }
    }
}
