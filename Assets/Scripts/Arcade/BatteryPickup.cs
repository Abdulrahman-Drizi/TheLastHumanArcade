using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// One of the four batteries. It turns slowly on its stand with its core lit; walking into it
    /// picks it up (no key): it jumps to the player and is gone, and its light goes with it.
    /// </summary>
    public class BatteryPickup : MonoBehaviour
    {
        public ArcadeFlow flow;
        public Transform cell;              // the part that is taken
        public Light glow;
        public float reach = 1.25f;
        public float spin = 50f;

        public bool Taken { get; private set; }
        Vector3 rest;

        void Awake() => rest = cell.localPosition;

        void Update()
        {
            if (Taken) return;
            cell.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
            cell.localPosition = rest + Vector3.up * (0.06f * Mathf.Sin(Time.time * 2.2f));
            if (!flow.CanCollect) return;
            var gap = flow.player.transform.position - transform.position;
            gap.y = 0f;
            if (gap.magnitude < reach) StartCoroutine(Take());
        }

        IEnumerator Take()
        {
            Taken = true;
            flow.Collected(this);
            Sfx.Play(Sound.Pickup);
            var from = cell.position;
            var scale = cell.localScale;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.22f)
            {
                // up and in to the player's hands, shrinking as it goes
                var to = flow.player.cameraPivot.position + flow.player.cameraPivot.forward * 0.3f - Vector3.up * 0.35f;
                cell.position = Vector3.Lerp(from, to, t * t) + Vector3.up * (0.4f * Mathf.Sin(t * Mathf.PI));
                cell.localScale = scale * (1f - 0.75f * t);
                if (glow != null) glow.intensity *= 0.8f;
                yield return null;
            }
            cell.gameObject.SetActive(false);
            if (glow != null) glow.enabled = false;
        }
    }
}
