using System;
using System.Collections.Generic;

namespace ColourCascade.Procedural
{
    public sealed class SoundData
    {
        public string Name = "";
        public int SampleRate;
        public float[] Samples;
        public bool Loop;
    }

    /// <summary>Names of the generated sound effects.</summary>
    public static class SfxNames
    {
        public const string Tap = "tap";
        public const string Select = "select";
        public const string Invalid = "invalid";
        public const string Coin = "coin";
        public const string Win = "win";
        public const string Lose = "lose";
        public const string Whoosh = "whoosh";
        public const string Blast = "blast";
        public const string Purchase = "purchase";
        public const string Tick = "tick";
        public const string Go = "go";
        public const string Unlock = "unlock";
        public const string Toggle = "toggle";
        public const string Swap = "swap";
        public const string Break = "break";
        public const string Shimmer = "shimmer";
        public const string Music = "music";
        public const int PopVariants = 8;
        public static string Pop(int index) => "pop" + Math.Max(0, Math.Min(PopVariants - 1, index));
        public static string Star(int index) => "star" + Math.Max(1, Math.Min(3, index));
    }

    /// <summary>
    /// Synthesises every sound in the game from simple oscillators (marimba-like plucks, bells, filtered
    /// noise), so the project needs no audio assets. Pure maths, safe to run on a background thread.
    /// </summary>
    public static class SoundSynth
    {
        public const int Rate = 44100;
        const double TwoPi = Math.PI * 2;

        // Pentatonic C major, two octaves: pops climb this scale as groups get bigger.
        static readonly double[] PopNotes = { 523.25, 587.33, 659.25, 783.99, 880.00, 1046.5, 1174.66, 1318.51 };

        public static List<SoundData> BuildEffects()
        {
            var list = new List<SoundData>();
            list.Add(Make(SfxNames.Tap, 0.05, b => { Tone(b, 0, 0.04, 1250, 0.32, Env.Click); Noise(b, 0, 0.012, 0.08, 0.2); }));
            list.Add(Make(SfxNames.Toggle, 0.06, b => { Tone(b, 0, 0.05, 900, 0.3, Env.Click); Tone(b, 0.012, 0.04, 1350, 0.18, Env.Click); }));
            list.Add(Make(SfxNames.Select, 0.12, b => Glide(b, 0, 0.1, 700, 980, 0.28)));
            for (int i = 0; i < SfxNames.PopVariants; i++)
            {
                double f = PopNotes[i];
                list.Add(Make(SfxNames.Pop(i), 0.32, b =>
                {
                    Glide(b, 0, 0.03, f * 1.6, f, 0.22);
                    Marimba(b, 0.008, 0.3, f, 0.42);
                    Noise(b, 0, 0.02, 0.05, 0.5);
                }));
            }
            list.Add(Make(SfxNames.Invalid, 0.22, b => { Tone(b, 0, 0.1, 180, 0.35, Env.Thud); Tone(b, 0.08, 0.12, 140, 0.32, Env.Thud); }));
            list.Add(Make(SfxNames.Coin, 0.5, b => { Bell(b, 0, 0.35, 987.77, 0.3); Bell(b, 0.07, 0.4, 1318.51, 0.32); }));
            double[] starNotes = { 659.25, 783.99, 1046.5 };
            for (int s = 1; s <= 3; s++)
            {
                double f = starNotes[s - 1];
                list.Add(Make(SfxNames.Star(s), 0.7, b =>
                {
                    Bell(b, 0, 0.6, f, 0.34);
                    Bell(b, 0.05, 0.5, f * 1.5, 0.16);
                    Bell(b, 0.1, 0.45, f * 2, 0.1);
                }));
            }
            list.Add(Make(SfxNames.Win, 1.5, b =>
            {
                double[] arp = { 523.25, 659.25, 783.99, 1046.5 };
                for (int i = 0; i < arp.Length; i++) Marimba(b, i * 0.1, 0.4, arp[i], 0.36);
                Bell(b, 0.42, 1.0, 1046.5, 0.22);
                Bell(b, 0.42, 1.0, 1318.51, 0.16);
                Bell(b, 0.42, 1.0, 1567.98, 0.14);
            }));
            list.Add(Make(SfxNames.Lose, 1.0, b =>
            {
                double[] notes = { 392.0, 329.63, 261.63 };
                for (int i = 0; i < notes.Length; i++) Soft(b, i * 0.2, 0.45, notes[i], 0.3);
            }));
            list.Add(Make(SfxNames.Whoosh, 0.42, b => Sweep(b, 0, 0.4, 0.35)));
            list.Add(Make(SfxNames.Blast, 0.7, b =>
            {
                Glide(b, 0, 0.55, 110, 38, 0.6, Env.Boom);
                Noise(b, 0, 0.35, 0.4, 0.12);
                Noise(b, 0, 0.08, 0.25, 0.6);
            }));
            list.Add(Make(SfxNames.Purchase, 0.6, b =>
            {
                Tone(b, 0, 0.03, 1600, 0.25, Env.Click);
                Bell(b, 0.04, 0.4, 1174.66, 0.28);
                Bell(b, 0.12, 0.45, 1567.98, 0.28);
            }));
            list.Add(Make(SfxNames.Tick, 0.08, b => Marimba(b, 0, 0.07, 1000, 0.35)));
            list.Add(Make(SfxNames.Go, 0.4, b => { Marimba(b, 0, 0.3, 1046.5, 0.35); Marimba(b, 0, 0.3, 1318.51, 0.25); Marimba(b, 0, 0.3, 1567.98, 0.2); }));
            list.Add(Make(SfxNames.Unlock, 0.9, b =>
            {
                double[] notes = { 1046.5, 1318.51, 1567.98, 2093.0 };
                for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.07, 0.5, notes[i], 0.22);
                Sweep(b, 0, 0.35, 0.08);
            }));
            list.Add(Make(SfxNames.Swap, 0.25, b => { Glide(b, 0, 0.1, 500, 800, 0.25); Glide(b, 0.09, 0.1, 800, 520, 0.22); }));
            list.Add(Make(SfxNames.Break, 0.35, b => { Noise(b, 0, 0.18, 0.45, 0.35); Tone(b, 0, 0.12, 220, 0.3, Env.Thud); }));
            list.Add(Make(SfxNames.Shimmer, 0.8, b =>
            {
                double[] notes = { 1567.98, 1318.51, 2093.0, 1760.0, 2349.32 };
                for (int i = 0; i < notes.Length; i++) Bell(b, i * 0.06, 0.4, notes[i], 0.14);
            }));
            foreach (var s in list) Normalise(s.Samples, 0.9f);
            return list;
        }

        enum Env { Pluck, Click, Thud, Boom, Pad }

        static SoundData Make(string name, double seconds, Action<float[]> draw)
        {
            var buffer = new float[(int)(seconds * Rate)];
            draw(buffer);
            return new SoundData { Name = name, SampleRate = Rate, Samples = buffer };
        }

        static double Envelope(Env env, double t, double dur)
        {
            switch (env)
            {
                case Env.Click: return Math.Exp(-t * 90);
                case Env.Thud: return Math.Min(1, t * 400) * Math.Exp(-t * 30);
                case Env.Boom: return Math.Min(1, t * 300) * Math.Exp(-t * 7);
                case Env.Pad: return Math.Min(1, t / 0.08) * Math.Max(0, 1 - t / dur);
                default: return Math.Min(1, t * 500) * Math.Exp(-t * 12);
            }
        }

        static void Tone(float[] b, double start, double dur, double freq, double amp, Env env = Env.Pluck)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / Rate;
                b[s0 + i] += (float)(Math.Sin(TwoPi * freq * t) * amp * Envelope(env, t, dur));
            }
        }

        static void Glide(float[] b, double start, double dur, double f0, double f1, double amp, Env env = Env.Pluck)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / Rate;
                double f = f0 + (f1 - f0) * (t / dur);
                phase += TwoPi * f / Rate;
                b[s0 + i] += (float)(Math.Sin(phase) * amp * Envelope(env, t, dur));
            }
        }

        static void Marimba(float[] b, double start, double dur, double f, double amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / Rate;
                double a = Math.Min(1, t * 600);
                double v = Math.Sin(TwoPi * f * t) * Math.Exp(-t * 9) + 0.3 * Math.Sin(TwoPi * f * 3.93 * t) * Math.Exp(-t * 40) + 0.12 * Math.Sin(TwoPi * f * 2 * t) * Math.Exp(-t * 20);
                b[s0 + i] += (float)(v * a * amp);
            }
        }

        static void Bell(float[] b, double start, double dur, double f, double amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / Rate;
                double a = Math.Min(1, t * 800) * Math.Max(0, 1 - t / dur);
                double v = Math.Sin(TwoPi * f * t) * Math.Exp(-t * 5) + 0.45 * Math.Sin(TwoPi * f * 2.76 * t) * Math.Exp(-t * 11) + 0.2 * Math.Sin(TwoPi * f * 5.4 * t) * Math.Exp(-t * 20);
                b[s0 + i] += (float)(v * a * amp);
            }
        }

        static void Soft(float[] b, double start, double dur, double f, double amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                double t = (double)i / Rate;
                double vib = 1 + 0.004 * Math.Sin(TwoPi * 5.5 * t);
                double a = Math.Min(1, t / 0.03) * Math.Exp(-t * 3.5);
                double v = Math.Sin(TwoPi * f * vib * t) + 0.25 * Math.Sin(TwoPi * f * 2 * vib * t);
                b[s0 + i] += (float)(v * a * amp);
            }
        }

        static void Noise(float[] b, double start, double dur, double amp, double brightness)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            uint seed = 2463534242u;
            double lp = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                double white = (seed / (double)uint.MaxValue) * 2 - 1;
                lp += (white - lp) * brightness;
                double t = (double)i / Rate;
                b[s0 + i] += (float)(lp * amp * Math.Min(1, t * 1000) * Math.Max(0, 1 - t / dur));
            }
        }

        static void Sweep(float[] b, double start, double dur, double amp)
        {
            int s0 = (int)(start * Rate), n = (int)(dur * Rate);
            uint seed = 88172645u;
            double lp = 0;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
                double white = (seed / (double)uint.MaxValue) * 2 - 1;
                double x = (double)i / n;
                double cutoff = 0.02 + 0.3 * Math.Sin(Math.PI * x);
                lp += (white - lp) * cutoff;
                b[s0 + i] += (float)(lp * amp * Math.Sin(Math.PI * x));
            }
        }

        static void Normalise(float[] s, float peak)
        {
            float max = 0;
            for (int i = 0; i < s.Length; i++) max = Math.Max(max, Math.Abs(s[i]));
            if (max < 1e-5f) return;
            float k = max > peak ? peak / max : 1f;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        /// <summary>
        /// A calm, seamless 20-second loop (C – Am – F – G at 96 BPM): soft pad, plucked arpeggio and bass.
        /// </summary>
        public static SoundData BuildMusic(int sampleRate = 22050)
        {
            const double bpm = 96;
            double beat = 60.0 / bpm;
            int bars = 8;
            double length = bars * 4 * beat;
            int total = (int)(length * sampleRate);
            var buf = new float[total];
            double[][] chords =
            {
                new[] { 261.63, 329.63, 392.00 }, // C
                new[] { 220.00, 261.63, 329.63 }, // Am
                new[] { 174.61, 220.00, 261.63 }, // F
                new[] { 196.00, 246.94, 293.66 }, // G
            };
            int[] arpPattern = { 0, 1, 2, 3, 2, 1, 2, 3 };
            for (int bar = 0; bar < bars; bar++)
            {
                var chord = chords[bar % 4];
                double barStart = bar * 4 * beat;
                foreach (var f in chord) PadNote(buf, sampleRate, total, barStart, 4 * beat, f, 0.05);
                PluckNote(buf, sampleRate, total, barStart, 1.6, chord[0] / 2, 0.16, 4.0);
                PluckNote(buf, sampleRate, total, barStart + 2 * beat, 1.2, chord[0] / 2, 0.12, 4.5);
                for (int step = 0; step < 8; step++)
                {
                    int idx = arpPattern[step];
                    double f = idx < 3 ? chord[idx] * 2 : chord[0] * 4;
                    double amp = (step % 2 == 0 ? 0.075 : 0.055) * (bar >= 4 ? 1.0 : 0.8);
                    PluckNote(buf, sampleRate, total, barStart + step * beat / 2, 0.6, f, amp, 7.5);
                }
            }
            Normalise(buf, 0.75f);
            return new SoundData { Name = SfxNames.Music, SampleRate = sampleRate, Samples = buf, Loop = true };
        }

        // Notes wrap around the end of the buffer so the loop is seamless.
        static void PadNote(float[] b, int rate, int total, double start, double dur, double f, double amp)
        {
            int s0 = (int)(start * rate);
            int n = (int)((dur + 0.6) * rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double env = Math.Min(1, t / 0.5) * (t < dur ? 1 : Math.Max(0, 1 - (t - dur) / 0.6));
                double v = Math.Sin(TwoPi * f * 1.0017 * t) + Math.Sin(TwoPi * f * 0.9983 * t) + 0.3 * Math.Sin(TwoPi * f * 2 * t);
                b[(s0 + i) % total] += (float)(v * env * amp * 0.5);
            }
        }

        static void PluckNote(float[] b, int rate, int total, double start, double dur, double f, double amp, double decay)
        {
            int s0 = (int)(start * rate);
            int n = (int)(dur * rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / rate;
                double env = Math.Min(1, t * 300) * Math.Exp(-t * decay);
                double v = Math.Sin(TwoPi * f * t) + 0.35 * Math.Sin(TwoPi * f * 2 * t) * Math.Exp(-t * 6);
                b[(s0 + i) % total] += (float)(v * env * amp);
            }
        }
    }
}
