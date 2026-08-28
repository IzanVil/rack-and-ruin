using UnityEngine;

namespace ServerGame.Utils
{
    /// <summary>Sonido generado por síntesis: no hay ningún archivo de audio en el proyecto.
    /// Son tonos cortos con envolvente para los clics y los avisos.</summary>
    public static class Sfx
    {
        const int SampleRate = 44100;

        static AudioSource _source;
        static AudioClip _click;
        static AudioClip _alert;
        static AudioClip _success;
        static AudioClip _cash;
        static bool _muted;

        public static bool Muted
        {
            get => _muted;
            set => _muted = value;
        }

        public static void ToggleMute() => _muted = !_muted;

        public static void Click() => Play(_click ?? (_click = Tone(660f, 0.055f, 0.16f, 0.35f)));
        public static void Alert() => Play(_alert ?? (_alert = Tone(180f, 0.32f, 0.30f, 0.8f, 110f)));
        public static void Success() => Play(_success ?? (_success = Tone(880f, 0.16f, 0.18f, 0.25f, 1320f)));
        public static void Cash() => Play(_cash ?? (_cash = Tone(1240f, 0.12f, 0.15f, 0.2f, 1660f)));

        static void Play(AudioClip clip)
        {
            if (_muted || clip == null) return;

            var source = Source();
            if (source == null) return;
            source.PlayOneShot(clip);
        }

        static AudioSource Source()
        {
            if (_source != null) return _source;
            if (!Application.isPlaying) return null;

            var go = new GameObject("[Sfx]") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            return _source;
        }

        /// <summary>Tono con envolvente exponencial. Si se indica <paramref name="endFrequency"/>
        /// el tono barre de una frecuencia a otra.</summary>
        static AudioClip Tone(float frequency, float duration, float volume, float decay,
            float endFrequency = -1f)
        {
            int samples = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float freq = endFrequency > 0f ? Mathf.Lerp(frequency, endFrequency, t) : frequency;
                phase += 2f * Mathf.PI * freq / SampleRate;

                float envelope = Mathf.Exp(-t / Mathf.Max(0.001f, decay)) * (1f - t);
                // Onda sinusoidal con un toque de tercer armónico para que no suene plana.
                float wave = Mathf.Sin(phase) * 0.85f + Mathf.Sin(phase * 3f) * 0.15f;
                data[i] = wave * envelope * volume;
            }

            var clip = AudioClip.Create("SG_Tone_" + Mathf.RoundToInt(frequency), samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
