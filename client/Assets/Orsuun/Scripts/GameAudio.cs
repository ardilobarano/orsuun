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
                m.loop = true;
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
                clip = Resources.Load<AudioClip>("Audio/" + name);
                _clips[name] = clip;
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

        /// <summary>Crossfades to a music track (Resources/Audio/Music*.wav), over about a second and a half.</summary>
        public void Music(string name)
        {
            AudioClip clip = Clip(name);
            if (clip == null || (_musicNow != null && _musicNow.clip == clip)) return;
            AudioSource next = _musicNow == _musicA ? _musicB : _musicA;
            next.clip = clip;
            next.volume = 0f;
            next.Play();
            _musicNow = next;
            _fade = 0f;
        }

        private void Update()
        {
            if (_musicNow == null || _fade >= 1f) return;
            _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / 1.5f);
            AudioSource other = _musicNow == _musicA ? _musicB : _musicA;
            _musicNow.volume = MusicVolume * _fade;
            other.volume = MusicVolume * (1f - _fade);
            if (_fade >= 1f) other.Stop();
        }
    }
}
