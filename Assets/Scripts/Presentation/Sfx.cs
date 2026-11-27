using UnityEngine;

namespace KingdomDeck.Presentation
{
    /// <summary>
    /// 임시 효과음: 코드로 합성한 짧은 소리들. 나중에 CardVisual의 효과음이나 실제 음원으로 교체.
    /// </summary>
    public static class Sfx
    {
        public static float Volume = 0.8f;
        public static bool Muted;

        static AudioSource source;
        const int Rate = 44100;

        static AudioClip click, card, coin, whoosh, boom, fire, arrows, repair, block, win, lose, shuffle, trash, chime, hit;
        public static AudioClip Click => click ?? (click = Make("click", 0.05f, (t, n) => Mathf.Sin(t * 2 * Mathf.PI * 1400) * Env(t, 0.05f, 0.002f) * 0.4f));
        public static AudioClip Card => card ?? (card = Make("card", 0.12f, (t, n) => n * Env(t, 0.12f, 0.005f) * 0.35f * (1f - t * 6f)));
        public static AudioClip Coin => coin ?? (coin = Make("coin", 0.35f, (t, n) =>
            (Mathf.Sin(t * 2 * Mathf.PI * 1976) * Env(t, 0.35f, 0.002f) + (t > 0.07f ? Mathf.Sin(t * 2 * Mathf.PI * 2637) * Env(t - 0.07f, 0.28f, 0.002f) : 0)) * 0.3f));
        public static AudioClip Whoosh => whoosh ?? (whoosh = MakeFiltered("whoosh", 0.45f, 0.08f, t => Mathf.Sin(t / 0.45f * Mathf.PI) * 0.5f));
        public static AudioClip Boom => boom ?? (boom = MakeFiltered("boom", 0.8f, 0.025f, t => Env(t, 0.8f, 0.004f) * 1.0f, 55f));
        public static AudioClip Fire => fire ?? (fire = MakeFiltered("fire", 1.0f, 0.18f, t => Mathf.Sin(t / 1.0f * Mathf.PI) * 0.45f));
        public static AudioClip Arrows => arrows ?? (arrows = Make("arrows", 0.6f, (t, n) =>
        {
            float a = 0;
            for (int k = 0; k < 6; k++) { float s = t - k * 0.07f; if (s > 0) a += n * Env(s, 0.09f, 0.002f); }
            return a * 0.25f;
        }));
        public static AudioClip Repair => repair ?? (repair = Make("repair", 0.6f, (t, n) =>
        {
            float f = t < 0.15f ? 523 : t < 0.3f ? 659 : t < 0.45f ? 784 : 1046;
            return Mathf.Sin(t * 2 * Mathf.PI * f) * Env(t, 0.6f, 0.01f) * 0.25f;
        }));
        public static AudioClip Block => block ?? (block = Make("block", 0.4f, (t, n) =>
            (Mathf.Sin(t * 2 * Mathf.PI * 220) * 0.6f + n * 0.4f) * Env(t, 0.4f, 0.002f) * 0.45f));
        public static AudioClip Hit => hit ?? (hit = MakeFiltered("hit", 0.35f, 0.05f, t => Env(t, 0.35f, 0.002f) * 0.9f, 90f));
        public static AudioClip Win => win ?? (win = Make("win", 1.4f, (t, n) =>
        {
            float[] notes = { 523, 659, 784, 1046 };
            int i = Mathf.Min(3, (int)(t / 0.18f));
            float local = t - i * 0.18f;
            return Mathf.Sin(t * 2 * Mathf.PI * notes[i]) * Env(local, i == 3 ? 0.9f : 0.2f, 0.01f) * 0.3f;
        }));
        public static AudioClip Lose => lose ?? (lose = Make("lose", 1.4f, (t, n) =>
        {
            float[] notes = { 392, 349, 311, 262 };
            int i = Mathf.Min(3, (int)(t / 0.25f));
            float local = t - i * 0.25f;
            return Mathf.Sin(t * 2 * Mathf.PI * notes[i]) * Env(local, i == 3 ? 0.9f : 0.28f, 0.01f) * 0.3f;
        }));
        public static AudioClip Shuffle => shuffle ?? (shuffle = Make("shuffle", 0.4f, (t, n) => n * (0.5f + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * 30)) * Env(t, 0.4f, 0.02f) * 0.22f));
        public static AudioClip Trash => trash ?? (trash = MakeFiltered("trash", 0.7f, 0.2f, t => Env(t, 0.7f, 0.05f) * 0.5f));
        public static AudioClip Chime => chime ?? (chime = Make("chime", 0.5f, (t, n) => (Mathf.Sin(t * 2 * Mathf.PI * 880) + 0.5f * Mathf.Sin(t * 2 * Mathf.PI * 1320)) * Env(t, 0.5f, 0.005f) * 0.2f));

        static float Env(float t, float len, float attack)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            return Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, len - attack)) * Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, len - attack));
        }

        static AudioClip Make(string name, float len, System.Func<float, float, float> f)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate, (float)(rnd.NextDouble() * 2 - 1)), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>저역 통과 노이즈 (+ 선택적으로 낮은 사인 섞기)</summary>
        static AudioClip MakeFiltered(string name, float len, float smooth, System.Func<float, float> env, float sineHz = 0f)
        {
            int n = Mathf.CeilToInt(len * Rate);
            var data = new float[n];
            var rnd = new System.Random(name.GetHashCode());
            float y = 0;
            float peak = 0.0001f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1);
                y += (x - y) * smooth;
                data[i] = y;
                peak = Mathf.Max(peak, Mathf.Abs(y));
            }
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float v = data[i] / peak * 0.8f;
                if (sineHz > 0) v = v * 0.6f + Mathf.Sin(t * 2 * Mathf.PI * sineHz * (1f - t * 0.5f)) * 0.6f;
                data[i] = Mathf.Clamp(v * env(t), -1, 1);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (Muted || clip == null) return;
            if (source == null)
            {
                var go = new GameObject("Sfx");
                Object.DontDestroyOnLoad(go);
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.pitch = pitch;
            source.PlayOneShot(clip, volume * Volume);
        }
    }
}
