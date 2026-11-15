using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Rules of the mask once the player owns it:
    ///   - worn, it lasts 10 seconds, and the disguise is complete until the battery reaches 0%
    ///     (for the last 3 seconds the battery reads low, which is a warning and nothing more);
    ///   - when it runs out it comes off by itself and needs 10 seconds before it can be worn again;
    ///   - holding E for 2 seconds takes it off early;
    ///   - Q (or a tap of E with nothing else to use) puts it back on once it is ready.
    /// </summary>
    public class MaskController : MonoBehaviour
    {
        public PlayerIdentity identity;
        public PlayerBody body;
        public PlayerInteractor interactor;
        public HudController hud;
        public TransitionFx fx;

        public float wearSeconds = 10f;
        public float warnAfter = 7f;          // seconds worn before the glitch warning starts
        public float cooldownSeconds = 10f;
        public float holdToRemove = 2f;

        public bool Owned { get; private set; }
        public bool Worn => identity.IsRobot;
        /// <summary>Stops the clocks (story cards, cutscenes).</summary>
        public bool Paused { get; set; }

        float timeLeft, cooldown, hold, scanLeft;
        bool warned, changing;

        /// <summary>The player now has the mask on for the first time: start the clock.</summary>
        public void Begin()
        {
            Owned = true;
            StartClock();
        }

        /// <summary>The player arrives already owning the mask, off and charged: E puts it on.</summary>
        public void GrantReady()
        {
            Owned = true;
            cooldown = 0f;
            hold = 0f;
        }

        /// <summary>
        /// An X-ray gate caught the masked player: robots can see the human underneath for a few
        /// seconds, exactly as when the mask is running out.
        /// </summary>
        public void Scanned(float seconds)
        {
            if (!Owned || !Worn || changing || scanLeft > 0f) return;
            seconds = Mathf.Min(seconds, timeLeft);
            scanLeft = seconds;
            identity.Exposed = true;
            fx.PlaySustained(seconds);
            hud.ShowWarning("X-ray scan: the human under the mask is showing.", seconds);
        }

        void StartClock()
        {
            timeLeft = wearSeconds;
            warned = false;
            scanLeft = 0f;
            hold = 0f;
            identity.Exposed = false;
        }

        void Update()
        {
            if (!Owned || Paused || changing)
            {
                if (!Owned || Paused) hud.SetHold(-1f);
                return;
            }

            if (Worn)
            {
                timeLeft -= Time.deltaTime;
                if (scanLeft > 0f)
                {
                    scanLeft -= Time.deltaTime;
                    if (scanLeft <= 0f)                     // the X-ray has worn off: the disguise is whole again
                    {
                        identity.Exposed = false;
                        fx.StopSustained();
                        hud.HideWarning();
                    }
                }
                // The disguise holds until the battery is flat. A low battery is only a warning: nothing sees
                // through the mask because of it, and the player is never caught for it.
                bool low = timeLeft <= wearSeconds - warnAfter;
                hud.SetMask("MASK  " + Mathf.CeilToInt(Mathf.Max(0f, timeLeft)), timeLeft / wearSeconds, low || identity.Exposed, false, identity.Exposed);
                if (!warned && low)
                {
                    warned = true;
                    if (scanLeft <= 0f) hud.ShowWarning("Mask battery low. It comes off at 0%.", 2.4f);
                }

                // hold E to take it off early
                // (not while E is busy on something in reach: a battery being ripped out, a door, a terminal)
                hold = GameInput.InteractHeld && interactor.Current == null && !interactor.HoldingAction ? hold + Time.deltaTime : 0f;
                hud.SetHold(hold > 0.15f ? hold / holdToRemove : -1f);
                if (hold >= holdToRemove || timeLeft <= 0f) StartCoroutine(TakeOff());
            }
            else
            {
                hud.SetHold(-1f);
                cooldown -= Time.deltaTime;
                if (cooldown > 0f)
                {
                    hud.SetMask("MASK RECHARGING  " + Mathf.CeilToInt(cooldown), 1f - cooldown / cooldownSeconds, false);
                }
                else
                {
                    hud.SetMask("MASK READY", 1f, false, true);      // the Q key cap is shown beside it
                    if (GameInput.MaskPressed || (GameInput.InteractPressed && interactor.Current == null)) StartCoroutine(PutOn());
                }
            }
        }

        IEnumerator TakeOff()
        {
            changing = true;
            hold = 0f;
            hud.SetHold(-1f);
            hud.HideWarning();
            fx.StopSustained();
            identity.Exposed = false;
            identity.SetForm(PlayerForm.Human);
            cooldown = cooldownSeconds;
            yield return body.Change(PlayerForm.Human, false);
            changing = false;
        }

        IEnumerator PutOn()
        {
            changing = true;
            identity.SetForm(PlayerForm.Robot);
            StartClock();
            yield return body.Change(PlayerForm.Robot, false);
            changing = false;
        }

        /// <summary>
        /// The player died (caught, flattened, restarted): the mask comes off and is at once fully
        /// charged and ready again. A fresh life never starts with a mask still cooling down.
        /// </summary>
        public void ResetReady()
        {
            if (!Owned) return;
            ForceOff();
            cooldown = 0f;
            timeLeft = wearSeconds;
            warned = false;
            scanLeft = 0f;
        }

        /// <summary>The mask is taken off the player by force (a scene strips it) and must recharge.</summary>
        public void ForceOff()
        {
            if (!Owned) return;
            StopAllCoroutines();
            changing = false;
            hud.HideWarning();
            fx.StopSustained();
            identity.Exposed = false;
            identity.SetForm(PlayerForm.Human);
            body.ShowForm(PlayerForm.Human);
            body.ShowInWorld(false);
            cooldown = cooldownSeconds;
            hold = 0f;
        }
    }
}
