using System;
using UnityEngine;

namespace PiratesDice.Game.Audio
{
    public enum Sfx { Rattle, Thud, Bid, Liar, SpotOn, Splash, Coin, Win, Lose, Click }

    /// <summary>
    /// Every sound in the game, synthesised at startup so there are no audio files to license or download.
    /// Swap any entry for a recorded clip later without touching the callers.
    /// </summary>
    public static class SoundBank
    {
        public const int Rate = 22050;

        static readonly System.Random Noise = new System.Random(7);

        public static AudioClip Build(Sfx sfx)
        {
            switch (sfx)
            {
                case Sfx.Rattle: return Rattle();
                case Sfx.Thud: return Thud();
                case Sfx.Bid: return Tones("bid", new[] { (784f, 0.07f), (1046f, 0.12f) }, Wave.Triangle, 0.35f);
                case Sfx.Liar: return Tones("liar", new[] { (392f, 0.12f), (370f, 0.12f), (311f, 0.12f), (233f, 0.4f) }, Wave.Square, 0.22f);
                case Sfx.SpotOn: return Tones("spot", new[] { (523f, 0.09f), (659f, 0.09f), (784f, 0.09f), (1046f, 0.3f) }, Wave.Triangle, 0.35f);
                case Sfx.Splash: return Splash();
                case Sfx.Coin: return Tones("coin", new[] { (988f, 0.06f), (1319f, 0.25f) }, Wave.Square, 0.18f);
                case Sfx.Win: return Tones("win", new[] { (523f, 0.12f), (659f, 0.12f), (784f, 0.12f), (1046f, 0.2f), (784f, 0.1f), (1046f, 0.5f) }, Wave.Triangle, 0.35f);
                case Sfx.Lose: return Tones("lose", new[] { (392f, 0.25f), (349f, 0.25f), (330f, 0.25f), (262f, 0.7f) }, Wave.Triangle, 0.35f);
                default: return Tones("click", new[] { (1200f, 0.03f) }, Wave.Triangle, 0.25f);
            }
        }

        enum Wave { Triangle, Square }

        static AudioClip Tones(string name, (float hz, float seconds)[] notes, Wave wave, float volume)
        {
            int total = 0;
            foreach (var n in notes) total += (int)(n.seconds * Rate);
            var data = new float[total];
            int at = 0;
            foreach (var n in notes)
            {
                int len = (int)(n.seconds * Rate);
                for (int i = 0; i < len; i++)
                {
                    float t = i / (float)Rate;
                    float phase = (t * n.hz) % 1f;
                    float s = wave == Wave.Square ? (phase < 0.5f ? 1f : -1f) : 4f * Mathf.Abs(phase - 0.5f) - 1f;
                    float env = Mathf.Min(1f, i / (Rate * 0.005f)) * Mathf.Exp(-3f * i / (float)len);
                    data[at + i] = s * env * volume;
                }
                at += len;
            }
            return Make(name, data);
        }

        static AudioClip Rattle()
        {
            var data = new float[(int)(0.55f * Rate)];
            for (int hit = 0; hit < 14; hit++)
            {
                int start = (int)(Rate * (0.02f + 0.48f * hit / 14f + (float)Noise.NextDouble() * 0.02f));
                float pitch = 2200f + (float)Noise.NextDouble() * 1800f;
                for (int i = 0; i < 260 && start + i < data.Length; i++)
                {
                    float env = Mathf.Exp(-i / 40f);
                    float click = Mathf.Sin(2 * Mathf.PI * pitch * i / Rate) * 0.5f + ((float)Noise.NextDouble() * 2 - 1) * 0.5f;
                    data[start + i] += click * env * 0.35f;
                }
            }
            return Make("rattle", data);
        }

        static AudioClip Thud()
        {
            var data = new float[(int)(0.25f * Rate)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float hz = 140f - 60f * t / 0.25f;
                data[i] = Mathf.Sin(2 * Mathf.PI * hz * t) * Mathf.Exp(-t * 18f) * 0.8f
                          + ((float)Noise.NextDouble() * 2 - 1) * Mathf.Exp(-t * 60f) * 0.25f;
            }
            return Make("thud", data);
        }

        static AudioClip Splash()
        {
            var data = new float[(int)(0.9f * Rate)];
            float low = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float white = (float)Noise.NextDouble() * 2 - 1;
                float cutoff = Mathf.Lerp(0.6f, 0.05f, t / 0.9f);
                low += (white - low) * cutoff;
                float env = Mathf.Min(1f, t / 0.02f) * Mathf.Exp(-t * 4.5f);
                data[i] = low * env * 0.7f;
            }
            return Make("splash", data);
        }

        /// <summary>
        /// "Drunken Sailor" (traditional, public domain) as a looping accordion-ish tune with a bass line.
        /// </summary>
        public static AudioClip Shanty()
        {
            const float bpm = 132f;
            float beat = 60f / bpm;

            // (note, beats). Notes are semitones from A4; 99 is a rest.
            const int D4 = -7, E4 = -5, F4 = -4, G4 = -2, A4 = 0, B4 = 2, C5 = 3, D5 = 5, C4 = -9;
            var verse = new (int note, float beats)[]
            {
                (A4, 1), (A4, .5f), (A4, .5f), (A4, 1), (A4, .5f), (A4, .5f), (A4, 1), (D4, 1), (F4, 1), (A4, 1),
                (G4, 1), (G4, .5f), (G4, .5f), (G4, 1), (G4, .5f), (G4, .5f), (G4, 1), (C4, 1), (E4, 1), (G4, 1),
                (A4, 1), (A4, .5f), (A4, .5f), (A4, 1), (A4, .5f), (A4, .5f), (A4, 1), (B4, 1), (C5, 1), (D5, 1),
                (C5, 1), (A4, 1), (G4, 1), (E4, 1), (D4, 2), (D4, 2),
            };
            var chorus = new (int note, float beats)[]
            {
                (A4, 2), (A4, 1), (A4, 1), (A4, 1), (D4, 1), (F4, 1), (A4, 1),
                (G4, 2), (G4, 1), (G4, 1), (G4, 1), (C4, 1), (E4, 1), (G4, 1),
                (A4, 2), (A4, 1), (A4, 1), (A4, 1), (B4, 1), (C5, 1), (D5, 1),
                (C5, 1), (A4, 1), (G4, 1), (E4, 1), (D4, 2), (D4, 2),
            };
            // Bass roots per bar (4 beats each), 8 bars per section: Dm Dm C C Dm Dm C(Am) Dm.
            int[] bass = { D4 - 12, D4 - 12, C4 - 12, C4 - 12, D4 - 12, D4 - 12, C4 - 12, D4 - 12 };

            int bars = 16;
            int length = (int)(bars * 4 * beat * Rate);
            var data = new float[length];

            float at = 0;
            foreach (var section in new[] { verse, chorus })
                foreach (var (note, beats) in section)
                {
                    Voice(data, at * beat, beats * beat * 0.92f, Hz(note), 0.16f, accordion: true);
                    at += beats;
                }

            for (int bar = 0; bar < bars; bar++)
            {
                int root = bass[bar % bass.Length];
                Voice(data, (bar * 4) * beat, beat * 1.6f, Hz(root), 0.14f, accordion: false);
                Voice(data, (bar * 4 + 2) * beat, beat * 1.6f, Hz(root + 7), 0.11f, accordion: false);
            }

            return Make("shanty", data);
        }

        static float Hz(int semitonesFromA4) => 440f * Mathf.Pow(2f, semitonesFromA4 / 12f);

        static void Voice(float[] data, float start, float seconds, float hz, float volume, bool accordion)
        {
            int from = (int)(start * Rate);
            int len = (int)(seconds * Rate);
            for (int i = 0; i < len && from + i < data.Length; i++)
            {
                float t = i / (float)Rate;
                float vib = accordion ? 1f + 0.004f * Mathf.Sin(2 * Mathf.PI * 5.5f * t) : 1f;
                float phase = (t * hz * vib) % 1f;
                float s = accordion
                    ? 0.6f * (phase < 0.5f ? 1f : -1f) * 0.5f + 0.5f * (4f * Mathf.Abs(phase - 0.5f) - 1f)
                    : Mathf.Sin(2 * Mathf.PI * phase);
                float attack = Mathf.Min(1f, t / 0.015f);
                float release = Mathf.Min(1f, (seconds - t) / 0.04f);
                float decay = accordion ? 1f : Mathf.Exp(-t * 2.5f);
                data[from + i] += s * attack * release * decay * volume;
            }
        }

        static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
