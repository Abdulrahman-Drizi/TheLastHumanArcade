using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The colossus's voice. There are no sound files: every sound is worked out here when the
    /// scene starts. A low hum while it is awake; a thump when a blow lands; a rush of air when it
    /// winds up; a roar when it leaps or loses a battery. All of it gets louder and higher with
    /// its anger.
    /// </summary>
    public class BossAudio : MonoBehaviour
    {
        [Range(0f, 1f)] public float master = 0.85f;

        const int Rate = 44100;
        AudioSource hum, shots;
        AudioClip thump, whoosh, roar;
        int anger;
        bool live;

        void Awake()
        {
            hum = gameObject.AddComponent<AudioSource>();
            hum.loop = true;
            hum.playOnAwake = false;
            hum.spatialBlend = 0f;
            hum.volume = 0f;
            hum.clip = Make("BossHum", 2f, t =>
                0.5f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.3f * Mathf.Sin(2f * Mathf.PI * 82.5f * t) + 0.16f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                * (0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 4f * t)));
            shots = gameObject.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.spatialBlend = 0f;

            var noise = new System.Random(7);
            float held = 0f;
            thump = Make("BossThump", 0.7f, t =>
            {
                float pitch = Mathf.Lerp(62f, 34f, t / 0.7f);
                float n = (float)noise.NextDouble() * 2f - 1f;
                return Mathf.Sin(2f * Mathf.PI * pitch * t) * Mathf.Exp(-5.5f * t) + n * 0.55f * Mathf.Exp(-28f * t);
            });
            whoosh = Make("BossWhoosh", 0.45f, t =>
            {
                float n = (float)noise.NextDouble() * 2f - 1f;
                held += (n - held) * Mathf.Lerp(0.05f, 0.5f, t / 0.45f);           // noise that brightens as it goes
                float env = Mathf.Sin(Mathf.PI * t / 0.45f);
                return held * env * env * 1.6f;
            });
            roar = Make("BossRoar", 1.2f, t =>
            {
                float pitch = Mathf.Lerp(74f, 46f, t / 1.2f) * (1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                float saw = 2f * Mathf.Repeat(pitch * t, 1f) - 1f;
                float n = (float)noise.NextDouble() * 2f - 1f;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.2f));
                return (saw * 0.6f + Mathf.Sin(2f * Mathf.PI * pitch * 0.5f * t) * 0.5f + n * 0.18f) * env;
            });
        }

        static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
        {
            int count = Mathf.RoundToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>The hum: on while it is awake.</summary>
        public void Live(bool on)
        {
            live = on;
            if (on && !hum.isPlaying) hum.Play();
        }

        public void SetAnger(int level) => anger = Mathf.Clamp(level, 0, 3);

        float Loudness => master * (0.6f + 0.13f * anger);

        public void Thump(float weight = 1f) => Shot(thump, Mathf.Clamp01(Loudness * weight), 1f - 0.05f * anger);
        public void Whoosh() => Shot(whoosh, Loudness * 0.7f, 1f + 0.1f * anger);
        public void Roar() => Shot(roar, Loudness, 1f + 0.08f * anger);

        void Shot(AudioClip clip, float volume, float pitch)
        {
            shots.pitch = pitch;
            shots.PlayOneShot(clip, volume);
        }

        void Update()
        {
            hum.volume = Mathf.MoveTowards(hum.volume, live ? master * (0.16f + 0.1f * anger) : 0f, Time.unscaledDeltaTime * 0.5f);
            hum.pitch = Mathf.Lerp(hum.pitch, 0.9f + 0.14f * anger, Time.unscaledDeltaTime * 2f);
            if (!live && hum.isPlaying && hum.volume <= 0f) hum.Stop();
        }
    }
}
