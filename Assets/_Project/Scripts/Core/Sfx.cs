using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Core
{
    /// <summary>Procedurally generated soft SFX/BGM so the project needs no external audio files.</summary>
    public class Sfx : MonoBehaviour
    {
        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        AudioSource[] pool;
        int next;
        AudioSource rub, bgm, puff;
        public float Master = 0.8f;
        System.Random rng = new System.Random(7);

        void Awake()
        {
            pool = new AudioSource[8];
            for (int i = 0; i < pool.Length; i++) { pool[i] = gameObject.AddComponent<AudioSource>(); pool[i].playOnAwake = false; }
            rub = gameObject.AddComponent<AudioSource>(); rub.loop = true; rub.playOnAwake = false; rub.volume = 0;
            puff = gameObject.AddComponent<AudioSource>(); puff.loop = true; puff.playOnAwake = false; puff.volume = 0;
            bgm = gameObject.AddComponent<AudioSource>(); bgm.loop = true; bgm.playOnAwake = false; bgm.volume = 0.12f;
            Build();
        }

        public void Play(string name, float volume = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(name, out var c)) return;
            var s = pool[next++ % pool.Length];
            s.pitch = pitch; s.volume = Mathf.Clamp01(volume) * Master; s.clip = c; s.Play();
        }

        /// <summary>Continuous rub texture: volume follows hand speed.</summary>
        public void SetRub(float level)
        {
            if (rub.clip == null) { rub.clip = clips["rub"]; }
            if (level > 0.01f && !rub.isPlaying) rub.Play();
            rub.volume = Mathf.Lerp(rub.volume, Mathf.Clamp01(level) * 0.6f * Master, 0.4f);
            if (level <= 0.01f && rub.volume < 0.02f && rub.isPlaying) rub.Stop();
        }

        public void SetPuff(float level)
        {
            if (puff.clip == null) puff.clip = clips["puffloop"];
            if (level > 0.01f && !puff.isPlaying) puff.Play();
            puff.volume = Mathf.Lerp(puff.volume, Mathf.Clamp01(level) * 0.5f * Master, 0.4f);
            if (level <= 0.01f && puff.volume < 0.02f && puff.isPlaying) puff.Stop();
        }

        public void StartBgm() { if (!bgm.isPlaying) { bgm.clip = clips["bgm"]; bgm.Play(); } }

        // ---------- synthesis ----------
        void Build()
        {
            clips["pop"] = Make(0.22f, t => Sine(300 + 700 * Mathf.Exp(-t * 30), t) * Mathf.Exp(-t * 18) * 0.9f);
            clips["thump"] = Make(0.35f, t => (Sine(70 - 30 * t, t) * 0.9f + Noise() * 0.15f * Mathf.Exp(-t * 40)) * Mathf.Exp(-t * 9));
            clips["click"] = Make(0.07f, t => Sine(1400, t) * Mathf.Exp(-t * 70) * 0.5f);
            clips["stitch"] = Make(0.16f, t => (Sine(900, t) * 0.4f + Noise() * 0.25f) * Mathf.Exp(-t * 26));
            clips["wrong"] = Make(0.2f, t => Sine(190 - 40 * t, t) * Mathf.Exp(-t * 11) * 0.6f);
            clips["grab"] = Make(0.1f, t => (Sine(520, t) * 0.35f + Noise() * 0.2f) * Mathf.Exp(-t * 35));
            clips["shutter"] = Make(0.25f, t => (Noise() * Mathf.Exp(-t * 60) + Sine(2200, t) * Mathf.Exp(-t * 90) * 0.5f) * 0.6f);
            clips["rub"] = Make(0.5f, t => Noise() * 0.5f * (0.6f + 0.4f * Mathf.Sin(t * 2 * Mathf.PI * 8)), true);
            clips["puffloop"] = Make(0.6f, t => Noise() * 0.35f * (0.7f + 0.3f * Mathf.Sin(t * 2 * Mathf.PI * 5)), true);
            clips["chime"] = Arp(new[] { 659.3f, 784f, 987.8f, 1318.5f }, 0.11f, 0.9f);
            clips["success"] = Arp(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.09f, 1.1f);
            clips["sparkle"] = Arp(new[] { 1568f, 2093f, 2637f }, 0.05f, 0.5f);
            clips["hint"] = Arp(new[] { 880f, 1175f }, 0.09f, 0.6f);
            clips["hello"] = Arp(new[] { 587.3f, 740f, 880f }, 0.08f, 0.8f);
            clips["water"] = Make(1.3f, t => (Noise() * 0.18f * (0.55f + 0.45f * Mathf.Sin(t * 2 * Mathf.PI * 13)) + Sine(500 + 260 * Mathf.Sin(t * 9), t) * 0.05f) * Mathf.Clamp01(t * 8) * Mathf.Clamp01((1.3f - t) * 6));
            clips["bgm"] = BuildBgm();
        }

        float Noise() => (float)(rng.NextDouble() * 2 - 1);
        static float Sine(float f, float t) => Mathf.Sin(2 * Mathf.PI * f * t);

        AudioClip Make(float seconds, System.Func<float, float> fn, bool loop = false)
        {
            int n = (int)(Rate * seconds);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float v = fn(t);
                if (loop) { float edge = Mathf.Min(1f, Mathf.Min(i, n - i) / 800f); v *= edge; }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var c = AudioClip.Create("sfx", n, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        AudioClip Arp(float[] notes, float step, float vol)
        {
            float total = step * notes.Length + 0.35f;
            return Make(total, t =>
            {
                float v = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float tt = t - k * step; if (tt < 0) continue;
                    v += (Sine(notes[k], tt) + 0.25f * Sine(notes[k] * 2, tt)) * Mathf.Exp(-tt * 7) * 0.35f;
                }
                return v * vol;
            });
        }

        AudioClip BuildBgm()
        {
            // gentle music-box loop in C major pentatonic, ~16s
            float[] scale = { 261.6f, 293.7f, 329.6f, 392f, 440f, 523.3f, 587.3f, 659.3f };
            int[] melody = { 4, 2, 3, 2, 0, 1, 2, -1, 5, 4, 3, 2, 1, 2, 0, -1, 4, 5, 6, 5, 4, 3, 2, -1, 3, 2, 1, 0, 1, 2, 0, -1 };
            float step = 0.5f;
            float total = melody.Length * step;
            return Make(total, t =>
            {
                float v = 0;
                int idx = Mathf.Clamp((int)(t / step), 0, melody.Length - 1);
                int deg = melody[idx];
                float tt = t - idx * step;
                if (deg >= 0) v += (Sine(scale[deg], tt) + 0.3f * Sine(scale[deg] * 2, tt)) * Mathf.Exp(-tt * 4.5f) * 0.4f;
                // soft pad
                int bar = (int)(t / (step * 8));
                float root = new[] { 130.8f, 110f, 98f, 130.8f }[bar % 4];
                v += Sine(root, t) * 0.12f + Sine(root * 1.5f, t) * 0.06f;
                return v * 0.8f;
            }, true);
        }
    }
}
