using UnityEngine;

namespace TheLastHuman
{
    /// <summary>A light that sputters like a welding torch: bursts of flashes with dark gaps between.</summary>
    [RequireComponent(typeof(Light))]
    public class Flicker : MonoBehaviour
    {
        public float low = 0.4f, high = 6f;
        public float burstSeconds = 1.6f, gapSeconds = 0.9f;

        Light lamp;
        float phase;

        void Awake()
        {
            lamp = GetComponent<Light>();
            phase = Random.value * 10f;
        }

        void Update()
        {
            float t = Mathf.Repeat(Time.time + phase, burstSeconds + gapSeconds);
            bool working = t < burstSeconds;
            lamp.intensity = working ? Mathf.Lerp(low, high, Mathf.PerlinNoise(Time.time * 38f, phase)) : low;
        }
    }
}
