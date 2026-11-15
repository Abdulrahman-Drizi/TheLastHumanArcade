using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The mask. It is not in the level at the start: the feeder sends it down the belt by mistake
    /// after the last ore. It can only be taken while no robot has line of sight on the player.
    /// </summary>
    public class MaskPickup : Interactable
    {
        public DetectionSystem detection;
        public GameFlow flow;
        public HudController hud;
        public GameObject visual;
        public float facingYaw = 180f;     // which way the player faces to reach it (toward the belt)

        bool arrived, taken;

        public override string Prompt => "Pick up the mask";
        public override bool IsAvailable => arrived && !taken;

        void Awake()
        {
            if (visual != null) visual.SetActive(false);
        }

        /// <summary>Slides the mask along the belt from the feeder to where the player works.</summary>
        public IEnumerator Arrive(Vector3 from, float seconds)
        {
            var to = transform.position;
            transform.position = from;
            visual.SetActive(true);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                transform.position = Vector3.Lerp(from, to, t * t * (3 - 2 * t));
                yield return null;
            }
            transform.position = to;
        }

        /// <summary>Makes the mask takeable (after the story card that follows its arrival).</summary>
        public void Unlock() => arrived = true;

        public override void Interact(PlayerInteractor player)
        {
            if (!IsAvailable) return;
            if (detection != null && detection.AnyWatching)
            {
                hud.ShowWarning("You can't grab the mask. The robots are watching.");
                return;
            }
            taken = true;
            StartCoroutine(Take(player));
        }

        IEnumerator Take(PlayerInteractor player)
        {
            // the right hand reaches out, closes on the mask and lifts it to the face
            if (player != null && player.body != null) yield return player.body.Grab(transform, facingYaw);
            if (visual != null) visual.SetActive(false);
            flow.OnMaskTaken();
        }
    }
}
