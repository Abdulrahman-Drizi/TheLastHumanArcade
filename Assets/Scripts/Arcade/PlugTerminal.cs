using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The power terminal behind the colossus, and the Plug of Malicious Data Center in it. It is
    /// caged until the colossus is down. F takes hold of the plug: it resists for a moment, then
    /// lets go with one heavy release and comes out in the player's hands, and the lamp over it
    /// dies. What happens to the rest of the building is the finale's business.
    /// </summary>
    public class PlugTerminal : Interactable
    {
        public ArcadeFlow flow;
        public Transform plug;                  // the plug itself: it comes out along its own -z
        public float pull = 0.7f;               // how far it comes
        public Light lamp;                      // the terminal's own light
        public Renderer[] liveParts;            // what glows while it has power
        public Material[] liveMaterials;
        public Material deadMaterial;
        public GameObject cage;                 // in the way until the colossus is down
        public Transform standPoint;

        public bool Locked { get; private set; } = true;
        public bool Pulled { get; private set; }
        bool busy;

        public override Vector3 Position => standPoint != null ? standPoint.position : transform.position;
        public override string Prompt => Locked || Pulled || busy ? null : "Pull the plug";

        /// <summary>The colossus is down: the cage drops out of the way.</summary>
        public void Unlock()
        {
            Locked = false;
            if (cage != null) StartCoroutine(DropCage());
        }

        IEnumerator DropCage()
        {
            var from = cage.transform.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
            {
                cage.transform.position = from + Vector3.down * (3.2f * t * t);
                yield return null;
            }
            cage.SetActive(false);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Locked || Pulled || busy) return;
            StartCoroutine(PullOut());
        }

        IEnumerator PullOut()
        {
            busy = true;
            flow.PlugGrabbed();
            var rest = plug.localPosition;
            var back = plug.localRotation * Vector3.back;
            // it takes the strain: a short, shaking half-inch
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                plug.localPosition = rest + back * (0.05f * t) + new Vector3(Mathf.Sin(Time.time * 90f), Mathf.Cos(Time.time * 77f), 0f) * (0.006f * t);
                yield return null;
            }
            // and lets go all at once
            Sfx.Play(Sound.Plug);
            flow.player.Shake(6f, 0.35f);
            if (lamp != null) lamp.enabled = false;
            foreach (var part in liveParts) Finale.Kill(part, liveMaterials, deadMaterial);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.16f)
            {
                float e = 1f - (1f - t) * (1f - t);
                plug.localPosition = rest + back * Mathf.Lerp(0.05f, pull, e);
                yield return null;
            }
            plug.localPosition = rest + back * pull;
            // it hangs from the hand, swinging down a little
            var hang = plug.localRotation;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.5f)
            {
                plug.localRotation = hang * Quaternion.Euler(22f * Mathf.Sin(t * Mathf.PI * 0.5f), 0f, 0f);
                yield return null;
            }
            Pulled = true;
            flow.PlugPulled();
        }
    }
}
