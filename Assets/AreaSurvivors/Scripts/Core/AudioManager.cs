using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AreaSurvivors
{
    public sealed class AudioManager : MonoBehaviour
    {
        const float DefaultBgmFadeSeconds = 0.45f;
        const float DefaultVolume = 0.5f;
        const float BgmOutputVolumeScale = 0.03333334f;
        const float SfxOutputVolumeScale = 0.05f;
        const float DefaultSfxCooldownSeconds = 0.06f;
        const string MasterVolumePrefsKey = "AreaSurvivors.Audio.MasterVolume";
        const string BgmVolumePrefsKey = "AreaSurvivors.Audio.BgmVolume";
        const string SfxVolumePrefsKey = "AreaSurvivors.Audio.SfxVolume";

        static readonly Dictionary<BgmTrack, AudioClip> BgmCache = new Dictionary<BgmTrack, AudioClip>();
        static readonly Dictionary<SfxTrack, AudioClip> SfxCache = new Dictionary<SfxTrack, AudioClip>();
        static readonly HashSet<BgmTrack> MissingBgmWarnings = new HashSet<BgmTrack>();
        static readonly HashSet<SfxTrack> MissingSfxWarnings = new HashSet<SfxTrack>();
        static AudioManager instance;

        AudioSource bgmSource;
        AudioSource sfxSource;
        AudioListener managedAudioListener;
        Coroutine bgmFadeRoutine;
        BgmTrack? currentBgm;
        readonly Dictionary<SfxTrack, float> lastSfxTimes = new Dictionary<SfxTrack, float>();

        public static void PlayBgm(BgmTrack track)
        {
            if (!Application.isPlaying) return;
            EnsureInstance().PlayBgmInternal(track, DefaultBgmFadeSeconds);
        }

        public static void PlaySfx(SfxTrack track, float volumeScale = 1f)
        {
            if (!Application.isPlaying) return;
            EnsureInstance().PlaySfxInternal(track, volumeScale);
        }

        public static void StopBgm()
        {
            if (!Application.isPlaying || instance == null) return;
            instance.StopBgmInternal();
        }

        public static void StopSfx()
        {
            if (!Application.isPlaying || instance == null) return;
            instance.StopSfxInternal();
        }

        public static void PlayButtonConfirm()
        {
            PlaySfx(SfxTrack.ButtonConfirm);
        }

        public static float MasterVolume
        {
            get
            {
                EnsureVolumePrefs();
                return Mathf.Min(BgmVolume, SfxVolume);
            }
            set
            {
                float volume = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(MasterVolumePrefsKey, volume);
                BgmVolume = volume;
                SfxVolume = volume;
            }
        }

        public static float BgmVolume
        {
            get
            {
                EnsureVolumePrefs();
                return Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumePrefsKey));
            }
            set
            {
                PlayerPrefs.SetFloat(BgmVolumePrefsKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                if (instance != null) instance.ApplySourceVolumes();
            }
        }

        public static float SfxVolume
        {
            get
            {
                EnsureVolumePrefs();
                return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePrefsKey));
            }
            set
            {
                PlayerPrefs.SetFloat(SfxVolumePrefsKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                if (instance != null) instance.ApplySourceVolumes();
            }
        }

        public static void ResetDefaults()
        {
            PlayerPrefs.DeleteKey(MasterVolumePrefsKey);
            PlayerPrefs.SetFloat(BgmVolumePrefsKey, DefaultVolume);
            PlayerPrefs.SetFloat(SfxVolumePrefsKey, DefaultVolume);
            PlayerPrefs.Save();
            if (instance != null) instance.ApplySourceVolumes();
        }

        static AudioManager EnsureInstance()
        {
            if (instance != null) return instance;
            if (!Application.isPlaying) return null;

            var go = new GameObject("Audio Manager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AudioManager>();
            instance.Initialize();
            return instance;
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
            Initialize();
        }

        void Initialize()
        {
            EnsureVolumePrefs();
            AudioListener.volume = 1f;
            EnsureAudioListener();
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.spatialBlend = 0f;
                bgmSource.volume = 0f;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f;
            }

            ApplySourceVolumes();
        }

        static void EnsureVolumePrefs()
        {
            if (PlayerPrefs.HasKey(BgmVolumePrefsKey) && PlayerPrefs.HasKey(SfxVolumePrefsKey)) return;

            float fallback = PlayerPrefs.HasKey(MasterVolumePrefsKey)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePrefsKey))
                : DefaultVolume;
            if (!PlayerPrefs.HasKey(BgmVolumePrefsKey)) PlayerPrefs.SetFloat(BgmVolumePrefsKey, fallback);
            if (!PlayerPrefs.HasKey(SfxVolumePrefsKey)) PlayerPrefs.SetFloat(SfxVolumePrefsKey, fallback);
            PlayerPrefs.Save();
        }

        void ApplySourceVolumes()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.volume = BgmOutputVolume;
            }

            if (sfxSource != null)
            {
                sfxSource.volume = SfxOutputVolume;
            }
        }

        void EnsureAudioListener()
        {
            if (managedAudioListener == null) managedAudioListener = GetComponent<AudioListener>();
            var listeners = FindObjectsOfType<AudioListener>(true);
            foreach (var listener in listeners)
            {
                if (listener != null && listener != managedAudioListener && listener.enabled)
                {
                    if (managedAudioListener != null) managedAudioListener.enabled = false;
                    return;
                }
            }

            if (managedAudioListener == null) managedAudioListener = gameObject.AddComponent<AudioListener>();
            managedAudioListener.enabled = true;
        }

        void PlayBgmInternal(BgmTrack track, float fadeSeconds)
        {
            Initialize();
            if (currentBgm.HasValue && currentBgm.Value == track && bgmSource.isPlaying) return;

            var clip = LoadBgm(track);
            if (clip == null) return;

            currentBgm = track;
            if (bgmFadeRoutine != null) StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = StartCoroutine(FadeToBgm(clip, fadeSeconds));
        }

        void StopBgmInternal()
        {
            Initialize();
            if (bgmFadeRoutine != null)
            {
                StopCoroutine(bgmFadeRoutine);
                bgmFadeRoutine = null;
            }

            currentBgm = null;
            if (bgmSource == null) return;

            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.volume = 0f;
        }

        void PlaySfxInternal(SfxTrack track, float volumeScale)
        {
            Initialize();
            if (IsSfxCoolingDown(track)) return;
            var clip = LoadSfx(track);
            if (clip == null) return;
            lastSfxTimes[track] = Time.unscaledTime;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale * SfxTrackVolumeScale(track)));
        }

        void StopSfxInternal()
        {
            Initialize();
            if (sfxSource == null) return;
            sfxSource.Stop();
        }

        bool IsSfxCoolingDown(SfxTrack track)
        {
            float lastTime;
            if (!lastSfxTimes.TryGetValue(track, out lastTime)) return false;
            return Time.unscaledTime - lastTime < DefaultSfxCooldownSeconds;
        }

        static AudioClip LoadBgm(BgmTrack track)
        {
            AudioClip clip;
            if (BgmCache.TryGetValue(track, out clip)) return clip;

            string path = AudioCatalog.BgmPath(track);
            clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                if (MissingBgmWarnings.Add(track))
                {
                    Debug.LogWarning("BGM clip was not found at Resources/" + path + ".");
                }

                return null;
            }

            BgmCache[track] = clip;
            return clip;
        }

        static AudioClip LoadSfx(SfxTrack track)
        {
            AudioClip clip;
            if (SfxCache.TryGetValue(track, out clip)) return clip;

            string path = AudioCatalog.SfxPath(track);
            clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                if (MissingSfxWarnings.Add(track))
                {
                    Debug.LogWarning("SFX clip was not found at Resources/" + path + ".");
                }

                return null;
            }

            SfxCache[track] = clip;
            return clip;
        }

        IEnumerator FadeToBgm(AudioClip clip, float fadeSeconds)
        {
            fadeSeconds = Mathf.Max(0f, fadeSeconds);
            if (bgmSource.clip == clip && bgmSource.isPlaying)
            {
                yield return FadeVolume(BgmOutputVolume, fadeSeconds);
                bgmFadeRoutine = null;
                yield break;
            }

            if (bgmSource.isPlaying && bgmSource.volume > 0f)
            {
                yield return FadeVolume(0f, fadeSeconds * 0.5f);
            }

            bgmSource.clip = clip;
            bgmSource.volume = 0f;
            bgmSource.Play();
            yield return FadeVolume(BgmOutputVolume, fadeSeconds);
            bgmFadeRoutine = null;
        }

        IEnumerator FadeVolume(float targetVolume, float seconds)
        {
            float startVolume = bgmSource.volume;
            if (seconds <= 0f)
            {
                bgmSource.volume = targetVolume;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            bgmSource.volume = targetVolume;
        }

        static float BgmOutputVolume => BgmOutputVolumeScale * BgmVolume;
        static float SfxOutputVolume => SfxOutputVolumeScale * SfxVolume;

        static float SfxTrackVolumeScale(SfxTrack track)
        {
            switch (track)
            {
                case SfxTrack.SlashSwing:
                case SfxTrack.ArrowShot:
                case SfxTrack.FireballCast:
                case SfxTrack.ExplosionHit:
                case SfxTrack.ShieldHit:
                case SfxTrack.BoomerangSwordThrow:
                case SfxTrack.AuraSwordCast:
                case SfxTrack.ArrowRainTick:
                case SfxTrack.GunShot:
                case SfxTrack.FrostCast:
                case SfxTrack.BossShockwaveHit:
                    return 0.45f;
                case SfxTrack.ThunderBallCast:
                case SfxTrack.GoblinLordDarkMagic:
                case SfxTrack.LichSummonMagic:
                    return 0.32f;
                case SfxTrack.TowerCollapse:
                case SfxTrack.BossDefeatRumble:
                    return 0.9f;
                case SfxTrack.RelicChestPickup:
                case SfxTrack.RelicChestOpen:
                case SfxTrack.TokenGain:
                    return 0.8f;
                case SfxTrack.StageUnlockPopup:
                    return 0.75f;
                case SfxTrack.MissionCompleteFanfare:
                case SfxTrack.MissionCompleteCheer:
                    return 0.8f;
                case SfxTrack.EnemyHit:
                    return 1.8f;
                default:
                    return 1f;
            }
        }
    }
}
