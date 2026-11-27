using System.Collections.Generic;
using UnityEngine;

// A tiny speech synthesizer (formant synthesis). It builds words out of sounds
// like "S", "T", "AA", "P" and makes the guard shout, the dog bark and the robot talk.
// Voiced sounds = buzzing "voice" shaped by 3 formant filters. Others = filtered noise.
public static class VoiceSynth
{
    const int Rate = 44100;
    static readonly float[] BaseBw = { 70f, 110f, 170f };
    static readonly float[] Weight = { 1f, 0.7f, 0.4f };

    class Ph
    {
        public int kind;      // 0 vowel, 1 noise (s, sh, h), 2 plosive (t, k, p), 3 voiced consonant (n, m, l, r), 4 silence
        public float dur;
        public float[] f;     // formants at the start
        public float[] e;     // formants at the end (different = gliding vowel)
        public float amp;
        public float bw;      // bandwidth scale (noise sounds need wide filters)
    }

    static Dictionary<string, Ph> table;

    static float[] F(float a, float b, float c) { return new float[] { a, b, c }; }

    static void Add(string name, int kind, float dur, float[] f, float[] e, float amp, float bw = 1f)
    {
        Ph p = new Ph();
        p.kind = kind; p.dur = dur; p.f = f; p.e = e; p.amp = amp; p.bw = bw;
        table[name] = p;
    }

    static void Init()
    {
        if (table != null) return;
        table = new Dictionary<string, Ph>();

        // vowels
        Add("AA", 0, 0.16f, F(730, 1090, 2440), F(730, 1090, 2440), 1f);
        Add("AE", 0, 0.16f, F(660, 1720, 2410), F(660, 1720, 2410), 1f);
        Add("EH", 0, 0.13f, F(530, 1840, 2480), F(530, 1840, 2480), 1f);
        Add("IH", 0, 0.10f, F(390, 1990, 2550), F(390, 1990, 2550), 1f);
        Add("IY", 0, 0.14f, F(270, 2290, 3010), F(270, 2290, 3010), 1f);
        Add("AH", 0, 0.12f, F(640, 1190, 2390), F(640, 1190, 2390), 1f);
        Add("UW", 0, 0.14f, F(300, 870, 2240), F(300, 870, 2240), 1f);
        Add("ER", 0, 0.16f, F(490, 1350, 1690), F(490, 1350, 1690), 1f);
        Add("OW", 0, 0.18f, F(570, 840, 2410), F(300, 870, 2240), 1f);
        Add("EY", 0, 0.20f, F(530, 1840, 2480), F(270, 2290, 3010), 1f);
        Add("AY", 0, 0.20f, F(730, 1090, 2440), F(270, 2290, 3010), 1f);

        // animal sounds
        Add("BARK", 0, 0.16f, F(750, 1250, 2500), F(600, 1000, 2400), 1f);
        Add("GROWL", 0, 0.90f, F(450, 850, 2000), F(450, 850, 2000), 1f);
        Add("SN", 1, 0.08f, F(2500, 3500, 5000), F(2500, 3500, 5000), 0.5f, 8f);

        // voiced consonants
        Add("N", 3, 0.07f, F(250, 1700, 2600), F(250, 1700, 2600), 0.6f);
        Add("M", 3, 0.08f, F(250, 1100, 2200), F(250, 1100, 2200), 0.6f);
        Add("MM", 3, 0.35f, F(250, 1100, 2200), F(250, 1100, 2200), 0.5f);
        Add("NG", 3, 0.09f, F(250, 1500, 2500), F(250, 1500, 2500), 0.6f);
        Add("L", 3, 0.07f, F(360, 1300, 2500), F(360, 1300, 2500), 0.8f);
        Add("R", 3, 0.08f, F(310, 1060, 1380), F(310, 1060, 1380), 0.8f);

        // noisy consonants
        Add("S", 1, 0.11f, F(5500, 7000, 8500), F(5500, 7000, 8500), 0.27f, 10f);
        Add("SH", 1, 0.11f, F(2800, 4000, 5500), F(2800, 4000, 5500), 0.45f, 8f);
        Add("H", 1, 0.06f, F(1400, 2000, 3000), F(1400, 2000, 3000), 0.63f, 10f);

        // plosives (silence, then a short burst)
        Add("T", 2, 0.05f, F(4000, 5500, 7000), F(4000, 5500, 7000), 1.05f, 10f);
        Add("D", 2, 0.04f, F(3500, 5000, 6500), F(3500, 5000, 6500), 0.65f, 10f);
        Add("K", 2, 0.06f, F(1800, 2400, 3200), F(1800, 2400, 3200), 2.1f, 10f);
        Add("G", 2, 0.05f, F(1500, 2200, 3000), F(1500, 2200, 3000), 1.3f, 10f);
        Add("P", 2, 0.05f, F(700, 1200, 2000), F(700, 1200, 2000), 4.2f, 10f);
        Add("B", 2, 0.04f, F(600, 1100, 1900), F(600, 1100, 1900), 2.5f, 10f);

        Add("_", 4, 0.08f, F(0, 0, 0), F(0, 0, 0), 0f);   // short pause
    }

    // ------------------------------------------------------------
    // Say one word. f0Start/f0End = voice pitch in Hz (changes over the word).
    // ------------------------------------------------------------
    public static float[] Word(string[] phonemes, float f0Start, float f0End, float speed = 1f, float breath = 0.05f, float vibrato = 0.02f)
    {
        Init();

        float total = 0f;
        foreach (string name in phonemes) total += table[name].dur / speed;

        List<float> output = new List<float>();
        float[] y1 = new float[3], y2 = new float[3], a = new float[3], b1 = new float[3], b2 = new float[3];
        float phase = 0f, elapsed = 0f;
        int counter = 0;

        foreach (string name in phonemes)
        {
            Ph p = table[name];
            float segDur = p.dur / speed;
            int n = Mathf.Max(1, Mathf.RoundToInt(segDur * Rate));

            for (int i = 0; i < n; i++, counter++)
            {
                if (p.kind == 4) { output.Add(0f); continue; }
                float u = i / (float)n;

                // update the filter settings every 8 samples
                if (counter % 8 == 0)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float freq = Mathf.Lerp(p.f[k], p.e[k], u);
                        float r = Mathf.Exp(-Mathf.PI * BaseBw[k] * p.bw / Rate);
                        b1[k] = 2f * r * Mathf.Cos(2f * Mathf.PI * freq / Rate);
                        b2[k] = -r * r;
                        a[k] = 1f - b1[k] - b2[k];
                    }
                }

                float noise = Random.value * 2f - 1f;
                float x, env;
                float edge = Mathf.Min(1f, Mathf.Min(i, n - 1 - i) / (0.006f * Rate));

                if (p.kind == 0 || p.kind == 3)
                {
                    float progress = (elapsed + u * segDur) / total;
                    float sec = output.Count / (float)Rate;
                    float f0 = Mathf.Lerp(f0Start, f0End, progress) * (1f + vibrato * Mathf.Sin(2f * Mathf.PI * 5.5f * sec));
                    phase += f0 / Rate;
                    float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                    x = saw * (1f - breath) + noise * breath;
                    env = edge;
                }
                else if (p.kind == 1)
                {
                    x = noise;
                    env = edge;
                }
                else
                {
                    if (u < 0.45f) { x = 0f; env = 1f; }                                  // closed mouth
                    else { x = noise; env = Mathf.Exp(-(u - 0.45f) / 0.55f * 5f); }       // burst
                }

                float sum = 0f;
                for (int k = 0; k < 3; k++)
                {
                    float y = a[k] * x + b1[k] * y1[k] + b2[k] * y2[k];
                    y2[k] = y1[k];
                    y1[k] = y;
                    sum += Weight[k] * y;
                }
                output.Add(sum * env * p.amp);
            }
            elapsed += segDur;
        }
        return Normalize(output.ToArray(), 0.9f);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------
    public static float[] Normalize(float[] d, float peak)
    {
        float max = 0.0001f;
        foreach (float v in d) { float av = Mathf.Abs(v); if (av > max) max = av; }
        float g = peak / max;
        float[] r = new float[d.Length];
        for (int i = 0; i < d.Length; i++) r[i] = d[i] * g;
        return r;
    }

    public static float[] Silence(float seconds)
    {
        return new float[Mathf.RoundToInt(seconds * Rate)];
    }

    public static float[] Join(params float[][] parts)
    {
        int total = 0;
        foreach (float[] p in parts) total += p.Length;
        float[] r = new float[total];
        int pos = 0;
        foreach (float[] p in parts) { System.Array.Copy(p, 0, r, pos, p.Length); pos += p.Length; }
        return r;
    }

    // Robot voice: grainy (sample hold), bit-crushed, ring-modulated, with a metallic echo
    public static float[] Robotize(float[] d)
    {
        float[] r = new float[d.Length];
        float held = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            if (i % 3 == 0) held = d[i];
            float v = Mathf.Round(held * 12f) / 12f;
            float t = i / (float)Rate;
            v *= 0.65f + 0.35f * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 55f * t));
            r[i] = v;
        }
        int delay = Mathf.RoundToInt(0.018f * Rate);
        for (int i = r.Length - 1; i >= delay; i--) r[i] += 0.5f * r[i - delay];
        return Normalize(r, 0.9f);
    }

    // Growl: voice wobbling in loudness
    public static float[] Growl(float[] d)
    {
        float[] r = new float[d.Length];
        for (int i = 0; i < d.Length; i++)
        {
            float t = i / (float)Rate;
            r[i] = d[i] * (0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 28f * t));
        }
        return Normalize(r, 0.9f);
    }
}
