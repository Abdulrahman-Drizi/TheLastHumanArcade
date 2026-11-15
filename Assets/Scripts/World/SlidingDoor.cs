using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The Monolith's main door: it shudders, then rises into the gatehouse.</summary>
    public class SlidingDoor : MonoBehaviour
    {
        public float rise = 7.4f;
        public float seconds = 2.6f;

        public bool IsOpen { get; private set; }

        public IEnumerator Open()
        {
            if (IsOpen) yield break;
            IsOpen = true;
            var from = transform.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                // seals break with a rattle, then it lifts smoothly
                float lift = Mathf.Clamp01((t - 0.18f) / 0.82f);
                lift = lift * lift * (3f - 2f * lift);
                float rattle = t < 0.22f ? Mathf.Sin(t * 180f) * 0.02f : 0f;
                transform.position = from + new Vector3(rattle, lift * rise, 0f);
                yield return null;
            }
            transform.position = from + Vector3.up * rise;
        }
    }
}
