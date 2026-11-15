using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Ore that never stops coming: a handful of chunks ride the far lane of the conveyor from the
    /// feeder to the end stop, one after another, for the whole round. The same few objects are
    /// reused, so it costs nothing to keep running.
    /// </summary>
    public class BeltFlow : MonoBehaviour
    {
        public Transform[] chunks;
        public Vector3 from, to;            // where a chunk appears and where it drops out of sight
        public float tripSeconds = 13f;     // one chunk's ride along the belt

        Vector3[] sizes;

        void Awake()
        {
            sizes = new Vector3[chunks.Length];
            for (int i = 0; i < chunks.Length; i++) sizes[i] = chunks[i].localScale;
        }

        void Update()
        {
            for (int i = 0; i < chunks.Length; i++)
            {
                float u = Mathf.Repeat(Time.time / tripSeconds + (float)i / chunks.Length, 1f);
                chunks[i].position = Vector3.Lerp(from, to, u);
                // it grows out of the feeder and shrinks into the end stop, so nothing pops
                chunks[i].localScale = sizes[i] * Mathf.Clamp01(Mathf.Min(u, 1f - u) / 0.035f);
            }
        }
    }
}
