using System;
using UnityEngine;

namespace Bajoterra.Unity
{
    /// <summary>
    /// Synthesises the game's sound effects from maths so the project needs few audio files: swept tones, filtered
    /// noise and short arpeggios, each with a decay envelope. The clips are generated once at start-up (a few
    /// hundred kilobytes in total) and can be replaced by recorded sounds without touching the code that plays them,
    /// as the blaster shots already are (<c>Resources/Sounds/Shots</c>; <see cref="Shot"/> is only their fallback).
    /// </summary>
    public static class ProceduralAudio
    {
        private const int SampleRate = 44100;

        public static AudioClip Shot(float baseHz)
        {
            return Build("shot", 0.16f, t => Sweep(baseHz * 2.2f, baseHz * 0.6f, 0.16f, t) * Env(t, 0.16f, 0.004f, 5f));
        }

        public static AudioClip Hit()
        {
            return Build("hit", 0.14f, t => Noise(t) * 0.7f * Env(t, 0.14f, 0.002f, 9f)
                                            + Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.5f * Env(t, 0.14f, 0.002f, 12f));
        }

        public static AudioClip WorldHit()
        {
            return Build("worldhit", 0.12f, t => Noise(t) * Env(t, 0.12f, 0.001f, 14f) * 0.8f);
        }

        public static AudioClip Explosion()
        {
            return Build("explosion", 0.7f, t =>
            {
                float low = Mathf.Sin(2f * Mathf.PI * (70f - 40f * t) * t) * 0.8f;
                return (Noise(t) * 0.6f + low) * Env(t, 0.7f, 0.003f, 4.5f);
            });
        }

        /// <summary>
        /// A grenade going off: a sharp crack, a deep boom whose pitch sinks, a rolling rumble and pebbles raining
        /// down for a second afterwards.
        /// </summary>
        public static AudioClip GrenadeBlast()
        {
            const float seconds = 2.2f;
            float low = 0f;
            float rumble = 0f;
            return Build("grenade", seconds, t =>
            {
                float noise = Noise(t);
                // Two one-pole low-passes over the same noise: a punchy body and a much darker rumble.
                low += (noise - low) * 0.09f;
                rumble += (noise - rumble) * 0.012f;
                float crack = noise * Mathf.Exp(-t * 70f) * 1.1f;
                float boomHz = 38f + 70f * Mathf.Exp(-t * 6f);
                float boom = Mathf.Sin(2f * Mathf.PI * boomHz * t) * Mathf.Exp(-t * 3.2f) * 1.3f;
                float body = low * Mathf.Exp(-t * 4.5f) * 2.4f;
                float tail = rumble * Mathf.Exp(-t * 1.6f) * 5f;
                float debris = Debris(t, 0.18f, 1.9f) * 0.35f;
                float attack = Mathf.Min(1f, t / 0.0015f);
                float edge = Mathf.Min(1f, (seconds - t) / 0.2f);
                return (crack + boom + body + tail + debris) * attack * edge;
            });
        }

        /// <summary>
        /// A lit fuse, as a seamless loop: a bright hiss that flickers with little crackling pops. Played on a loop
        /// while a grenade is stuck, its pitch rising as the fuse burns down.
        /// </summary>
        public static AudioClip FuseLoop()
        {
            const float seconds = 1.2f;
            float previous = 0f;
            float body = 0f;
            return Build("fuse", seconds, t =>
            {
                float noise = Noise(t);
                // Difference of neighbouring samples = a crude high-pass: the "tsss" of burning powder.
                float hiss = noise - previous;
                previous = noise;
                body += (noise - body) * 0.25f;
                float flicker = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 13f * t) * Mathf.Sin(2f * Mathf.PI * 7f * t + 1.3f);
                float pops = Debris(t, 0f, seconds) * 0.6f;
                return (hiss * 0.32f + body * 0.18f) * flicker + pops;
            });
        }

        /// <summary>The short warning tick of a grenade about to blow; it repeats faster as the fuse runs out.</summary>
        public static AudioClip FuseBeep()
        {
            return Build("fusebeep", 0.08f, t => (Mathf.Sin(2f * Mathf.PI * 1760f * t) + Mathf.Sin(2f * Mathf.PI * 2640f * t) * 0.4f)
                                                 * Env(t, 0.08f, 0.002f, 5f) * 0.5f);
        }

        /// <summary>A wet "splotch" with a thud: a slug grenade sticking to whatever it hit.</summary>
        public static AudioClip Stick()
        {
            float low = 0f;
            return Build("stick", 0.22f, t =>
            {
                low += (Noise(t) - low) * 0.18f;
                float thud = Sweep(170f, 55f, 0.22f, t) * Env(t, 0.22f, 0.002f, 6f) * 0.9f;
                float squish = low * Env(t, 0.22f, 0.004f, 4f) * 1.6f * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 38f * t));
                return thud + squish;
            });
        }

        /// <summary>
        /// A Phosphoro charging up, as a seamless loop: a glassy shimmer of high partials that wobble against each
        /// other over a soft electric hum. Its pitch is raised while the fuse burns down, so it rises to a whine.
        /// </summary>
        public static AudioClip FlashCharge()
        {
            const float seconds = 1f;
            float previous = 0f;
            return Build("flashcharge", seconds, t =>
            {
                // Whole numbers of cycles per second keep the loop seamless.
                float shimmer = Mathf.Sin(2f * Mathf.PI * 1320f * t) * 0.32f + Mathf.Sin(2f * Mathf.PI * 1987f * t) * 0.2f
                                + Mathf.Sin(2f * Mathf.PI * 2641f * t) * 0.12f;
                float wobble = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 9f * t);
                float hum = Mathf.Sin(2f * Mathf.PI * 220f * t) * 0.18f + Mathf.Sin(2f * Mathf.PI * 440f * t) * 0.08f;
                float noise = Noise(t);
                float sizzle = (noise - previous) * 0.06f;
                previous = noise;
                return shimmer * wobble + hum + sizzle;
            });
        }

        /// <summary>
        /// The flash itself: a sharp electric crack, a bright "pshhh" of light and a ringing tone that hangs on.
        /// </summary>
        public static AudioClip FlashBang()
        {
            const float seconds = 1.6f;
            float previous = 0f;
            float low = 0f;
            return Build("flashbang", seconds, t =>
            {
                float noise = Noise(t);
                float bright = noise - previous;
                previous = noise;
                low += (noise - low) * 0.06f;
                float crack = bright * Mathf.Exp(-t * 45f) * 1.6f;
                float burst = noise * Mathf.Exp(-t * 9f) * 0.6f;
                float thump = Mathf.Sin(2f * Mathf.PI * (90f - 40f * t) * t) * Mathf.Exp(-t * 7f) * 0.7f;
                float ring = (Mathf.Sin(2f * Mathf.PI * 2350f * t) + Mathf.Sin(2f * Mathf.PI * 3120f * t) * 0.5f)
                             * Mathf.Exp(-t * 2.2f) * 0.22f;
                float attack = Mathf.Min(1f, t / 0.001f);
                return (crack + burst + thump + ring + low * Mathf.Exp(-t * 3f) * 0.8f) * attack;
            });
        }

        /// <summary>The high whistle in your ears after being blinded by a flash; it slowly fades.</summary>
        public static AudioClip Tinnitus()
        {
            const float seconds = 3.5f;
            return Build("tinnitus", seconds, t =>
            {
                float tone = Mathf.Sin(2f * Mathf.PI * 3700f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 3711f * t) * 0.25f;
                float fadeIn = Mathf.Min(1f, t / 0.05f);
                return tone * fadeIn * Mathf.Exp(-t * 0.9f) * 0.8f;
            });
        }

        /// <summary>A big splash in water: a slap, a gush of spray and a few bubbly plops as it settles.</summary>
        public static AudioClip Splash()
        {
            const float seconds = 0.9f;
            float low = 0f;
            float previous = 0f;
            return Build("splash", seconds, t =>
            {
                float noise = Noise(t);
                low += (noise - low) * 0.12f;
                float hiss = noise - previous;
                previous = noise;
                float slap = low * Mathf.Exp(-t * 18f) * 2.2f;
                float spray = (hiss * 0.5f + noise * 0.25f) * Mathf.Exp(-t * 5f) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 31f * t));
                float plops = Bubbles(t, 0.12f, seconds) * 0.5f;
                return slap + spray + plops;
            });
        }

        /// <summary>A light splash of a foot in a puddle.</summary>
        public static AudioClip Splish()
        {
            float low = 0f;
            return Build("splish", 0.25f, t =>
            {
                float noise = Noise(t);
                low += (noise - low) * 0.2f;
                float wet = (noise * 0.4f + low) * Env(t, 0.25f, 0.003f, 6f);
                return wet * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 45f * t)) + Bubbles(t, 0.03f, 0.25f) * 0.3f;
            });
        }

        /// <summary>Something small dropping into water: a hollow "bloop" that rises in pitch.</summary>
        public static AudioClip Plop()
        {
            return Build("plop", 0.3f, t => Sweep(260f, 900f, 0.3f, t) * Env(t, 0.3f, 0.003f, 7f) * 0.8f
                                            + Noise(t) * Env(t, 0.3f, 0.001f, 25f) * 0.3f);
        }

        /// <summary>Ice growing out of the floor: a rising, glassy crackle that ends in a hard "tock".</summary>
        public static AudioClip IceForm()
        {
            const float seconds = 0.6f;
            float previous = 0f;
            return Build("iceform", seconds, t =>
            {
                float noise = Noise(t);
                float bright = noise - previous;
                previous = noise;
                float crackle = Debris(t, 0f, 0.45f) * 0.9f + bright * Mathf.Exp(-t * 6f) * 0.25f;
                float ring = Sweep(900f, 2400f, seconds, t) * Env(t, seconds, 0.02f, 3f) * 0.25f;
                float tock = Mathf.Sin(2f * Mathf.PI * 420f * t) * Mathf.Exp(-Mathf.Max(0f, t - 0.42f) * 40f)
                             * (t > 0.42f ? 0.6f : 0f);
                return crackle + ring + tock;
            });
        }

        /// <summary>A slug cracking an ice wall: a sharp snap with a glassy ring.</summary>
        public static AudioClip IceCrack()
        {
            const float seconds = 0.35f;
            float previous = 0f;
            return Build("icecrack", seconds, t =>
            {
                float noise = Noise(t);
                float snap = (noise - previous) * Mathf.Exp(-t * 60f) * 1.4f;
                previous = noise;
                float ring = (Mathf.Sin(2f * Mathf.PI * 2900f * t) + Mathf.Sin(2f * Mathf.PI * 4100f * t) * 0.5f)
                             * Mathf.Exp(-t * 14f) * 0.3f;
                return snap + ring + Debris(t, 0.02f, 0.3f) * 0.4f;
            });
        }

        /// <summary>An ice wall shattering: a crash and a rain of tinkling shards.</summary>
        public static AudioClip IceShatter()
        {
            const float seconds = 1.3f;
            float previous = 0f;
            return Build("iceshatter", seconds, t =>
            {
                float noise = Noise(t);
                float bright = noise - previous;
                previous = noise;
                float crash = bright * Mathf.Exp(-t * 9f) * 1.1f + noise * Mathf.Exp(-t * 18f) * 0.5f;
                float tinkle = 0f;
                for (int k = 0; k < 6; k++)
                {
                    float start = 0.05f + Hash(k * 3.7f) * 0.8f;
                    float age = t - start;
                    if (age > 0f)
                    {
                        float hz = 2200f + Hash(k * 1.3f + 0.5f) * 3000f;
                        tinkle += Mathf.Sin(2f * Mathf.PI * hz * age) * Mathf.Exp(-age * 18f) * 0.25f;
                    }
                }
                return crash + tinkle;
            });
        }

        /// <summary>A wall of fire burning, as a seamless loop: a low roar with crackles.</summary>
        public static AudioClip FireLoop()
        {
            const float seconds = 1.5f;
            float low = 0f;
            float body = 0f;
            return Build("fireloop", seconds, t =>
            {
                float noise = Noise(t);
                low += (noise - low) * 0.02f;
                body += (noise - body) * 0.12f;
                float flutter = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 6f * t) * Mathf.Sin(2f * Mathf.PI * 11f * t);
                return (low * 4f + body * 0.5f) * flutter + Debris(t, 0f, seconds) * 0.45f;
            });
        }

        /// <summary>Fire catching along the floor: a quick "fwoosh".</summary>
        public static AudioClip FireWhoosh()
        {
            const float seconds = 0.5f;
            float low = 0f;
            return Build("firewhoosh", seconds, t =>
            {
                float noise = Noise(t);
                low += (noise - low) * (0.05f + 0.3f * t);
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / seconds)) * Mathf.Exp(-t * 2f);
                return low * swell * 2.2f + noise * swell * 0.15f;
            });
        }

        /// <summary>A bright metallic "ding" for a headshot.</summary>
        public static AudioClip Critical()
        {
            return Build("critical", 0.45f, t =>
            {
                float bell = Mathf.Sin(2f * Mathf.PI * 1568f * t) + Mathf.Sin(2f * Mathf.PI * 2349f * t) * 0.6f
                             + Mathf.Sin(2f * Mathf.PI * 4186f * t) * 0.25f;
                float knock = Noise(t) * Mathf.Exp(-t * 90f) * 0.6f;
                return bell * Env(t, 0.45f, 0.001f, 3.5f) * 0.45f + knock;
            });
        }

        /// <summary>A rising, defiant chord with a whoosh: your team is outnumbered and you get courage.</summary>
        public static AudioClip Courage()
        {
            float[] notes = { 293.66f, 440f, 587.33f, 880f };
            const float seconds = 1.1f;
            float air = 0f;
            return Build("courage", seconds, t =>
            {
                air += (Noise(t) - air) * 0.05f;
                float swell = Mathf.Clamp01(t / 0.35f);
                float value = air * swell * Mathf.Exp(-t * 2f) * 1.2f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = t - n * 0.06f;
                    if (local >= 0f)
                    {
                        float tone = Mathf.Sin(2f * Mathf.PI * notes[n] * local)
                                     + Mathf.Sin(2f * Mathf.PI * notes[n] * 2.003f * local) * 0.3f;
                        value += tone * Env(local, seconds, 0.03f, 2.2f) * 0.3f;
                    }
                }
                return value;
            });
        }

        public static AudioClip Heal()
        {
            return Arpeggio("heal", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.07f, 0.12f);
        }

        public static AudioClip Shield()
        {
            return Arpeggio("shield", new[] { 392f, 587.33f, 880f }, 0.07f, 0.1f);
        }

        public static AudioClip Pickup()
        {
            return Arpeggio("pickup", new[] { 659.25f, 987.77f }, 0.06f, 0.08f);
        }

        public static AudioClip Defeat()
        {
            return Build("defeat", 0.8f, t => Sweep(420f, 60f, 0.8f, t) * Env(t, 0.8f, 0.005f, 3f) * 0.8f);
        }

        public static AudioClip Jump()
        {
            return Build("jump", 0.14f, t => Sweep(260f, 620f, 0.14f, t) * Env(t, 0.14f, 0.004f, 7f) * 0.5f);
        }

        public static AudioClip Roll()
        {
            return Build("roll", 0.28f, t => Noise(t) * 0.35f * Mathf.Sin(Mathf.PI * t / 0.28f));
        }

        public static AudioClip Beep(float hz, float seconds)
        {
            return Build("beep", seconds, t => Mathf.Sin(2f * Mathf.PI * hz * t) * Env(t, seconds, 0.005f, 3f) * 0.6f);
        }

        public static AudioClip Whistle()
        {
            return Arpeggio("whistle", new[] { 440f, 554.37f, 659.25f, 880f }, 0.1f, 0.16f);
        }

        /// <summary>A soft interface click.</summary>
        public static AudioClip Click()
        {
            return Build("click", 0.07f, t => Sweep(900f, 480f, 0.07f, t) * Env(t, 0.07f, 0.002f, 6f) * 0.55f);
        }

        /// <summary>A tiny tick when the pointer moves onto a button.</summary>
        public static AudioClip Hover()
        {
            return Build("hover", 0.05f, t => Mathf.Sin(2f * Mathf.PI * 1250f * t) * Env(t, 0.05f, 0.002f, 8f) * 0.25f);
        }

        /// <summary>The "you hit something" tick that goes with the crosshair marker.</summary>
        public static AudioClip HitMarker()
        {
            return Build("marker", 0.09f, t => (Mathf.Sin(2f * Mathf.PI * 1800f * t) + Mathf.Sin(2f * Mathf.PI * 2700f * t) * 0.5f)
                                               * Env(t, 0.09f, 0.001f, 9f) * 0.5f);
        }

        /// <summary>A low, swelling chord for a victory.</summary>
        public static AudioClip Victory()
        {
            return Arpeggio("victory", new[] { 392f, 523.25f, 659.25f, 783.99f, 1046.5f }, 0.11f, 0.5f);
        }

        /// <summary>A falling minor figure for a defeat.</summary>
        public static AudioClip Loss()
        {
            return Arpeggio("loss", new[] { 392f, 349.23f, 311.13f, 233.08f }, 0.16f, 0.6f);
        }

        /// <summary>
        /// A looping cavern drone: two slowly beating low tones, a faint shimmer and the odd far-off water drip, so
        /// the menu and the arena are never silent.
        /// </summary>
        public static AudioClip CaveAmbience()
        {
            const float seconds = 16f;
            return Build("cave", seconds, t =>
            {
                float drone = Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 55.6f * t) * 0.3f
                              + Mathf.Sin(2f * Mathf.PI * 82.4f * t + Mathf.Sin(t * 0.7f)) * 0.12f;
                float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * t / seconds * 2f);
                float shimmer = Mathf.Sin(2f * Mathf.PI * (880f + 40f * Mathf.Sin(t * 0.9f)) * t) * 0.025f
                                * (0.5f + 0.5f * Mathf.Sin(t * 0.35f));
                float dripClock = Mathf.Repeat(t + 2.3f, 5.1f);
                float drip = dripClock < 0.12f
                    ? Mathf.Sin(2f * Mathf.PI * (1800f - 4500f * dripClock) * dripClock) * Mathf.Exp(-dripClock * 30f) * 0.12f
                    : 0f;
                // Fade the ends so the loop has no click.
                float edge = Mathf.Min(1f, Mathf.Min(t, seconds - t) / 0.3f);
                return (drone * swell * 0.55f + shimmer + drip) * edge;
            });
        }

        // ------------------------------------------------------------------------------------------ building blocks

        private static AudioClip Build(string name, float seconds, Func<float, float> sample)
        {
            int count = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = Mathf.Clamp(sample(i / (float)SampleRate), -1f, 1f) * 0.6f;
            }
            AudioClip clip = AudioClip.Create("bajoterra-" + name, count, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Arpeggio(string name, float[] notes, float noteSeconds, float tailSeconds)
        {
            float total = notes.Length * noteSeconds + tailSeconds;
            return Build(name, total, t =>
            {
                float value = 0f;
                for (int n = 0; n < notes.Length; n++)
                {
                    float local = t - n * noteSeconds;
                    if (local >= 0f)
                    {
                        value += Mathf.Sin(2f * Mathf.PI * notes[n] * local) * Env(local, total, 0.004f, 7f) * 0.5f;
                    }
                }
                return value;
            });
        }

        /// <summary>Linear sweep of the frequency from <paramref name="from"/> to <paramref name="to"/> over the duration.</summary>
        private static float Sweep(float from, float to, float seconds, float t)
        {
            float phase = 2f * Mathf.PI * (from * t + (to - from) * t * t / (2f * seconds));
            return Mathf.Sin(phase);
        }

        /// <summary>Attack/decay envelope: a short linear attack followed by exponential decay.</summary>
        private static float Env(float t, float seconds, float attack, float decay)
        {
            if (t < 0f || t > seconds)
            {
                return 0f;
            }
            float rise = Mathf.Min(1f, t / attack);
            return rise * Mathf.Exp(-decay * t / Mathf.Max(0.01f, seconds) * 3f);
        }

        /// <summary>
        /// Sparse crackles between <paramref name="from"/> and <paramref name="to"/> seconds: short decaying clicks at
        /// pseudo-random moments (pebbles, sparks), thinning out towards the end.
        /// </summary>
        private static float Debris(float t, float from, float to)
        {
            if (t < from || t > to)
            {
                return 0f;
            }
            const float slot = 0.023f;
            float index = Mathf.Floor(t / slot);
            float local = t - index * slot;
            float chance = Hash(index);
            float density = 1f - (t - from) / Mathf.Max(0.01f, to - from) * 0.7f;
            if (chance > 0.45f * density)
            {
                return 0f;
            }
            float delay = Hash(index + 17.3f) * slot * 0.6f;
            if (local < delay)
            {
                return 0f;
            }
            float age = local - delay;
            return Noise(t) * Mathf.Exp(-age * 600f) * (0.4f + Hash(index + 3.1f));
        }

        /// <summary>Little bubbles popping between <paramref name="from"/> and <paramref name="to"/> seconds: short rising chirps.</summary>
        private static float Bubbles(float t, float from, float to)
        {
            if (t < from || t > to)
            {
                return 0f;
            }
            const float slot = 0.045f;
            float index = Mathf.Floor(t / slot);
            if (Hash(index + 5.7f) > 0.55f)
            {
                return 0f;
            }
            float age = t - index * slot;
            float hz = 500f + 900f * Hash(index + 1.9f);
            return Mathf.Sin(2f * Mathf.PI * hz * age * (1f + age * 18f)) * Mathf.Exp(-age * 70f);
        }

        /// <summary>A repeatable pseudo-random number in [0, 1) for a given input.</summary>
        private static float Hash(float x)
        {
            float h = Mathf.Sin(x * 78.233f + 12.9898f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        /// <summary>Deterministic white noise from the sample time (no random state to seed).</summary>
        private static float Noise(float t)
        {
            float x = Mathf.Sin(t * 12.9898f * SampleRate) * 43758.5453f;
            return (x - Mathf.Floor(x)) * 2f - 1f;
        }
    }
}
