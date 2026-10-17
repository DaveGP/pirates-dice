using System.Collections.Generic;
using UnityEngine;

namespace PiratesDice.Game.Audio
{
    /// <summary>Plays music and sound effects, and remembers whether the player muted either.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const string MusicKey = "PiratesDice.Music";
        const string SfxKey = "PiratesDice.Sfx";

        public static AudioDirector Instance { get; private set; }

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        AudioSource _music;
        AudioSource _sfx;

        public bool MusicOn
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(MusicKey, value ? 1 : 0);
                PlayerPrefs.Save();
                ApplyMusic();
            }
        }

        public bool SfxOn
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SfxKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        void Awake()
        {
            Instance = this;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.45f;
            _music.clip = SoundBank.Shanty();

            foreach (Sfx sfx in System.Enum.GetValues(typeof(Sfx))) _clips[sfx] = SoundBank.Build(sfx);
            ApplyMusic();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f)
        {
            AudioDirector a = Instance;
            if (a == null || !a.SfxOn) return;
            a._sfx.pitch = pitch;
            a._sfx.PlayOneShot(a._clips[sfx], volume);
        }

        void ApplyMusic()
        {
            if (MusicOn && !_music.isPlaying) _music.Play();
            else if (!MusicOn && _music.isPlaying) _music.Stop();
        }
    }
}
