using System;
using UnityEngine;

namespace TheLastHuman
{
    public enum Sound
    {
        MaskOn, MaskLow, MaskOff, MaskReady, Pickup, Snap, GateOpen, Suspect, Alarm, Caught,
        DodgeGood, Hit, Plug, PowerDown, Fanfare, Deny,
    }

    /// <summary>
    /// Every sound in the game, made from arithmetic when the scene starts: there are no audio
    /// files. Each one is short and says one thing: the disguise going on, running low, dropping
    /// and being ready again are four different sounds, so the mask can be played by ear.
    /// It also carries the low hum of the machinery, which the plug silences.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx I { get; private set; }
        [Range(0f, 1f)] public float master = 0.8f;

        const int Rate = 44100;
        AudioSource shots, hum;
        AudioClip[] clips;
        float humTarget, humPitch = 1f;
        readonly System.Random noise = new System.Random(11);

        float Noise() => (float)noise.NextDouble() * 2f - 1f;
        static float Sine(float hz, float t) => Mathf.Sin(2f * Mathf.PI * hz * t);
        /// <summary>A soft square: a sine pushed toward its corners.</summary>
        static float Buzz(float hz, float t) => Mathf.Clamp(Sine(hz, t) * 3f, -1f, 1f) * 0.6f;
        /// <summary>A plucked note: sharp start, quick fade.</summary>
        static float Pluck(float hz, float t, float start, float length) =>
            t < start || t > start + length ? 0f : Sine(hz, t - start) * Mathf.Exp(-(t - start) * 5f / length) * Mathf.Clamp01((t - start) * 400f);

        void Awake()
        {
            I = this;
            shots = gameObject.AddComponent<AudioSource>();
            shots.playOnAwake = false;
            shots.spatialBlend = 0f;
            hum = gameObject.AddComponent<AudioSource>();
            hum.playOnAwake = false;
            hum.loop = true;
            hum.spatialBlend = 0f;
            hum.volume = 0f;
            hum.clip = Make("MachineHum", 2f, t =>
                0.45f * Sine(50f, t) + 0.25f * Sine(100f, t) * (0.8f + 0.2f * Sine(0.5f, t)) + 0.1f * Sine(150.5f, t) + 0.05f * Noise());
            hum.Play();

            clips = new AudioClip[Enum.GetValues(typeof(Sound)).Length];
            // the disguise: up and on; three warning ticks; down and off; two clean pings when it is ready again
            Set(Sound.MaskOn, 0.22f, t => Buzz(Mathf.Lerp(220f, 660f, t / 0.16f), t) * Mathf.Clamp01(1f - t / 0.22f) * 0.8f + Noise() * 0.3f * Mathf.Exp(-t * 60f));
            Set(Sound.MaskLow, 0.07f, t => Sine(1250f, t) * Mathf.Exp(-t * 45f) * 0.6f);
            Set(Sound.MaskOff, 0.32f, t => Buzz(Mathf.Lerp(520f, 130f, t / 0.3f), t) * Mathf.Clamp01(1f - t / 0.32f) * 0.8f + Noise() * 0.25f * Mathf.Exp(-t * 18f));
            Set(Sound.MaskReady, 0.3f, t => (Pluck(880f, t, 0f, 0.12f) + Pluck(1320f, t, 0.11f, 0.18f)) * 0.6f);
            // batteries: a bright three-note pick-up, and a hard seat-and-click into the gate
            Set(Sound.Pickup, 0.34f, t => (Pluck(660f, t, 0f, 0.1f) + Pluck(990f, t, 0.07f, 0.1f) + Pluck(1320f, t, 0.14f, 0.2f)) * 0.55f);
            Set(Sound.Snap, 0.26f, t => Noise() * Mathf.Exp(-t * 90f) * 0.9f + Sine(Mathf.Lerp(170f, 95f, t / 0.2f), t) * Mathf.Exp(-t * 16f) * 0.9f + Sine(1700f, t) * Mathf.Exp(-t * 40f) * 0.25f);
            float held = 0f;
            Set(Sound.GateOpen, 1.9f, t =>
            {
                held += (Noise() - held) * 0.06f;
                float env = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((1.9f - t) * 2.5f);
                return (held * 1.6f + Sine(46f, t) * 0.5f + Sine(92f + 30f * t, t) * 0.18f) * env + Sine(120f, t) * Mathf.Exp(-(1.9f - t) * 14f) * 0.5f * Mathf.Clamp01(t - 1.5f) * 4f;
            });
            // the patrols: a questioning blip, then a hard double alarm
            Set(Sound.Suspect, 0.2f, t => Sine(Mathf.Lerp(620f, 930f, t / 0.18f), t) * Mathf.Clamp01(1f - t / 0.2f) * 0.5f);
            Set(Sound.Alarm, 0.3f, t => Buzz(980f, t) * ((t < 0.09f || (t > 0.14f && t < 0.26f)) ? 1f : 0f) * 0.75f);
            Set(Sound.Caught, 0.7f, t => (Buzz(Mathf.Lerp(210f, 62f, t / 0.7f), t) + Noise() * 0.2f) * Mathf.Clamp01(1f - t / 0.7f));
            // the colossus: a clean rising chime for a dodge, a dull blow for a hit
            Set(Sound.DodgeGood, 0.4f, t => (Pluck(990f, t, 0f, 0.12f) + Pluck(1480f, t, 0.08f, 0.3f)) * 0.6f + Noise() * 0.2f * Mathf.Exp(-t * 30f));
            Set(Sound.Hit, 0.5f, t => Sine(Mathf.Lerp(90f, 40f, t / 0.5f), t) * Mathf.Exp(-t * 7f) + Noise() * 0.6f * Mathf.Exp(-t * 26f));
            // the plug: a heavy release, a ring of metal, and after it the long fall of a machine winding down
            Set(Sound.Plug, 0.9f, t => Noise() * Mathf.Exp(-t * 50f) + Sine(Mathf.Lerp(110f, 55f, t / 0.4f), t) * Mathf.Exp(-t * 6f) + Sine(410f, t) * Mathf.Exp(-t * 5f) * 0.3f + Sine(1030f, t) * Mathf.Exp(-t * 9f) * 0.12f);
            Set(Sound.PowerDown, 2.4f, t => (Buzz(Mathf.Lerp(170f, 24f, Mathf.Pow(t / 2.4f, 0.7f)), t) * 0.7f + Sine(Mathf.Lerp(340f, 48f, t / 2.4f), t) * 0.3f) * Mathf.Clamp01((2.4f - t) * 1.2f) * (0.75f + 0.25f * Sine(7f, t)));
            Set(Sound.Fanfare, 1.6f, t => (Pluck(523f, t, 0f, 0.3f) + Pluck(659f, t, 0.16f, 0.3f) + Pluck(784f, t, 0.32f, 0.3f) + Pluck(1047f, t, 0.5f, 1.0f) + Pluck(784f, t, 0.5f, 1.0f) * 0.6f) * 0.5f);
            Set(Sound.Deny, 0.22f, t => Buzz(150f, t) * ((t < 0.08f || t > 0.12f) ? 1f : 0f) * Mathf.Clamp01(1f - t / 0.22f) * 0.6f);
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        void Set(Sound id, float seconds, Func<float, float> wave) => clips[(int)id] = Make(id.ToString(), seconds, wave);

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int count = Mathf.RoundToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate), -1f, 1f);
            // (a few milliseconds of fade at each end, so nothing clicks)
            int edge = Mathf.Min(120, count / 4);
            for (int i = 0; i < edge; i++)
            {
                data[i] *= i / (float)edge;
                data[count - 1 - i] *= i / (float)edge;
            }
            var clip = AudioClip.Create(name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Play one sound (safe to call before the scene has an Sfx, or without one).</summary>
        public static void Play(Sound id, float volume = 1f, float pitch = 1f)
        {
            if (I == null) return;
            I.shots.pitch = pitch;
            I.shots.PlayOneShot(I.clips[(int)id], volume * I.master);
        }

        /// <summary>How loud the machinery hums (0 to 1), and how high. It eases toward what is asked.</summary>
        public static void Hum(float level, float pitch = 1f)
        {
            if (I == null) return;
            I.humTarget = level;
            I.humPitch = pitch;
        }

        void Update()
        {
            hum.volume = Mathf.MoveTowards(hum.volume, humTarget * master * 0.5f, Time.unscaledDeltaTime * 0.35f);
            hum.pitch = Mathf.MoveTowards(hum.pitch, humPitch, Time.unscaledDeltaTime * 0.5f);
        }
    }
}
