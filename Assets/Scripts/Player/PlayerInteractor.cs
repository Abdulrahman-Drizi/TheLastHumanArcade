using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Finds the nearest usable thing in front of the player, shows its prompt, and uses it on E.
    /// Things that ask for it (a boss battery) must have E held down for a while: a bar fills, and
    /// letting go or turning away starts it over.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public Transform viewpoint;            // the camera
        public HudController hud;
        public FirstPersonController controller;
        public PlayerBody body;                // the figure whose hands do the reaching
        [Range(0f, 1f)] public float facing = 0.35f;   // how directly the player must face the target

        public Interactable Current { get; private set; }
        /// <summary>True while a held interaction is in progress (the mask's own hold-to-remove stands aside).</summary>
        public bool HoldingAction => held != null;

        Interactable held;
        float holdTime;

        void Update()
        {
            Current = (controller == null || controller.ControlEnabled) ? FindTarget() : null;
            // (while something is being held, its own small progress line says all there is to say)
            if (hud != null) hud.SetPrompt(Current != null && held == null ? Current.Prompt : null);

            // a hold in progress carries on only while E stays down and the same thing is still in reach
            if (held != null)
            {
                if (held != Current || !GameInput.InteractHeld) Drop();
                else
                {
                    holdTime += Time.deltaTime;
                    float progress = Mathf.Clamp01(holdTime / held.HoldSeconds);
                    held.Holding(progress);
                    if (hud != null) hud.SetAction(held.HoldLabel, progress);
                    if (progress >= 1f)
                    {
                        var done = held;
                        Drop();
                        done.Interact(this);
                    }
                }
                return;
            }

            if (Current == null || !GameInput.InteractPressed) return;
            if (Current.HoldSeconds <= 0f) Current.Interact(this);      // the target drives the hands
            else if (Current.BeginHold(this))
            {
                held = Current;
                holdTime = 0f;
            }
        }

        void Drop()
        {
            if (held != null) held.Holding(0f);
            held = null;
            holdTime = 0f;
            if (hud != null) hud.SetAction(null, -1f);
        }

        Interactable FindTarget()
        {
            Interactable best = null;
            float bestDistance = float.MaxValue;
            var origin = viewpoint.position;
            var forward = viewpoint.forward;
            forward.y = 0f;
            forward.Normalize();
            foreach (var it in Interactable.All)
            {
                if (!it.IsAvailable || it.Prompt == null) continue;
                var to = it.Position - origin;
                to.y = 0f;
                float d = to.magnitude;
                if (d > it.range || d >= bestDistance) continue;
                if (d > 0.6f && Vector3.Dot(forward, to / d) < facing) continue;
                best = it;
                bestDistance = d;
            }
            return best;
        }
    }
}
