using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Energy bars sliding up and down inside the portal frame, plus a pulsing light.</summary>
    public class PortalFx : MonoBehaviour
    {
        public Transform[] bars;
        public float travel = 1.4f;
        public float speed = 0.9f;
        public Light glow;
        public float glowPulse = 0.25f;

        Vector3[] rest;
        float baseIntensity;

        void Start()
        {
            rest = new Vector3[bars.Length];
            for (int i = 0; i < bars.Length; i++) rest[i] = bars[i].localPosition;
            if (glow != null) baseIntensity = glow.intensity;
        }

        void Update()
        {
            for (int i = 0; i < bars.Length; i++)
            {
                float phase = Time.time * speed * (0.7f + 0.13f * i) + i * 1.7f;
                bars[i].localPosition = rest[i] + Vector3.up * (Mathf.Sin(phase) * travel);
                var s = bars[i].localScale;
                s.y = 0.5f + 0.35f * Mathf.Sin(phase * 1.3f + 1f);
                bars[i].localScale = s;
            }
            if (glow != null) glow.intensity = baseIntensity * (1f + glowPulse * Mathf.Sin(Time.time * 2.2f));
        }
    }
}
