using System.Collections.Generic;
using UnityEngine;

namespace Orsuun.Client
{
    /// <summary>
    /// Music and sound effects. Clips live in Resources/Audio (generated with Mirelo and Sonilo, trimmed and
    /// normalised). Each effect has a minimum gap so a pack fight does not stack fifty hits into noise; music
    /// crossfades between the title theme and the hunt loop. The sound switch is remembered in PlayerPrefs.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string MutedKey = "orsuun.muted";
        private const float MusicVolume = 0.32f;
        private const int Voices = 10;

        public static GameAudio Instance { get; private set; }

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private AudioSource _musicA, _musicB;
        private AudioSource _musicNow;
        private float _fade = 1f;
        /// <summary>-musiclog writes each track change to the log (the Mac player cannot be listened to headless).</summary>
        private static readonly bool MusicLog = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-musiclog") >= 0;
        private float _fadeSeconds = 1.5f;
        /// <summary>A track starts again this long before its end, crossfading over LoopFade (the generated themes fade
        /// out over their last seconds, so a plain loop would dip).</summary>
        private const float LoopTail = 6f, LoopFade = 4f;

        /// <summary>
        /// The generated themes (ElevenLabs Music, 27 Sep 2026), measured: a gain in dB that brings each to about -17.5 dBFS,
        /// and where a repeat starts for one that fades in (the swamp's first four seconds rise from silence).
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, (float db, float start)> Tracks =
            new System.Collections.Generic.Dictionary<string, (float, float)>
            {
                ["MusicMap02"] = (-1.3f, 0f), ["MusicMap04"] = (2.7f, 0f), ["MusicMap05"] = (1.1f, 0f), ["MusicMap06"] = (-0.8f, 0f),
                ["MusicMap07"] = (-2.6f, 0f), ["MusicMap08"] = (0.4f, 4f), ["MusicMap10"] = (2.9f, 0f), ["MusicMap11"] = (-2.6f, 0f),
            };

        private static float Gain(AudioClip clip) => clip != null && Tracks.TryGetValue(clip.name, out var t) ? Mathf.Pow(10f, t.db / 20f) : 1f;
        private int _next;

        public bool Muted { get; private set; }

        public static GameAudio Create()
        {
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameAudio>();
            Instance.Init();
            return Instance;
        }

        private void Init()
        {
            try { Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1; } catch { Muted = false; }
            for (int i = 0; i < Voices; i++)
            {
                AudioSource s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                _voices.Add(s);
            }
            _musicA = gameObject.AddComponent<AudioSource>();
            _musicB = gameObject.AddComponent<AudioSource>();
            foreach (AudioSource m in new[] { _musicA, _musicB })
            {
                m.loop = false;   // Update loops each track into itself (LoopTail)
                m.playOnAwake = false;
                m.volume = 0f;
            }
            AudioListener.volume = Muted ? 0f : 1f;
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            AudioListener.volume = Muted ? 0f : 1f;
            try { PlayerPrefs.SetInt(MutedKey, Muted ? 1 : 0); } catch { }
        }

        private AudioClip Clip(string name)
        {
            if (!_clips.TryGetValue(name, out AudioClip clip))
            {
                // Sounds live in Resources/Audio; the map music and stings are downloaded art (Content/Music).
                clip = Resources.Load<AudioClip>("Audio/" + name) ?? Art.Load<AudioClip>("Music/" + name);
                if (clip != null) _clips[name] = clip;   // a miss is asked again (the art may still be downloading)
            }
            return clip;
        }

        /// <summary>Plays an effect unless the same one played less than minGap seconds ago.</summary>
        public void Play(string name, float volume = 1f, float minGap = 0.05f, float pitchJitter = 0.06f)
        {
            float now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(name, out float last) && now - last < minGap) return;
            AudioClip clip = Clip(name);
            if (clip == null) return;
            _lastPlayed[name] = now;
            AudioSource voice = _voices[_next];
            _next = (_next + 1) % _voices.Count;
            voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            voice.PlayOneShot(clip, volume);
        }

        /// <summary>Crossfades to a music track (Resources/Audio or the downloaded Content/Music) over about a second and a
        /// half; a track not there yet (art still downloading) plays <paramref name="fallback"/>.</summary>
        public void Music(string name, string fallback = null)
        {
            AudioClip clip = Clip(name) ?? (fallback != null ? Clip(fallback) : null);
            if (clip == null || (_musicNow != null && _musicNow.clip == clip)) return;
            StartMusic(clip, 1.5f);
        }

        private void StartMusic(AudioClip clip, float fadeSeconds, float from = 0f)
        {
            AudioSource next = _musicNow == _musicA ? _musicB : _musicA;
            next.clip = clip;
            next.time = from;
            if (MusicLog) Debug.Log("MUSIC " + clip.name + " from " + from.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            next.volume = 0f;
            next.Play();
            _musicNow = next;
            _fade = 0f;
            _fadeSeconds = fadeSeconds;
        }

        private void Update()
        {
            // The track runs into itself before its end.
            if (_musicNow != null && _musicNow.clip != null && _musicNow.clip.length > LoopTail * 2f && _fade >= 1f
                && _musicNow.time >= _musicNow.clip.length - LoopTail)
                StartMusic(_musicNow.clip, LoopFade, Tracks.TryGetValue(_musicNow.clip.name, out var t) ? t.start : 0f);
            if (_musicNow == null || _fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / _fadeSeconds);
            AudioSource other = _musicNow == _musicA ? _musicB : _musicA;
            _musicNow.volume = MusicVolume * Gain(_musicNow.clip) * _fade;
            other.volume = MusicVolume * Gain(other.clip) * (1f - _fade);
            if (_fade >= 1f) other.Stop();
        }
    }
}
