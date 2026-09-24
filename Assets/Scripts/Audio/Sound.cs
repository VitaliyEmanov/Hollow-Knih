using System.Collections;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;

namespace AshenWick
{
    /// <summary>
    /// All audio is synthesized at runtime: sound effects and melancholic ambient scores
    /// (pads, plucked "music-box" melodies, drones) plus driving boss themes.
    /// </summary>
    public sealed class Sound : MonoBehaviour
    {
        const int Rate = 22050;
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        readonly Dictionary<int, AudioClip> music = new Dictionary<int, AudioClip>();
        static readonly Dictionary<int, float[]> musicData = new Dictionary<int, float[]>();
        static readonly int[] AllMusic = { -1, 0, -2, 10, 1, 11, 2, 3, 12, -3 };
        readonly List<AudioSource> voices = new List<AudioSource>();
        AudioSource musicA, musicB;
        int currentMusic = int.MinValue;
        Coroutine fade;
        public float MusicVolume = 0.55f, SfxVolume = 0.8f;

        void Awake()
        {
            for (int i = 0; i < 16; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                voices.Add(s);
            }
            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var m in new[] { musicA, musicB }) { m.loop = true; m.playOnAwake = false; m.volume = 0f; }

            // compose every score on a worker thread so the game never hitches when music changes
            var worker = new Thread(() =>
            {
                foreach (int id in AllMusic)
                {
                    lock (musicData) { if (musicData.ContainsKey(id)) continue; }
                    var d = Compose(id);
                    lock (musicData) { if (!musicData.ContainsKey(id)) musicData[id] = d; }
                }
            });
            worker.IsBackground = true;
            if (Application.platform != RuntimePlatform.WebGLPlayer)
            {
                try { worker.Start(); } catch (System.Exception) { /* composed on demand instead */ }
            }
        }

        public void Play(string id, float volume = 1f, float pitchVar = 0.06f)
        {
            var clip = GetSfx(id);
            if (clip == null) return;
            AudioSource v = null;
            foreach (var s in voices) if (!s.isPlaying) { v = s; break; }
            if (v == null) v = voices[Random.Range(0, voices.Count)];
            v.pitch = 1f + Random.Range(-pitchVar, pitchVar);
            v.volume = volume * SfxVolume;
            v.clip = clip;
            v.Play();
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------

        public void PlayMusic(int id)
        {
            if (id == currentMusic) return;
            currentMusic = id;
            var clip = GetMusic(id);
            var from = musicA.isPlaying && musicA.volume > 0.01f ? musicA : musicB;
            var to = from == musicA ? musicB : musicA;
            to.clip = clip;
            to.volume = 0f;
            if (clip != null) to.Play();
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(CrossFade(from, to, 1.6f));
        }

        public void StopMusic(float time)
        {
            currentMusic = int.MinValue;
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(CrossFade(musicA.isPlaying ? musicA : musicB, null, time));
        }

        IEnumerator CrossFade(AudioSource from, AudioSource to, float time)
        {
            float t = 0f;
            float a0 = from != null ? from.volume : 0f;
            while (t < time)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / time);
                if (from != null) from.volume = a0 * (1f - k);
                if (to != null) to.volume = MusicVolume * k;
                yield return null;
            }
            if (from != null && from != to) { from.Stop(); from.volume = 0f; }
        }

        AudioClip GetMusic(int id)
        {
            AudioClip c;
            if (music.TryGetValue(id, out c)) return c;
            float[] data;
            lock (musicData) { musicData.TryGetValue(id, out data); }
            if (data == null)
            {
                data = Compose(id);
                lock (musicData) { musicData[id] = data; }
            }
            c = AudioClip.Create("music" + id, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            music[id] = c;
            return c;
        }

        static float[] Compose(int id)
        {
            switch (id)
            {
                case 10: return BossTheme(38, 0.9f, 1);      // Gornan: heavy anvil march
                case 11: return BossTheme(41, 1.0f, 2);      // Weaver: eerie pulse
                case 12: return BossTheme(36, 1.15f, 3);     // Cinder King: grand and desperate
                case -3: return Ambient(48, true, 5, 0.7f);  // ending: warm, major
                case -2: return Ambient(45, false, 9, 0.5f); // prologue
                case -1: return Ambient(45, false, 1, 0.8f); // title
                case 1: return Ambient(43, false, 2, 1f);
                case 2: return Ambient(50, false, 3, 0.9f);
                case 3: return Ambient(40, false, 4, 1.1f);
                default: return Ambient(45, false, 0, 1f);
            }
        }

        static float Midi(float n) { return 440f * Mathf.Pow(2f, (n - 69f) / 12f); }

        /// <summary>Slow ambient score: pad chords, drone, plucked melody. root = MIDI note.</summary>
        static float[] Ambient(int root, bool major, int seed, float density)
        {
            var rng = new System.Random(seed * 97 + 13);
            float bpm = 64f;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            int n = (int)(len * Rate);
            var buf = new float[n];
            int[] minorProg = { 0, -4, -7, -2, 0, 3, -5, -2 };
            int[] majorProg = { 0, 5, -3, 7, 0, 5, 7, 0 };
            int[] prog = major ? majorProg : minorProg;
            int[] scale = major ? new[] { 0, 2, 4, 7, 9, 12, 14, 16 } : new[] { 0, 2, 3, 7, 8, 12, 14, 15 };

            // pads
            for (int bar = 0; bar < bars; bar++)
            {
                int chordRoot = root + prog[bar];
                int third = major ? 4 : 3;
                if (!major && (prog[bar] == -4 || prog[bar] == 3)) third = 4;
                int[] notes = { chordRoot - 12, chordRoot, chordRoot + third, chordRoot + 7, chordRoot + 12 };
                int start = (int)(bar * 4 * beat * Rate);
                int dur = (int)(4 * beat * Rate * 1.25f);
                foreach (int note in notes)
                {
                    float f = Midi(note);
                    float detune = 1.003f;
                    for (int i = 0; i < dur; i++)
                    {
                        int idx = (start + i) % n;
                        float t = i / (float)Rate;
                        float env = Mathf.Clamp01(t / 1.6f) * Mathf.Clamp01((dur / (float)Rate - t) / 1.8f);
                        float s = Mathf.Sin(2 * Mathf.PI * f * t) + Mathf.Sin(2 * Mathf.PI * f * detune * t) * 0.7f + Mathf.Sin(2 * Mathf.PI * f * 2 * t) * 0.12f;
                        buf[idx] += s * env * 0.028f;
                    }
                }
            }
            // low drone with slow breathing
            float df = Midi(root - 24);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float br = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * t / len * 2);
                buf[i] += (Mathf.Sin(2 * Mathf.PI * df * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * df * 1.5f * t)) * 0.05f * br;
            }
            // music-box melody
            int steps = bars * 8;
            for (int s = 0; s < steps; s++)
            {
                if (rng.NextDouble() > 0.42 * density) continue;
                int bar = s / 8;
                int note = root + 12 + prog[bar] + scale[rng.Next(scale.Length)];
                float f = Midi(note);
                int start = (int)(s * beat * 0.5f * Rate);
                int dur = (int)(Rate * 2.2f);
                float amp = 0.07f + (float)rng.NextDouble() * 0.04f;
                for (int i = 0; i < dur; i++)
                {
                    int idx = (start + i) % n;
                    float t = i / (float)Rate;
                    float env = Mathf.Exp(-t * 2.4f) * Mathf.Clamp01(t * 200f);
                    float v = Mathf.Sin(2 * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2 * Mathf.PI * f * 2 * t) * Mathf.Exp(-t * 5f) + 0.1f * Mathf.Sin(2 * Mathf.PI * f * 3.01f * t) * Mathf.Exp(-t * 8f);
                    buf[idx] += v * env * amp;
                }
                // soft echo
                int echo = (int)(beat * 0.75f * Rate);
                for (int i = 0; i < dur; i++)
                {
                    int idx = (start + echo + i) % n;
                    float t = i / (float)Rate;
                    buf[idx] += Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 3f) * Mathf.Clamp01(t * 200f) * amp * 0.3f;
                }
            }
            Normalize(buf, 0.8f);
            return buf;
        }

        /// <summary>Driving boss theme: ostinato low strings, timpani, stabs and a lament melody.</summary>
        static float[] BossTheme(int root, float tempoMul, int seed)
        {
            var rng = new System.Random(seed * 131);
            float bpm = 132f * tempoMul;
            float beat = 60f / bpm;
            int bars = 8;
            float len = bars * 4 * beat;
            int n = (int)(len * Rate);
            var buf = new float[n];
            int[] prog = seed == 2 ? new[] { 0, 1, 0, -2, 0, 1, 3, -1 } : new[] { 0, 0, -4, -2, 0, 0, 1, -1 };

            // ostinato: filtered saw eighth notes
            float lp = 0f;
            for (int e = 0; e < bars * 8; e++)
            {
                int bar = e / 8;
                int[] pattern = { 0, 0, 12, 0, 7, 0, 12, 3 };
                int note = root + prog[bar] + pattern[e % 8];
                float f = Midi(note);
                int start = (int)(e * beat * 0.5f * Rate);
                int dur = (int)(beat * 0.5f * Rate);
                for (int i = 0; i < dur; i++)
                {
                    float t = i / (float)Rate;
                    float saw = 2f * ((f * t) % 1f) - 1f;
                    float env = Mathf.Exp(-t * 7f);
                    lp += (saw - lp) * 0.18f;
                    buf[(start + i) % n] += lp * env * 0.22f;
                }
            }
            // timpani / anvil hits
            for (int b = 0; b < bars * 4; b++)
            {
                bool hit = b % 4 == 0 || (b % 4 == 2 && rng.NextDouble() < 0.6) || (b % 8 == 7);
                if (!hit) continue;
                int start = (int)(b * beat * Rate);
                int dur = (int)(Rate * 0.6f);
                float f0 = Midi(root - 12 + prog[b / 4]);
                for (int i = 0; i < dur && start + i < n; i++)
                {
                    float t = i / (float)Rate;
                    float f = f0 * (1f + 0.6f * Mathf.Exp(-t * 30f));
                    float v = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 6f) * 0.5f;
                    if (seed == 1) v += ((float)rng.NextDouble() * 2 - 1) * Mathf.Exp(-t * 40f) * 0.25f; // anvil clang
                    buf[start + i] += v;
                }
            }
            // stabs on bar starts (brass-ish: stacked squares)
            for (int bar = 0; bar < bars; bar += 2)
            {
                int start = (int)(bar * 4 * beat * Rate);
                int dur = (int)(beat * 1.5f * Rate);
                int r = root + 12 + prog[bar];
                foreach (int nt in new[] { r, r + 3, r + 7 })
                {
                    float f = Midi(nt);
                    float l2 = 0f;
                    for (int i = 0; i < dur; i++)
                    {
                        float t = i / (float)Rate;
                        float sq = Mathf.Sin(2 * Mathf.PI * f * t) > 0 ? 1f : -1f;
                        l2 += (sq - l2) * 0.08f;
                        buf[(start + i) % n] += l2 * Mathf.Exp(-t * 2.5f) * 0.06f;
                    }
                }
            }
            // lament melody (strings) in the upper register
            int[] scale = { 0, 2, 3, 5, 7, 8, 11, 12 };
            for (int q = 0; q < bars * 2; q++)
            {
                if (rng.NextDouble() < 0.25) continue;
                int bar = q / 2;
                int note = root + 24 + prog[bar] + scale[rng.Next(scale.Length)];
                float f = Midi(note);
                int start = (int)(q * 2 * beat * Rate);
                int dur = (int)(2 * beat * Rate);
                for (int i = 0; i < dur; i++)
                {
                    float t = i / (float)Rate;
                    float vib = 1f + 0.004f * Mathf.Sin(2 * Mathf.PI * 5.5f * t);
                    float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((dur / (float)Rate - t) / 0.2f);
                    float v = Mathf.Sin(2 * Mathf.PI * f * vib * t) + 0.4f * Mathf.Sin(2 * Mathf.PI * f * 2 * vib * t) + 0.2f * Mathf.Sin(2 * Mathf.PI * f * 3 * vib * t);
                    buf[(start + i) % n] += v * env * 0.05f;
                }
            }
            Normalize(buf, 0.85f);
            return buf;
        }

        static void Normalize(float[] b, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        // ------------------------------------------------------------------
        // SFX synthesis
        // ------------------------------------------------------------------

        AudioClip GetSfx(string id)
        {
            AudioClip c;
            if (sfx.TryGetValue(id, out c)) return c;
            float[] d = Synth(id);
            if (d == null) return null;
            c = AudioClip.Create(id, d.Length, 1, Rate, false);
            c.SetData(d, 0);
            sfx[id] = c;
            return c;
        }

        static float[] Synth(string id)
        {
            var rng = new System.Random(id.GetHashCode());
            System.Func<float> noise = () => (float)rng.NextDouble() * 2f - 1f;
            float[] b;
            float lp = 0f, lp2 = 0f;
            switch (id)
            {
                case "slash":
                    b = New(0.16f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float k = 0.05f + 0.5f * Mathf.Exp(-t * 20f);
                        lp += (noise() - lp) * k;
                        b[i] = (lp - lp2) * 2.2f * Env(t, 0.005f, 0.13f); lp2 += (lp - lp2) * 0.1f;
                    }
                    break;
                case "hit":
                    b = New(0.22f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float f = 180f * Mathf.Exp(-t * 18f) + 45f;
                        lp += (noise() - lp) * 0.3f;
                        b[i] = Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 16f) * 0.9f + lp * Mathf.Exp(-t * 40f) * 0.8f;
                    }
                    break;
                case "enemydie":
                    b = New(0.45f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i);
                        lp += (noise() - lp) * (0.4f * Mathf.Exp(-t * 6f) + 0.02f);
                        b[i] = lp * Mathf.Exp(-t * 6f) * 1.3f + Mathf.Sin(2 * Mathf.PI * (90f + 200f * Mathf.Exp(-t * 20f)) * t) * Mathf.Exp(-t * 10f) * 0.6f;
                    }
                    break;
                case "hurt":
                    b = New(0.5f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float f = 420f * Mathf.Exp(-t * 5f) + 60f;
                        float sq = Mathf.Sin(2 * Mathf.PI * f * t) > 0 ? 1f : -1f;
                        lp += (noise() - lp) * 0.5f;
                        b[i] = (sq * 0.35f + lp * 0.6f) * Mathf.Exp(-t * 7f);
                    }
                    break;
                case "jump":
                    b = New(0.14f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * (0.05f + t * 2f);
                        b[i] = lp * Env(t, 0.01f, 0.12f) * 0.8f;
                    }
                    break;
                case "land":
                    b = New(0.1f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * 0.08f;
                        b[i] = (lp * 2f + Mathf.Sin(2 * Mathf.PI * 70 * t) * 0.4f) * Mathf.Exp(-t * 40f);
                    }
                    break;
                case "dash":
                    b = New(0.3f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float k = 0.03f + 0.3f * Mathf.Sin(Mathf.PI * t / 0.3f);
                        lp += (noise() - lp) * k;
                        b[i] = lp * 1.4f * Env(t, 0.02f, 0.26f);
                    }
                    break;
                case "flame":
                case "fire":
                    b = New(id == "flame" ? 0.6f : 0.8f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * 0.12f;
                        float crackle = rng.NextDouble() < 0.004 ? noise() * 3f : 0f;
                        b[i] = (lp * 1.8f + crackle) * Env(t, 0.03f, b.Length / (float)Rate - 0.03f) + Mathf.Sin(2 * Mathf.PI * (120f - t * 60f) * t) * 0.25f * Mathf.Exp(-t * 4f);
                    }
                    break;
                case "heal":
                    b = New(0.9f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i);
                        float f = 440f * Mathf.Pow(2f, Mathf.Floor(t * 10f) * 2f / 12f);
                        b[i] = (Mathf.Sin(2 * Mathf.PI * f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * f * 2.01f * t)) * Env(t, 0.05f, 0.8f) * 0.4f;
                    }
                    break;
                case "focus":
                    b = New(0.6f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float f = 220f + t * 300f;
                        b[i] = Mathf.Sin(2 * Mathf.PI * f * t) * 0.25f * Env(t, 0.2f, 0.4f);
                    }
                    break;
                case "pickup":
                case "rest":
                    b = New(1.6f);
                    {
                        float[] notes = id == "pickup" ? new[] { 72f, 76f, 79f, 84f } : new[] { 60f, 64f, 67f, 72f };
                        for (int k = 0; k < notes.Length; k++)
                        {
                            float f = Midi(notes[k]); int off = (int)(k * 0.09f * Rate);
                            for (int i = 0; i + off < b.Length; i++)
                            {
                                float t = T(i);
                                b[i + off] += (Mathf.Sin(2 * Mathf.PI * f * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * f * 2.76f * t) * Mathf.Exp(-t * 6f)) * Mathf.Exp(-t * 2.2f) * 0.3f;
                            }
                        }
                    }
                    break;
                case "ui":
                    b = New(0.06f);
                    for (int i = 0; i < b.Length; i++) { float t = T(i); b[i] = Mathf.Sin(2 * Mathf.PI * 880 * t) * Mathf.Exp(-t * 60f) * 0.6f; }
                    break;
                case "gate":
                    b = New(0.9f);
                    {
                        float[] fr = { 180f, 267f, 431f, 612f, 890f };
                        for (int i = 0; i < b.Length; i++)
                        {
                            float t = T(i); float v = 0;
                            for (int k = 0; k < fr.Length; k++) v += Mathf.Sin(2 * Mathf.PI * fr[k] * t) * Mathf.Exp(-t * (3f + k * 2f)) / (k + 1);
                            lp += (noise() - lp) * 0.3f;
                            b[i] = v * 0.6f + lp * Mathf.Exp(-t * 30f) * 0.6f;
                        }
                    }
                    break;
                case "roar":
                    b = New(1.8f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float f = 70f + 12f * Mathf.Sin(2 * Mathf.PI * 7f * t) + 20f * Mathf.Sin(Mathf.PI * t / 1.8f);
                        float saw = 2f * ((f * t) % 1f) - 1f;
                        lp += (saw * 0.7f + noise() * 0.5f - lp) * 0.2f;
                        b[i] = lp * Env(t, 0.15f, 1.5f) * 1.4f;
                    }
                    break;
                case "slam":
                    b = New(0.9f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * (0.3f * Mathf.Exp(-t * 8f) + 0.02f);
                        b[i] = Mathf.Sin(2 * Mathf.PI * (55f + 80f * Mathf.Exp(-t * 25f)) * t) * Mathf.Exp(-t * 4f) + lp * Mathf.Exp(-t * 3f) * 1.5f;
                    }
                    break;
                case "boom":
                    b = New(2f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * (0.25f * Mathf.Exp(-t * 3f) + 0.01f);
                        b[i] = lp * Mathf.Exp(-t * 1.8f) * 2f + Mathf.Sin(2 * Mathf.PI * 40f * t) * Mathf.Exp(-t * 2f) * 0.6f;
                    }
                    break;
                case "shoot":
                    b = New(0.25f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * 0.2f;
                        b[i] = (Mathf.Sin(2 * Mathf.PI * (500f * Mathf.Exp(-t * 12f) + 100f) * t) * 0.5f + lp * 0.5f) * Mathf.Exp(-t * 12f);
                    }
                    break;
                case "wing":
                    b = New(0.22f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); lp += (noise() - lp) * 0.06f;
                        b[i] = lp * 3f * Mathf.Sin(Mathf.PI * t / 0.22f);
                    }
                    break;
                case "telegraph":
                    b = New(0.45f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i);
                        b[i] = (Mathf.Sin(2 * Mathf.PI * 1760f * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 2637f * t)) * Mathf.Exp(-t * 7f) * 0.35f;
                    }
                    break;
                case "death":
                    b = New(2.2f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float f = Midi(57) * Mathf.Pow(0.5f, t / 2.2f);
                        lp += (noise() - lp) * 0.05f;
                        b[i] = (Mathf.Sin(2 * Mathf.PI * f * t) * 0.5f + lp * 0.8f) * Env(t, 0.05f, 2.1f);
                    }
                    break;
                case "shatter":
                    b = New(0.7f);
                    for (int i = 0; i < b.Length; i++)
                    {
                        float t = T(i); float v = noise() * Mathf.Exp(-t * 9f);
                        for (int k = 0; k < 4; k++) v += Mathf.Sin(2 * Mathf.PI * (1200f + k * 713f) * t) * Mathf.Exp(-t * (6f + k * 3f)) * 0.2f;
                        b[i] = v * 0.7f;
                    }
                    break;
                default:
                    return null;
            }
            return b;
        }

        static float[] New(float seconds) { return new float[Mathf.Max(1, (int)(seconds * Rate))]; }
        static float T(int i) { return i / (float)Rate; }
        static float Env(float t, float attack, float release) { return Mathf.Clamp01(t / attack) * Mathf.Clamp01((release - t) / (release * 0.6f) + 0.0f); }
    }
}
