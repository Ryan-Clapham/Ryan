using System.Collections.Generic;
using ColourCascade.Core;
using ColourCascade.Procedural;
using UnityEngine;

namespace ColourCascade.App
{
    /// <summary>Plays the synthesised effects through a small pool of sources, plus the music loop.
    /// Volumes follow the player's settings.</summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int SourceCount = 8;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        AudioSource[] sources;
        AudioSource music;
        int next;
        GameSettings settings;
        bool musicWanted = true;

        public static AudioService Instance { get; private set; }

        public void Init(GameSettings gameSettings, List<SoundData> effects, SoundData musicData)
        {
            Instance = this;
            settings = gameSettings;
            sources = new AudioSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
            }
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.loop = true;
            foreach (var s in effects) clips[s.Name] = ToClip(s);
            if (musicData != null)
            {
                music.clip = ToClip(musicData);
            }
            settings.Changed += OnSettingChanged;
            ApplyVolumes();
            if (musicWanted && music.clip != null) music.Play();
        }

        void OnDestroy()
        {
            if (settings != null) settings.Changed -= OnSettingChanged;
        }

        static AudioClip ToClip(SoundData s)
        {
            var clip = AudioClip.Create(s.Name, s.Samples.Length, 1, s.SampleRate, false);
            clip.SetData(s.Samples, 0);
            return clip;
        }

        void OnSettingChanged(SettingKind kind)
        {
            if (kind == SettingKind.SfxVolume || kind == SettingKind.MusicVolume) ApplyVolumes();
        }

        public void RefreshVolumes() => ApplyVolumes();

        void ApplyVolumes()
        {
            if (music == null) return;
            music.volume = settings.MusicVolume * 0.55f;
            if (settings.MusicVolume <= 0.001f)
            {
                if (music.isPlaying) music.Pause();
            }
            else if (musicWanted && music.clip != null && !music.isPlaying)
            {
                music.Play();
            }
        }

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (sources == null || settings == null || settings.SfxVolume <= 0.001f) return;
            AudioClip clip;
            if (!clips.TryGetValue(name, out clip)) return;
            float now = Time.unscaledTime;
            float last;
            if (lastPlayed.TryGetValue(name, out last) && now - last < 0.035f) return;
            lastPlayed[name] = now;
            var src = sources[next];
            next = (next + 1) % sources.Length;
            src.pitch = pitch;
            src.PlayOneShot(clip, Mathf.Clamp01(volume * settings.SfxVolume));
        }

        /// <summary>Pop sound whose pitch climbs with group size.</summary>
        public void PlayPop(int groupSize)
        {
            int idx = Mathf.Clamp((groupSize - 2) / 2, 0, SfxNames.PopVariants - 1);
            Play(SfxNames.Pop(idx), 0.9f, 1f);
        }

        public void SetMusicEnabled(bool on)
        {
            musicWanted = on;
            if (music == null) return;
            if (!on) music.Pause();
            else ApplyVolumes();
        }

        public static void Sfx(string name, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.Play(name, volume, pitch);
        }
    }

    /// <summary>Short vibrations on Android (amplitude-controlled on Android 8+). Respects the Haptics setting.</summary>
    public static class Haptics
    {
        public static GameSettings Settings;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vibrator;
        static int sdk = -1;
        static bool failed;
#endif

        public static void Light() => Vibrate(12, 60);
        public static void Medium() => Vibrate(22, 140);
        public static void Heavy() => Vibrate(40, 255);

        static void Vibrate(long ms, int amplitude)
        {
            if (Settings == null || !Settings.Haptics) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (failed) return;
            try
            {
                if (vibrator == null)
                {
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        sdk = version.GetStatic<int>("SDK_INT");
                }
                if (vibrator == null) return;
                if (sdk >= 26)
                {
                    using (var effect = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var oneShot = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                        vibrator.Call("vibrate", oneShot);
                }
                else
                {
                    vibrator.Call("vibrate", ms);
                }
            }
            catch (System.Exception e)
            {
                failed = true;
                Debug.LogWarning("[ColourCascade] Haptics unavailable: " + e.Message);
                // Referencing Handheld.Vibrate also makes Unity add the VIBRATE permission to the manifest.
                Handheld.Vibrate();
            }
#endif
        }
    }
}
