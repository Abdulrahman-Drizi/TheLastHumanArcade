using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// One of the four batteries that power the boss. Only robot strength can tear it out, so the
    /// player has to be wearing the mask (a human is told so), and it does not come easily: E has
    /// to be held for two seconds, standing still beside it, while the boss is still coming.
    /// While the fight is on, an arrow hovers over every battery that is still in place, so the
    /// four corners can be picked out from anywhere in the hall; it goes as the battery comes out.
    /// If the player loses the fight, every battery goes back in.
    /// </summary>
    public class PowerBattery : Interactable
    {
        public PlayerIdentity identity;
        public HudController hud;
        public BossFight fight;
        public Transform cell;              // the part that comes out
        public Light glow;
        public GameObject[] alsoOff;        // its socket and its cable across the floor: gone once the battery is out
        public GameObject marker;           // the arrow hovering over it during the fight
        public float holdSeconds = 2f;

        public bool Removed { get; private set; }
        bool busy;
        Vector3 rest, restScale;
        float glowRest;

        void Awake()
        {
            rest = cell.localPosition;
            restScale = cell.localScale;
            if (glow != null) glowRest = glow.intensity;
            if (marker != null) marker.SetActive(false);
        }

        void Update()
        {
            if (marker == null) return;
            bool show = fight != null && fight.Active && !Removed && !busy;
            if (marker.activeSelf != show) marker.SetActive(show);
        }

        public override string Prompt => Removed || busy || !fight.Active ? null : "Hold to rip out the battery";
        public override float HoldSeconds => holdSeconds;
        public override string HoldLabel => "Ripping out the battery...";

        public override bool BeginHold(PlayerInteractor player)
        {
            if (Removed || busy || !fight.Active) return false;
            if (!identity.IsRobot)
            {
                hud.ShowWarning("Human hands cannot shift it. Put the mask on.", 2.4f);
                return false;
            }
            return true;
        }

        /// <summary>While it is being pulled it lifts a little, rattles harder and its light flares.</summary>
        public override void Holding(float progress)
        {
            if (Removed || busy) return;
            float shake = 0.012f + 0.05f * progress;
            cell.localPosition = progress <= 0f ? rest
                : rest + new Vector3(Mathf.Sin(Time.time * 83f) * shake, 0.22f * progress, Mathf.Cos(Time.time * 71f) * shake);
            if (glow != null) glow.intensity = glowRest * (1f + 2.5f * progress * (0.6f + 0.4f * Mathf.Sin(Time.time * 40f)));
        }

        public override void Interact(PlayerInteractor player)
        {
            if (Removed || busy || !fight.Active || !identity.IsRobot) return;
            StartCoroutine(Pull());
        }

        IEnumerator Pull()
        {
            busy = true;
            if (marker != null) marker.SetActive(false);
            // it comes free and is thrown aside
            var from = cell.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                cell.localPosition = from + Vector3.up * (1.6f * t);
                cell.localScale = restScale * (1f - t);
                if (glow != null) glow.intensity = 4f * (1f - t);
                yield return null;
            }
            cell.gameObject.SetActive(false);
            if (glow != null) glow.enabled = false;
            if (alsoOff != null) foreach (var go in alsoOff) go.SetActive(false);
            Removed = true;
            busy = false;
            fight.OnBatteryRemoved();
        }

        /// <summary>Back in its socket, live again (the fight is starting over).</summary>
        public void Restore()
        {
            StopAllCoroutines();
            busy = false;
            Removed = false;
            cell.gameObject.SetActive(true);
            cell.localPosition = rest;
            cell.localScale = restScale;
            if (glow != null)
            {
                glow.enabled = true;
                glow.intensity = glowRest;
            }
            if (alsoOff != null) foreach (var go in alsoOff) go.SetActive(true);
        }
    }
}
