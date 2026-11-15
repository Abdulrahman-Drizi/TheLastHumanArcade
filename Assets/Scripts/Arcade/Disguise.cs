using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The robot disguise. E puts it on; it holds for exactly five seconds, drops by itself, and
    /// then needs exactly five more before it can be used again. There is nothing else to it: no
    /// early removal, no battery to find. While it is on, patrols take the player for one of their
    /// own; the moment it drops, the player is a human in plain view again.
    /// Each change has its own sound and the HUD says the state in a word.
    /// </summary>
    public class Disguise : MonoBehaviour
    {
        public PlayerIdentity identity;
        public PlayerBody body;
        public FirstPersonController player;
        public ArcadeHud hud;
        public TransitionFx fx;
        public float activeSeconds = 5f;
        public float cooldownSeconds = 5f;

        /// <summary>Out of use and off the HUD (the boss fight, the ending).</summary>
        public bool Locked { get; set; }
        public bool Active => identity.IsRobot;
        public bool Ready => !Active && cooldown <= 0f;
        /// <summary>Seconds of disguise left (0 when it is off).</summary>
        public float Left => Active ? left : 0f;

        float left, cooldown;
        int ticked;

        void Update()
        {
            if (Locked)
            {
                if (Active) Drop(false);
                hud.ShowDisguise(false);
                return;
            }
            if (Active)
            {
                left -= Time.deltaTime;
                hud.SetDisguise(DisguiseState.Active, left / activeSeconds, Mathf.Max(0f, left));
                // three ticks count the last second and a half down
                int tick = left < 1.5f ? 3 - Mathf.FloorToInt(Mathf.Max(0f, left) / 0.5f) : 0;
                if (tick > ticked)
                {
                    ticked = tick;
                    Sfx.Play(Sound.MaskLow, 0.8f, 1f + 0.12f * tick);
                }
                if (left <= 0f) Drop(true);
            }
            else if (cooldown > 0f)
            {
                cooldown -= Time.deltaTime;
                hud.SetDisguise(DisguiseState.Cooling, 1f - cooldown / cooldownSeconds, Mathf.Max(0f, cooldown));
                if (cooldown <= 0f) Sfx.Play(Sound.MaskReady);
            }
            else
            {
                hud.SetDisguise(DisguiseState.Ready, 1f, 0f);
                if (GameInput.MaskPressed && player.ControlEnabled && !identity.Hidden) PutOn();
            }
        }

        void PutOn()
        {
            identity.SetForm(PlayerForm.Robot);
            body.ShowForm(PlayerForm.Robot);
            body.ShowInWorld(false);
            left = activeSeconds;
            ticked = 0;
            Sfx.Play(Sound.MaskOn);
        }

        void Drop(bool expired)
        {
            identity.SetForm(PlayerForm.Human);
            body.ShowForm(PlayerForm.Human);
            body.ShowInWorld(false);
            cooldown = cooldownSeconds;
            if (!expired) return;
            Sfx.Play(Sound.MaskOff);
            if (fx != null) fx.Play(0.22f);         // the picture tears for a moment as it fails: that, and no more
        }
    }
}
