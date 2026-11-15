using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The Monolith's door terminal: a four-character password and a glitching BRUTE FORCE button.
    ///   - In human form the button is dead; pressing it shows why (only a robot interface can run the attack).
    ///   - With the mask on, the attack runs as four timing checks, one per character: a marker
    ///     sweeps a bar and must be stopped inside the lit zone in the middle. Each character is a
    ///     little faster with a little less room. A hit locks that character in.
    ///   - One miss throws the player out of the terminal (the level adds the knockback and shockwave).
    /// The mouse is free while the terminal is open. Q puts the mask on without leaving it.
    /// </summary>
    public class HackTerminal : MonoBehaviour
    {
        [Header("Panel")]
        public CanvasGroup group;
        public Text status;
        public Text error;
        public Text[] slots;
        public Image[] slotFrames;
        public RectTransform bruteRect;
        public Image bruteImage;
        public Text bruteLabel;
        public CanvasGroup barGroup;
        public RectTransform sweet;
        public RectTransform marker;
        public Text streamLeft, streamRight;

        [Header("Game")]
        public FirstPersonController player;
        public PlayerIdentity identity;
        public MaskController maskRules;
        public HudController hud;
        public string password = "H0P3";
        // Tuned to be demanding but readable: the marker moves at a steady speed, so the stop can be
        // timed by eye; the lit zone stays open for about 260, 200, 160 and 125 ms on the four stages.
        public float[] sweepsPerSecond = { 0.8f, 0.95f, 1.08f, 1.2f };     // bar lengths the marker covers each second
        public float[] windows = { 0.21f, 0.19f, 0.17f, 0.15f };           // width of the lit zone, as a share of the bar

        public Color live = new Color32(150, 240, 255, 255);
        public Color hot = new Color32(255, 70, 190, 255);
        public Color locked = new Color32(120, 255, 235, 255);
        public Color refused = new Color32(255, 84, 60, 255);
        public Color dead = new Color32(110, 130, 140, 255);

        public bool Open { get; private set; }
        public bool Cracked { get; private set; }
        public event Action Unlocked;      // all four characters are in
        public event Action Kicked;        // a timing check was missed

        const string Glyphs = "ABCDEFGHJKLMNPQRSTUVWXYZ0123456789";
        const string Hex = "0123456789ABCDEF";
        bool attacking, stopRequested;
        int lockedCount, stage;
        float openedAt, errorUntil, nextGlitch, nextStream, nextCycle;
        Vector2 bruteHome;

        UiFade fade;

        void Awake()
        {
            fade = UiFade.For(group);
            fade.slide = Vector2.zero;          // a whole screen: it only fades
            fade.seconds = 0.25f;
            fade.Show(false, true);
            group.blocksRaycasts = false;
            bruteHome = bruteRect.anchoredPosition;
        }

        public void Show()
        {
            if (Cracked) return;
            Open = true;
            openedAt = Time.unscaledTime;
            lockedCount = 0;
            attacking = false;
            errorUntil = 0f;
            player.ControlEnabled = false;                 // the keys and mouse belong to the terminal now
            FirstPersonController.CaptureCursor(false);
            hud.SetPrompt(null);
            fade.Show(true);
            group.blocksRaycasts = true;
            barGroup.alpha = 0f;
            Paint();
        }

        public void Close()
        {
            Open = false;
            attacking = false;
            StopAllCoroutines();
            maskRules.Paused = false;
            fade.Show(false);
            group.blocksRaycasts = false;
            player.ControlEnabled = true;
            FirstPersonController.CaptureCursor(true);
        }

        /// <summary>The BRUTE FORCE button (clicked, or B / Enter).</summary>
        public void OnBruteForce()
        {
            if (Open && attacking) { PressStop(); return; }      // during the attack the same button stops the marker
            if (!Open || attacking || Cracked) return;
            if (!identity.IsRobot)
            {
                errorUntil = Time.unscaledTime + 3.5f;
                error.text = "ERROR  -  ONLY A ROBOT INTERFACE CAN EXECUTE A BRUTE-FORCE ATTACK.\nPUT THE MASK ON  ( Q )  AND TRY AGAIN.";
                return;
            }
            StartCoroutine(Attack());
        }

        /// <summary>Stop the marker where it is (same as Space, E, Enter or a click).</summary>
        public void PressStop() => stopRequested = true;

        /// <summary>Where the marker is on the bar right now, 0 to 1 (the lit zone is centred on 0.5).</summary>
        public float MarkerPosition { get; private set; }

        IEnumerator Attack()
        {
            attacking = true;
            errorUntil = 0f;
            maskRules.Paused = true;                       // the terminal holds the link: the mask's clock waits
            barGroup.alpha = 1f;
            for (stage = 0; stage < password.Length; stage++)
            {
                float width = windows[Mathf.Min(stage, windows.Length - 1)];
                float speed = sweepsPerSecond[Mathf.Min(stage, sweepsPerSecond.Length - 1)];
                sweet.anchorMin = new Vector2(0.5f - width / 2f, 0f);
                sweet.anchorMax = new Vector2(0.5f + width / 2f, 1f);
                status.text = "CHARACTER " + (stage + 1) + " OF " + password.Length + "  -  STOP THE MARKER IN THE LIT ZONE";

                // the marker starts somewhere new each time, heading either way, but never on top of the lit zone
                float start = UnityEngine.Random.value < 0.5f ? UnityEngine.Random.Range(0f, 0.22f) : UnityEngine.Random.Range(0.78f, 1f);
                if (UnityEngine.Random.value < 0.5f) start = 2f - start;       // the same spot on the return leg of the sweep
                float t = 0f, position = 0f;
                bool stopped = false;
                while (!stopped)
                {
                    t += Time.deltaTime;
                    position = Mathf.PingPong(start + t * speed, 1f);
                    MarkerPosition = position;
                    marker.anchorMin = new Vector2(position, 0f);
                    marker.anchorMax = new Vector2(position, 1f);
                    // the first moments are a run-up: the press that started the attack (or locked the last character) does not count
                    if (t > 0.4f && (stopRequested || GameInput.ContinuePressed)) stopped = true;
                    stopRequested = false;
                    yield return null;
                }

                if (Mathf.Abs(position - 0.5f) > width / 2f)
                {
                    status.text = "INTRUSION DETECTED  -  LINK SEVERED";
                    status.color = refused;
                    yield return new WaitForSeconds(0.25f);
                    status.color = live;
                    Close();
                    Kicked?.Invoke();
                    yield break;
                }
                lockedCount = stage + 1;
                Paint();
                yield return new WaitForSeconds(0.35f);    // a beat to see the character lock in
            }

            barGroup.alpha = 0f;
            status.text = "ACCESS GRANTED";
            status.color = locked;
            Cracked = true;
            yield return new WaitForSeconds(0.9f);
            Close();
            Unlocked?.Invoke();
        }

        void Update()
        {
            if (!Open) return;
            var k = Keyboard.current;
            if (!attacking && k != null && Time.unscaledTime - openedAt > 0.25f)
            {
                if (k.escapeKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) { Close(); return; }
                if (k.bKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) OnBruteForce();
            }
            if (!attacking && !Cracked)
                status.text = identity.IsRobot ? "ROBOT INTERFACE DETECTED  -  BRUTE FORCE AVAILABLE" : "PASSWORD REQUIRED  -  HUMAN INTERFACE: NO ACCESS";
            error.enabled = Time.unscaledTime < errorUntil;
            Paint();
        }

        /// <summary>Everything that flickers: cycling characters, the code streams, the unstable button.</summary>
        void Paint()
        {
            bool robot = identity.IsRobot;
            float now = Time.unscaledTime;

            // password slots: locked ones hold their character, the rest spin through codes while the attack runs
            if (now >= nextCycle)
            {
                nextCycle = now + 0.045f;
                for (int i = 0; i < slots.Length; i++)
                {
                    bool isLocked = i < lockedCount;
                    slots[i].text = isLocked ? password[i].ToString()
                                  : attacking ? Glyphs[UnityEngine.Random.Range(0, Glyphs.Length)].ToString() : "?";
                    var colour = isLocked ? locked : attacking ? (i == stage ? live : new Color(live.r, live.g, live.b, 0.45f)) : dead;
                    slots[i].color = colour;
                    slotFrames[i].color = isLocked ? locked : attacking && i == stage ? hot : new Color(dead.r, dead.g, dead.b, 0.6f);
                }
            }

            // code streams down both sides: fast during the attack, a slow idle trickle otherwise
            if (now >= nextStream)
            {
                nextStream = now + (attacking ? 0.05f : 0.5f);
                streamLeft.text = Stream();
                streamRight.text = Stream();
                var c = attacking ? live : dead;
                c.a = attacking ? 0.8f : 0.35f;
                streamLeft.color = streamRight.color = c;
            }

            // the button: dead and grey for a human; live, jittering and flickering for a robot
            if (now >= nextGlitch)
            {
                nextGlitch = now + UnityEngine.Random.Range(0.05f, 0.16f);
                if (!robot || attacking)
                {
                    bruteRect.anchoredPosition = bruteHome;
                    bruteLabel.text = attacking ? "ATTACK RUNNING" : "BRUTE FORCE  [LOCKED]";
                    bruteLabel.color = attacking ? live : dead;
                    bruteImage.color = new Color(dead.r, dead.g, dead.b, attacking ? 0.5f : 0.35f);
                }
                else
                {
                    bool jump = UnityEngine.Random.value < 0.35f;
                    bruteRect.anchoredPosition = bruteHome + (jump ? new Vector2(UnityEngine.Random.Range(-5f, 5f), UnityEngine.Random.Range(-2f, 2f)) : Vector2.zero);
                    var text = "BRUTE FORCE".ToCharArray();
                    if (UnityEngine.Random.value < 0.4f) text[UnityEngine.Random.Range(0, text.Length)] = "#%7$0X@"[UnityEngine.Random.Range(0, 7)];
                    bruteLabel.text = new string(text);
                    bool flash = UnityEngine.Random.value < 0.3f;
                    bruteLabel.color = flash ? hot : live;
                    bruteImage.color = flash ? hot : live;
                }
            }
        }

        static string Stream()
        {
            var sb = new System.Text.StringBuilder(160);
            for (int line = 0; line < 9; line++)
            {
                for (int i = 0; i < 10; i++) sb.Append(Hex[UnityEngine.Random.Range(0, 16)]);
                if (line < 8) sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
