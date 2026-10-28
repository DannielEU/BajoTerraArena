using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>Plays the game's sounds through a small pool of audio sources, in 3D or as interface sounds.</summary>
    public sealed class AudioPlayer
    {
        private const int Voices = 24;

        private readonly AudioSource[] _sources = new AudioSource[Voices];
        private int _next;

        /// <summary>The recorded blaster shots (<c>Resources/Sounds/Shots</c>); every shot plays one of them.</summary>
        public readonly AudioClip[] Shots;
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
                // Real-world falloff: full volume up to 4 m from the camera (your own hero is ~3.5 m in front of
                // it), then half as loud each time the distance doubles: -8 dB at 10 m, -22 dB across the cavern.
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 4f;
                source.maxDistance = 75f;
                // Sounds stay where they started; without this, moving a pooled voice to a new sound reads as a huge
                // velocity and the Doppler effect warps the start of the sound.
                source.dopplerLevel = 0f;
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

            Shots = Resources.LoadAll<AudioClip>("Sounds/Shots");
            if (Shots.Length == 0)
            {
                Shots = new[] { ProceduralAudio.Shot(440f) };
            }
            // Imported clips are not preloaded by default; load them now so the first shot does not stall to decode.
            foreach (AudioClip shot in Shots)
            {
                shot.LoadAudioData();
            }
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
            // Prefer an idle voice, so a short sound does not cut off a shot (up to two seconds long) still ringing.
            int voice = _next;
            for (int i = 0; i < Voices; i++)
            {
                int candidate = (_next + i) % Voices;
                if (!_sources[candidate].isPlaying)
                {
                    voice = candidate;
                    break;
                }
            }
            _next = (voice + 1) % Voices;
            AudioSource source = _sources[voice];
            source.transform.position = position;
            source.spatialBlend = spatial;
            source.pitch = pitch;
            source.volume = volume;
            source.clip = clip;
            source.Play();
        }
    }
}
