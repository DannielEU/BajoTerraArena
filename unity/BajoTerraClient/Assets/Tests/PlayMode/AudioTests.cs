using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Bajoterra.Unity.Tests
{
    /// <summary>
    /// Every blaster shot sounds like one of the recordings in the repository's <c>sound/</c> folder (imported into
    /// <c>Resources/Sounds/Shots</c> by <c>unity/scripts/import-shot-sounds.sh</c>), not the synthesised stand-in,
    /// and each recording is heard the moment the trigger is pulled, louder the closer the shooter is.
    /// </summary>
    public sealed class AudioTests
    {
        private GameObject _host;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // The game starts itself in Play mode; its camera's listener and cave ambience would blur the measurements.
            if (GameApp.Instance != null)
            {
                Object.Destroy(GameApp.Instance.gameObject);
            }
            yield return null;
            _host = new GameObject("Audio host");
        }

        [TearDown]
        public void Teardown()
        {
            Object.Destroy(_host);
        }

        [Test]
        public void ShotsAreTheRecordingsInTheSoundFolder()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "sound"));
            string[] recordings = Directory.GetFiles(folder, "*.mp3")
                .Select(Path.GetFileNameWithoutExtension)
                .ToArray();

            var audio = new AudioPlayer(_host.transform);

            CollectionAssert.AreEquivalent(recordings, audio.Shots.Select(clip => clip.name).ToArray(),
                "run unity/scripts/import-shot-sounds.sh after changing sound/");
        }

        [Test]
        public void EveryShotStartsWithSoundNotSilence()
        {
            var audio = new AudioPlayer(_host.transform);

            foreach (AudioClip clip in audio.Shots)
            {
                Assert.AreEqual(AudioDataLoadState.Loaded, clip.loadState, clip.name + " is loaded before the first shot");
                Assert.AreEqual(1, clip.channels, clip.name + " is mono, so it is placed in 3D at the muzzle");
                var firstMilliseconds = new float[clip.frequency / 50];
                Assert.IsTrue(clip.GetData(firstMilliseconds, 0), clip.name + " samples can be read");
                Assert.Greater(firstMilliseconds.Max(Mathf.Abs), 0.01f, clip.name + " is audible in its first 20 ms");
            }
        }

        [UnityTest]
        public IEnumerator AShotFadesWithDistance()
        {
            var near = new float[1];
            var far = new float[1];
            yield return LoudestShot(4f, near);
            yield return LoudestShot(40f, far);

            Assume.That(near[0], Is.GreaterThan(0.01f), "no audio output to measure (audio device disabled?)");
            float decibels = 20f * Mathf.Log10(far[0] / near[0]);
            Assert.Less(decibels, -14f, "a shot 40 m away must sound much weaker than one 4 m away");
        }

        /// <summary>Plays a shot straight ahead of a listener and keeps the loudest sample the listener outputs.</summary>
        private IEnumerator LoudestShot(float metres, float[] loudest)
        {
            var listener = new GameObject("Listener", typeof(AudioListener));
            listener.transform.SetParent(_host.transform, false);
            var audio = new AudioPlayer(listener.transform) { AmbienceVolume = 0f };
            audio.PlayAt(audio.Shots[0], new Vector3(0f, 0f, metres));

            var samples = new float[4096];
            float end = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
                for (int channel = 0; channel < 2; channel++)
                {
                    AudioListener.GetOutputData(samples, channel);
                    loudest[0] = Mathf.Max(loudest[0], samples.Max(Mathf.Abs));
                }
            }
            Object.Destroy(listener);
            yield return null;
        }
    }
}
