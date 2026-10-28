using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>Plays the procedural sounds through a small pool of audio sources, in 3D or as interface sounds.</summary>
    public sealed class AudioPlayer
    {
        private const int Voices = 16;

        private readonly AudioSource[] _sources = new AudioSource[Voices];
        private int _next;

        public readonly AudioClip[] ShotBySound;
        public readonly AudioClip Hit = ProceduralAudio.Hit();
        public readonly AudioClip WorldHit = ProceduralAudio.WorldHit();
        public readonly AudioClip Explosion = ProceduralAudio.Explosion();
        public readonly AudioClip Heal = ProceduralAudio.Heal();
        public readonly AudioClip Shield = ProceduralAudio.Shield();
        public readonly AudioClip Pickup = ProceduralAudio.Pickup();
        public readonly AudioClip Defeat = ProceduralAudio.Defeat();
        public readonly AudioClip Jump = ProceduralAudio.Jump();
        public readonly AudioClip Roll = ProceduralAudio.Roll();
        public readonly AudioClip CountdownBeep = ProceduralAudio.Beep(660f, 0.18f);
        public readonly AudioClip GoBeep = ProceduralAudio.Beep(990f, 0.35f);
        public readonly AudioClip Whistle = ProceduralAudio.Whistle();
        public readonly AudioClip Click = ProceduralAudio.Click();
        public readonly AudioClip Hover = ProceduralAudio.Hover();
        public readonly AudioClip HitMarker = ProceduralAudio.HitMarker();
        public readonly AudioClip Victory = ProceduralAudio.Victory();
        public readonly AudioClip Loss = ProceduralAudio.Loss();

        private readonly AudioSource _ambience;

        /// <summary>Master volume of the whole game, 0 to 1.</summary>
        public float MasterVolume
        {
            get { return AudioListener.volume; }
            set { AudioListener.volume = Mathf.Clamp01(value); }
        }

        /// <summary>Volume of the cavern drone, 0 to 1.</summary>
        public float AmbienceVolume
        {
            get { return _ambience.volume; }
            set { _ambience.volume = Mathf.Clamp01(value); }
        }

        public AudioPlayer(Transform parent)
        {
            var root = new GameObject("Audio");
            root.transform.SetParent(parent, false);
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice " + i, typeof(AudioSource));
                go.transform.SetParent(root.transform, false);
                AudioSource source = go.GetComponent<AudioSource>();
                source.playOnAwake = false;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 4f;
                source.maxDistance = 60f;
                _sources[i] = source;
            }
            var ambience = new GameObject("Ambience", typeof(AudioSource));
            ambience.transform.SetParent(root.transform, false);
            _ambience = ambience.GetComponent<AudioSource>();
            _ambience.clip = ProceduralAudio.CaveAmbience();
            _ambience.loop = true;
            _ambience.spatialBlend = 0f;
            _ambience.volume = 0.5f;
            _ambience.Play();

            ShotBySound = new[]
            {
                ProceduralAudio.Shot(440f), ProceduralAudio.Shot(520f), ProceduralAudio.Shot(380f),
                ProceduralAudio.Shot(600f)
            };
        }

        /// <summary>A sound located in the world.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            Play(clip, position, volume, pitch, 1f);
        }

        /// <summary>An interface sound, heard at the same level everywhere.</summary>
        public void PlayUi(AudioClip clip, float volume = 1f)
        {
            Play(clip, Vector3.zero, volume, 1f, 0f);
        }

        private void Play(AudioClip clip, Vector3 position, float volume, float pitch, float spatial)
        {
            AudioSource source = _sources[_next];
            _next = (_next + 1) % Voices;
            source.transform.position = position;
            source.spatialBlend = spatial;
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }
    }
}
