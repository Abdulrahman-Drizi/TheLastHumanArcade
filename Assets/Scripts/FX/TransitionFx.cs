using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace TheLastHuman
{
    /// <summary>
    /// The screen glitch: a post-processing volume (colour split, lens warp, grain) is pushed in
    /// while the HUD draws sliding bars and dropout blocks, like the glitch in the menu video.
    /// Play() is a short burst for the change of form; PlaySustained() keeps it going as a warning.
    /// </summary>
    public class TransitionFx : MonoBehaviour
    {
        public Volume volume;        // heavy chromatic split / distortion profile, weight 0 at rest
        public HudController hud;
        public Light flash;          // amber light near the hands

        Coroutine burst, sustained;

        public void Play(float seconds)
        {
            if (burst != null) StopCoroutine(burst);
            burst = StartCoroutine(Burst(seconds));
        }

        /// <summary>Keeps glitching for the given time (the mask is failing).</summary>
        public void PlaySustained(float seconds)
        {
            StopSustained();
            sustained = StartCoroutine(Sustain(seconds));
        }

        public void StopSustained()
        {
            if (sustained == null) return;
            StopCoroutine(sustained);
            sustained = null;
            if (burst == null && volume != null) volume.weight = 0f;
        }

        IEnumerator Burst(float seconds)
        {
            if (hud != null) hud.PlayGlitch(seconds);
            float peak = flash != null ? flash.intensity : 0f;
            if (flash != null) flash.enabled = true;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                // sharp hit at contact, a second swell as the plates lock, then clear
                float w = Mathf.Max(Mathf.Exp(-t * 9f), Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.75f);
                if (volume != null) volume.weight = w * (0.75f + 0.25f * Mathf.PerlinNoise(t * 40f, 0f));
                if (flash != null) flash.intensity = peak * w;
                yield return null;
            }
            if (volume != null) volume.weight = 0f;
            if (flash != null)
            {
                flash.intensity = peak;
                flash.enabled = false;
            }
            burst = null;
        }

        IEnumerator Sustain(float seconds)
        {
            if (hud != null) hud.PlayGlitch(seconds, 0.55f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                // grows stronger as time runs out, with a nervous flicker
                float w = Mathf.Lerp(0.3f, 0.7f, t / seconds) * (0.6f + 0.4f * Mathf.PerlinNoise(t * 9f, 3f));
                if (burst == null && volume != null) volume.weight = w;
                yield return null;
            }
            if (burst == null && volume != null) volume.weight = 0f;
            sustained = null;
        }
    }
}
