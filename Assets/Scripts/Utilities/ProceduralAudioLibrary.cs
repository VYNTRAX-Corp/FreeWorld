using UnityEngine;
using System;

namespace FreeWorld.Utilities
{
    /// <summary>
    /// Generates synthetic AudioClips at runtime with zero external asset dependencies.
    /// All clips are cached after first creation. Call ProceduralAudioLibrary.Apply(gameObject)
    /// to push the right clip to an AudioSource, or use the static Clip* properties directly.
    ///
    /// Sound taxonomy
    ///   ClipGunshot        — player / enemy short crack + noise burst
    ///   ClipShotgunBlast   — heavier wide burst
    ///   ClipBulletImpact   — sharp metallic tick (concrete / metal)
    ///   ClipFleshHit       — dull thud (hitting an enemy)
    ///   ClipFootstep       — low weight step
    ///   ClipPlayerHurt     — short groan / distort
    ///   ClipEnemyHurt      — higher pitched version
    ///   ClipEnemyDeath     — descending tone + noise tail
    ///   ClipEnemyAlert     — rising electronic beep
    ///   ClipPickup         — short ascending arpeggio chime
    ///   ClipReload         — mechanical click pattern
    ///   ClipEmpty          — dry metallic click (dry-fire)
    ///   ClipHitMarker      — ultra-short beep (UI feedback on enemy hit)
    ///   ClipRoundStart     — low whoosh sweep
    /// </summary>
    public static class ProceduralAudioLibrary
    {
        private const int   SampleRate = 22050;
        private const float TwoPi      = Mathf.PI * 2f;

        // ── Cached clips ──────────────────────────────────────────────────────
        private static AudioClip _gunshot;
        private static AudioClip _shotgunBlast;
        private static AudioClip _bulletImpact;
        private static AudioClip _fleshHit;
        private static AudioClip _footstep;
        private static AudioClip _playerHurt;
        private static AudioClip _enemyHurt;
        private static AudioClip _enemyDeath;
        private static AudioClip _enemyAlert;
        private static AudioClip _pickup;
        private static AudioClip _reload;
        private static AudioClip _empty;
        private static AudioClip _hitMarker;
        private static AudioClip _roundStart;

        public static AudioClip ClipGunshot      => _gunshot      ??= BuildGunshot(0.18f, 280f, 3400f);
        public static AudioClip ClipShotgunBlast => _shotgunBlast ??= BuildGunshot(0.28f, 140f, 2200f);
        public static AudioClip ClipBulletImpact => _bulletImpact ??= BuildImpact(0.06f, 1800f, 0.35f);
        public static AudioClip ClipFleshHit     => _fleshHit     ??= BuildImpact(0.08f, 420f, 0.6f);
        public static AudioClip ClipFootstep     => _footstep     ??= BuildFootstep();
        public static AudioClip ClipPlayerHurt   => _playerHurt   ??= BuildHurt(0.22f, 180f, 0.8f);
        public static AudioClip ClipEnemyHurt    => _enemyHurt    ??= BuildHurt(0.18f, 280f, 0.5f);
        public static AudioClip ClipEnemyDeath   => _enemyDeath   ??= BuildDeath();
        public static AudioClip ClipEnemyAlert   => _enemyAlert   ??= BuildAlert();
        public static AudioClip ClipPickup       => _pickup       ??= BuildPickup();
        public static AudioClip ClipReload       => _reload       ??= BuildReload();
        public static AudioClip ClipEmpty        => _empty        ??= BuildEmpty();
        public static AudioClip ClipHitMarker    => _hitMarker    ??= BuildTone(0.03f, 1200f, 0.0f, 0.85f);
        public static AudioClip ClipRoundStart   => _roundStart   ??= BuildRoundStart();

        // ─────────────────────────────────────────────────────────────────────
        //  Play helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Play a one-shot clip in world-space (does NOT require an AudioSource).</summary>
        public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, volume);
        }

        /// <summary>Play via an existing AudioSource.</summary>
        public static void Play(AudioSource src, AudioClip clip, float volume = 1f)
        {
            if (src == null || clip == null) return;
            src.PlayOneShot(clip, volume);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Synthesis helpers
        // ─────────────────────────────────────────────────────────────────────

        private static float[] MakeBuffer(float seconds) =>
            new float[Mathf.CeilToInt(SampleRate * seconds)];

        private static AudioClip Bake(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Exponential decay envelope
        private static float Env(int i, int total, float attack = 0.005f)
        {
            float t = (float)i / total;
            float a = Mathf.SmoothStep(0f, 1f, t / attack);   // short attack
            float d = Mathf.Exp(-t * 8f);                     // exponential decay
            return a * d;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Individual sound builders
        // ─────────────────────────────────────────────────────────────────────

        // Gunshot: short noise burst shaped by a pitched sinusoid + rapid decay
        private static AudioClip BuildGunshot(float duration, float bassFreq, float crackFreq)
        {
            var buf = MakeBuffer(duration);
            int n   = buf.Length;
            var rng = new System.Random(42);

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 28f);                         // fast decay

                // Sub/bass thump
                float bass  = Mathf.Sin(TwoPi * bassFreq  * t) * 0.6f;
                // High transient crack
                float crack = Mathf.Sin(TwoPi * crackFreq * t) * 0.3f;
                // White noise burst
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.55f;
                // High-passed noise tail that lingers
                float tail  = Mathf.Sin(TwoPi * 800f * t) * Mathf.Exp(-t * 40f) * 0.2f;

                buf[i] = Mathf.Clamp((bass + crack + noise + tail) * env, -1f, 1f);
            }
            return Bake("Gunshot", buf);
        }

        // Impact: short metallic tick shaped by frequency and decay factor
        private static AudioClip BuildImpact(float duration, float freq, float decayMult)
        {
            var buf = MakeBuffer(duration);
            int n   = buf.Length;
            var rng = new System.Random(7);

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-t * (30f * decayMult));
                float tone  = Mathf.Sin(TwoPi * freq * t) * 0.5f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.45f;
                buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }
            return Bake("Impact", buf);
        }

        // Footstep: low sinusoidal thud
        private static AudioClip BuildFootstep()
        {
            float dur = 0.12f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            var rng = new System.Random(13);

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 45f);
                float sub = Mathf.Sin(TwoPi * 80f  * t) * 0.7f;
                float mid = Mathf.Sin(TwoPi * 200f * t) * 0.25f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.1f;
                buf[i] = Mathf.Clamp((sub + mid + noise) * env, -1f, 1f);
            }
            return Bake("Footstep", buf);
        }

        // Hurt: distorted low tone
        private static AudioClip BuildHurt(float duration, float freq, float distPerc)
        {
            var buf = MakeBuffer(duration);
            int n   = buf.Length;
            var rng = new System.Random(99);

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 9f) * Mathf.SmoothStep(0f, 1f, t / 0.01f);
                float tone = Mathf.Sin(TwoPi * freq * t);
                // Soft clip / distortion
                tone = Mathf.Clamp(tone * (1f + distPerc * 2f), -1f, 1f);
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
                buf[i] = Mathf.Clamp((tone + noise) * env * 0.8f, -1f, 1f);
            }
            return Bake("Hurt", buf);
        }

        // Death: descending pitched sweep + noise tail
        private static AudioClip BuildDeath()
        {
            float dur = 0.6f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            var rng = new System.Random(55);

            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / SampleRate;
                float freq  = 350f * Mathf.Exp(-t * 3.5f);  // pitch drops over time
                float env  = Mathf.Exp(-t * 5f);
                float tone = Mathf.Sin(TwoPi * freq * t) * 0.6f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.35f * Mathf.Exp(-t * 8f);
                buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }
            return Bake("Death", buf);
        }

        // Alert: quick rising double-beep
        private static AudioClip BuildAlert()
        {
            float dur = 0.25f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                // Two beeps: 0–0.08 s and 0.12–0.20 s, second slightly higher
                bool beep1 = t < 0.07f;
                bool beep2 = t >= 0.12f && t < 0.20f;
                float freq = beep1 ? 880f : (beep2 ? 1100f : 0f);
                float env  = beep1 ? Mathf.Exp(-(t)         * 30f) :
                             beep2 ? Mathf.Exp(-(t - 0.12f) * 30f) : 0f;
                buf[i] = Mathf.Sin(TwoPi * freq * t) * env * 0.6f;
            }
            return Bake("Alert", buf);
        }

        // Pickup: short ascending three-note arpeggio
        private static AudioClip BuildPickup()
        {
            float dur = 0.25f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            float[] freqs = { 880f, 1100f, 1320f };  // C-E-G arpeggio feel
            float noteLen = dur / freqs.Length;

            for (int i = 0; i < n; i++)
            {
                float t       = (float)i / SampleRate;
                int   noteIdx = Mathf.Min((int)(t / noteLen), freqs.Length - 1);
                float tNote   = t - noteIdx * noteLen;
                float env     = Mathf.Exp(-tNote * 25f);
                buf[i] = Mathf.Sin(TwoPi * freqs[noteIdx] * t) * env * 0.55f;
            }
            return Bake("Pickup", buf);
        }

        // Reload: two mechanical clicks spaced apart
        private static AudioClip BuildReload()
        {
            float dur = 0.45f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            var rng = new System.Random(11);

            // Clicks at t=0.05 and t=0.30
            float[] clickTimes = { 0.05f, 0.30f };
            foreach (float ct in clickTimes)
            {
                for (int i = 0; i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float d = t - ct;
                    if (d < 0f || d > 0.04f) continue;
                    float env   = Mathf.Exp(-d * 150f);
                    float tone  = Mathf.Sin(TwoPi * 2400f * t) * 0.3f;
                    float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.4f;
                    buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
                }
            }
            return Bake("Reload", buf);
        }

        // Empty / dry-fire: single short click
        private static AudioClip BuildEmpty()
        {
            float dur = 0.05f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            var rng = new System.Random(21);

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 120f);
                float tone = Mathf.Sin(TwoPi * 3200f * t) * 0.25f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }
            return Bake("Empty", buf);
        }

        // Generic tone: single sustained beep (used for hit marker)
        private static AudioClip BuildTone(float duration, float freq, float freqDelta, float decay)
        {
            var buf = MakeBuffer(duration);
            int n   = buf.Length;

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / SampleRate;
                float f   = freq + freqDelta * t;
                float env = Mathf.Exp(-t * (decay * 80f + 2f));
                buf[i] = Mathf.Sin(TwoPi * f * t) * env * 0.55f;
            }
            return Bake("Tone", buf);
        }

        // Round start: low whoosh sweep
        private static AudioClip BuildRoundStart()
        {
            float dur = 0.8f;
            var buf = MakeBuffer(dur);
            int n   = buf.Length;
            var rng = new System.Random(77);

            for (int i = 0; i < n; i++)
            {
                float t    = (float)i / SampleRate;
                float freq = 100f + 600f * (t / dur);          // sweep 100→700 Hz
                float env  = Mathf.SmoothStep(0f, 1f, t / 0.1f) * Mathf.Exp(-t * 2.5f);
                float tone = Mathf.Sin(TwoPi * freq * t) * 0.45f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
                buf[i] = Mathf.Clamp((tone + noise) * env, -1f, 1f);
            }
            return Bake("RoundStart", buf);
        }
    }
}
