using RichCoast.Core;
using UnityEngine;

namespace RichCoast.Game
{
    /// <summary>
    /// Procedural placeholder audio — synthesised <see cref="AudioClip"/>s (soft sine/triangle bells
    /// with an exponential envelope), no asset files, mirroring the web build's Web Audio engine.
    /// Cues pitch-climb through fast chains via the pure, unit-tested <see cref="ComboPitch"/>,
    /// applied as <see cref="AudioSource.pitch"/>. Replace clips with authored SFX later without
    /// touching callers.
    /// </summary>
    public sealed class Sfx : MonoBehaviour
    {
        const int SampleRate = 44100;
        static Sfx instance;

        GameFeelSO feel;
        AudioSource[] voices;
        int nextVoice;
        AudioClip drop, merge, mergeHigh, tick, goal, gameOver, transition, multiply, collect;
        readonly ComboState mergeCombo = new ComboState();
        readonly ComboState multiplyCombo = new ComboState();
        bool muted;

        public static Sfx Instance => instance;

        public static Sfx Create(GameFeelSO feel)
        {
            if (instance != null) return instance;
            var go = new GameObject("Sfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Sfx>();
            instance.feel = feel;
            instance.Build();
            return instance;
        }

        void Build()
        {
            voices = new AudioSource[8];
            for (int i = 0; i < voices.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                voices[i] = src;
            }
            drop = Synth("drop", 180f, 120f, Wave.Triangle, 0.004f, 0.12f, 0.18f);
            merge = Layer("merge", Synth("m1", 523.25f, 523.25f, Wave.Sine, 0.004f, 0.22f, 0.26f), Synth("m2", 1046.5f, 1046.5f, Wave.Sine, 0.004f, 0.12f, 0.08f));
            mergeHigh = Synth("mergeHigh", 659.25f, 659.25f, Wave.Sine, 0.004f, 0.3f, 0.3f);
            tick = Synth("tick", 784f, 784f, Wave.Sine, 0.003f, 0.1f, 0.16f);
            goal = Arpeggio("goal", new[] { 659.25f, 830.61f, 987.77f }, 0.09f, 0.28f, 0.5f);
            gameOver = Layer("gameOver", Synth("g1", 220f, 110f, Wave.Triangle, 0.01f, 0.6f, 0.3f), Synth("g2", 164.8f, 82.4f, Wave.Sine, 0.01f, 0.7f, 0.25f));
            transition = Synth("transition", 520f, 180f, Wave.Triangle, 0.004f, 0.2f, 0.22f);
            multiply = Synth("multiply", 392f, 392f, Wave.Triangle, 0.003f, 0.16f, 0.2f);
            collect = Synth("collect", 880f, 880f, Wave.Sine, 0.002f, 0.07f, 0.09f);
        }

        /// <summary>Zone C: the trap-door sucks a ball through. Downward whoosh.</summary>
        public void Transition() => Play(transition, 1f);

        /// <summary>Zone B: a ball splits into <paramref name="n"/> copies. Bright pluck that climbs through a fast chain.</summary>
        public void Multiply(int n)
        {
            if (n <= 1) return;
            double mult = ComboPitch.Next(multiplyCombo, Time.unscaledTime * 1000.0, feel.comboWindowMs, feel.comboMaxStep).Mult;
            Play(multiply, (float)mult);
        }

        /// <summary>Zone B: a ball drains. Quiet coin tick, tinted up with value — happens often, must not dominate.</summary>
        public void Collect(double value)
        {
            double semis = System.Math.Min(12, System.Math.Log(System.Math.Max(1, value), 2));
            Play(collect, Mathf.Pow(2f, (float)semis / 12f));
        }

        public void SetMuted(bool on) => muted = on;
        public void ToggleMute() => muted = !muted;

        /// <summary>Zone A: a ball is released. Soft low thunk.</summary>
        public void Drop() => Play(drop, 1f);

        /// <summary>Zone A: two balls merge. Bell ping that climbs through a fast chain.</summary>
        public void Merge(int tier)
        {
            double mult = ComboPitch.Next(mergeCombo, Time.unscaledTime * 1000.0, feel.comboWindowMs, feel.comboMaxStep).Mult;
            Play(tier >= feel.heavyHapticFromTier ? mergeHigh : merge, (float)mult);
        }

        /// <summary>One buffer slot arrives during a refill: ascending blip, index-th in the sequence.</summary>
        public void BufferTick(int index) => Play(tick, Mathf.Pow(2f, Mathf.Min(index, 8) / 12f));

        /// <summary>Score bar filled / round banked. The loudest, rarest cue.</summary>
        public void Goal() => Play(goal, 1f);

        public void GameOver() => Play(gameOver, 1f);

        void Play(AudioClip clip, float pitch)
        {
            if (muted || clip == null) return;
            var src = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            src.pitch = pitch;
            src.volume = feel.masterVolume;
            src.clip = clip;
            src.Play();
        }

        // --- synthesis ------------------------------------------------------------------

        enum Wave { Sine, Triangle }

        /// <summary>One voice: osc (freq gliding to glideTo) × exp attack/decay envelope × gain.</summary>
        static AudioClip Synth(string name, float freq, float glideTo, Wave wave, float attack, float decay, float gain)
        {
            float length = attack + decay + 0.02f;
            int n = Mathf.CeilToInt(length * SampleRate);
            var data = new float[n];
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SampleRate;
                float u = Mathf.Clamp01(t / (attack + decay));
                float f = freq * Mathf.Pow(glideTo / freq, u); // exponential glide
                phase += f / SampleRate;
                float env = t < attack ? Mathf.Pow(t / attack, 0.5f) : Mathf.Exp(-(t - attack) / decay * 5f);
                float s = wave == Wave.Sine ? Mathf.Sin((float)(phase * Mathf.PI * 2)) : Triangle((float)phase);
                data[i] = s * env * gain;
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float Triangle(float phase)
        {
            float p = phase - Mathf.Floor(phase);
            return 4f * Mathf.Abs(p - 0.5f) - 1f;
        }

        static AudioClip Layer(string name, params AudioClip[] clips)
        {
            int n = 0;
            foreach (var c in clips) n = Mathf.Max(n, c.samples);
            var mix = new float[n];
            foreach (var c in clips)
            {
                var d = new float[c.samples];
                c.GetData(d, 0);
                for (int i = 0; i < d.Length; i++) mix[i] += d[i];
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(mix, 0);
            return clip;
        }

        static AudioClip Arpeggio(string name, float[] notes, float gapS, float decay, float gain)
        {
            int n = Mathf.CeilToInt((gapS * notes.Length + decay + 0.05f) * SampleRate);
            var mix = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                var voice = Synth("v", notes[k], notes[k], Wave.Sine, 0.005f, decay, gain);
                var d = new float[voice.samples];
                voice.GetData(d, 0);
                int offset = Mathf.RoundToInt(k * gapS * SampleRate);
                for (int i = 0; i < d.Length && offset + i < n; i++) mix[offset + i] += d[i];
            }
            var clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(mix, 0);
            return clip;
        }
    }
}
