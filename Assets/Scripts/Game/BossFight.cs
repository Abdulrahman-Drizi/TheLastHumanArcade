using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The fight for the kill switch.
    ///   - It opens with a tour of the hall on a moving camera: the colossus kneeling in the middle,
    ///     then each of the four corner batteries in turn, then the colossus getting to its feet.
    ///     The player is brought in through the gate first, so no doorway is ever in the way.
    ///   - Four batteries stand in the corners, an arrow hovering over each one still in place.
    ///     Each must be torn out (E held for two seconds), which needs the mask on; every one
    ///     removed makes the boss angrier: faster, louder, redder, with less time between attacks
    ///     and a tighter dodge.
    ///   - The boss slams the floor, sweeps its sword and leaps across the hall (see
    ///     BossController). The floor shows where each blow will land. If the player is inside it
    ///     when it is about to land, time slows and a dodge bar appears: SPACE inside the lit
    ///     window throws the player clear. A blow that lands on a player inside its zone always
    ///     costs a heart: the zone drawn on the floor is exactly the zone that hurts.
    ///   - Three hearts. At none the fight starts over: the player back at the gate, the boss calm,
    ///     all four batteries back in, the mask recharged.
    ///   - With all four gone the boss collapses (clear of the switch), says its last line, and
    ///     the kill switch's lever unlocks.
    /// </summary>
    public class BossFight : MonoBehaviour
    {
        public const string BossName = "THE COLOSSUS";
        public const string LastWords = "After all what we did to control humans, apparently humans always win.";

        public CityFlow flow;
        public HudController hud;
        public FirstPersonController player;
        public MaskController maskRules;
        public BossController boss;
        public BossAudio voice;
        public PowerBattery[] batteries;
        public KillSwitch killSwitch;
        public GameObject shield;               // the barrier round the kill switch
        public DodgePrompt dodge;
        public Shockwave[] waves;
        public Transform telegraph;             // a disc on the floor where a fist (or the whole boss) will land
        public Transform telegraphBody;         // a disc round its feet (the slam hurts there too)
        public Transform telegraphArc;          // a fan in front of it (the sword's sweep)
        public Transform arenaEntry;            // where the player stands when the fight starts (and restarts)
        public Transform bossHome;
        public Light[] arenaLamps;              // they redden as it angers
        public int maxHearts = 3;

        [Header("The opening tour")]
        public Camera flightCamera;
        public Camera[] playerCameras;

        [Header("Dodge: seconds of slowed time, and the lit window's share of the bar, by anger 0..3")]
        public float[] dodgeSeconds = { 1.5f, 1.25f, 1.05f, 0.85f };
        public float[] windowWidths = { 0.34f, 0.28f, 0.23f, 0.19f };
        public float dodgeLead = 0.5f;          // game seconds before impact that the prompt starts (always before the strike itself begins)
        public float keepClear = 8f;            // the fallen colossus lies at least this far back from the kill switch

        public bool Active { get; private set; }
        /// <summary>True during the slowed dodge moment (the pause menu stays out of the way).</summary>
        public bool InDodge { get; private set; }
        public int Hearts { get; private set; }
        public int Removed { get; private set; }

        public TransitionFx fx;                 // the picture's distortion when a blow lands

        /// <summary>The colossus is down for good.</summary>
        public bool Defeated { get; private set; }

        bool prompted, evaded, skipTour;
        float safeUntil, nextTargetCheck;
        PowerBattery target;
        Color[] lampColours;
        float[] lampLevels;

        static readonly string[] Taunts =
        {
            "One battery. I have three more, human.",
            "Stop that. Those are load-bearing.",
            "You will be RECYCLED!",
        };

        void Awake()
        {
            boss.Impact += OnImpact;
            boss.Began += OnAttackBegan;
            telegraph.gameObject.SetActive(false);
            telegraphBody.gameObject.SetActive(false);
            telegraphArc.gameObject.SetActive(false);
            Hearts = maxHearts;
            lampColours = new Color[arenaLamps.Length];
            lampLevels = new float[arenaLamps.Length];
            for (int i = 0; i < arenaLamps.Length; i++)
            {
                lampColours[i] = arenaLamps[i].color;
                lampLevels[i] = arenaLamps[i].intensity;
            }
        }

        // ---------------------------------------------------------------- the opening
        /// <summary>The gate has opened: the tour of the hall, the boss rises, and the fight starts.</summary>
        public IEnumerator Begin()
        {
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
            // through the gate first: wherever the player was standing to type the code, nothing of the doorway can block what follows
            yield return hud.Fade(0f, 1f, 0.25f);
            player.Teleport(arenaEntry.position, arenaEntry.eulerAngles.y);
            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            StartCoroutine(hud.Fade(1f, 0f, 0.5f));
            skipTour = false;

            var centre = bossHome.position;
            var entry = arenaEntry.position;
            var inward = (centre - entry).normalized;
            // 1. from just inside the gate, rising: the whole hall, the colossus on its knees in the middle
            hud.SetCaption("THE COLOSSUS GUARDS THE KILL SWITCH");
            Vector3 at = entry + Vector3.up * 2.2f, look = centre + Vector3.up * 2.6f;
            Aim(at, look, 62f);
            Vector3 high = entry + inward * 2.5f + Vector3.up * 6.2f;
            yield return Fly(at, look, high, centre + Vector3.up * 2.2f, 2.2f);
            at = high;
            look = centre + Vector3.up * 2.2f;

            // 2. round the hall, corner by corner: each battery in turn
            int[] order = { 0, 1, 3, 2 };
            for (int i = 0; i < order.Length && i < batteries.Length; i++)
            {
                var battery = batteries[order[i]].transform.position;
                var toCentre = centre - battery;
                toCentre.y = 0f;
                toCentre.Normalize();
                var side = Vector3.Cross(Vector3.up, toCentre);
                Vector3 nextAt = battery + toCentre * 6.2f + side * 1.6f + Vector3.up * 2.8f, nextLook = battery + Vector3.up * 1.3f;
                hud.SetCaption("POWER BATTERY " + (i + 1) + " OF 4");
                yield return Fly(at, look, nextAt, nextLook, 1.25f);
                at = nextAt;
                look = nextLook;
                // a slow push in on it
                yield return Fly(at, look, at + (nextLook - at).normalized * 1.2f, nextLook, 0.45f);
                at += (nextLook - at).normalized * 1.2f;
            }

            // 3. low in front of the colossus as it gets to its feet
            hud.SetCaption("RIP OUT ALL FOUR  -  ONLY ROBOT HANDS ARE STRONG ENOUGH");
            Vector3 low = centre - inward * 9.5f + Vector3.up * 1.5f;
            yield return Fly(at, look, low, centre + Vector3.up * 3.2f, 1.5f);
            boss.Wake();
            if (voice != null) { voice.Live(true); voice.Roar(); }
            hud.SetCaption(BossName + ":  No unit passes this gate. No human ever has.");
            for (float t = 0; t < 2.9f && !skipTour; t += Time.deltaTime)
            {
                // the camera backs off and shudders as it rises
                float shake = Mathf.Clamp01(1f - t / 2.4f) * 0.07f;
                var jitter = new Vector3(Mathf.PerlinNoise(t * 23f, 0f) - 0.5f, Mathf.PerlinNoise(0f, t * 27f) - 0.5f, 0f) * shake * 2f;
                Aim(low - inward * (t * 0.7f) + jitter, centre + Vector3.up * (3.2f + 0.5f * t), 62f);
                SkipCheck();
                yield return null;
            }

            // back behind the player's eyes, inside the hall, facing it
            hud.SetCaption(null);
            flightCamera.enabled = false;
            foreach (var c in playerCameras) c.enabled = true;
            hud.SetLetterbox(false, true);
            hud.SetGameplayVisible(true);
            player.LookAt(boss.transform.position + Vector3.up * 3f);
            Hearts = maxHearts;
            hud.ShowHearts(true);
            hud.SetHearts(Hearts);
            flow.ShowMission(Mission());
            player.ControlEnabled = true;
            Active = true;
            PointAtNearestBattery();
        }

        /// <summary>The camera glides from one view to another. Any "continue" key cuts the tour short.</summary>
        IEnumerator Fly(Vector3 fromAt, Vector3 fromLook, Vector3 toAt, Vector3 toLook, float seconds)
        {
            for (float t = 0; t < 1f && !skipTour; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                Aim(Vector3.Lerp(fromAt, toAt, e), Vector3.Lerp(fromLook, toLook, e), 62f);
                SkipCheck();
                yield return null;
            }
            Aim(toAt, toLook, 62f);
        }

        void SkipCheck()
        {
            if (GameInput.ContinuePressed) skipTour = true;
        }

        void Aim(Vector3 from, Vector3 at, float fieldOfView)
        {
            flightCamera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            flightCamera.fieldOfView = fieldOfView;
        }

        string Mission() => "Current Mission: Rip out the 4 corner batteries (" + Removed + "/4). Mask on, then hold E at each.";

        /// <summary>The waypoint arrow leads to the nearest battery still in place.</summary>
        void PointAtNearestBattery()
        {
            PowerBattery nearest = null;
            float best = float.MaxValue;
            foreach (var battery in batteries)
            {
                if (battery.Removed) continue;
                float d = (battery.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = battery; }
            }
            if (nearest != target)
            {
                target = nearest;
                hud.SetObjective(nearest != null ? nearest.transform : null, nearest != null ? "BATTERY" : null);
            }
        }

        // ---------------------------------------------------------------- the fight
        void Update()
        {
            Escalate();
            if (!Active) return;
            if (Time.time >= nextTargetCheck)
            {
                nextTargetCheck = Time.time + 0.4f;
                PointAtNearestBattery();
            }

            // the floor shows exactly where the coming blow will hurt, filling in as it winds up
            bool threat = boss.Threatening;
            var move = boss.Move;
            Show(telegraph, threat && move != BossController.Attack.Sweep);
            Show(telegraphBody, threat && move == BossController.Attack.Slam);
            Show(telegraphArc, threat && move == BossController.Attack.Sweep);
            if (threat)
            {
                float grow = Mathf.Lerp(0.3f, 1f, boss.WindUpProgress);
                if (move == BossController.Attack.Sweep)
                {
                    telegraphArc.SetPositionAndRotation(Floor(boss.transform.position, 0.05f), Quaternion.Euler(0f, boss.transform.eulerAngles.y, 0f));
                    telegraphArc.localScale = Vector3.one * (boss.swordReach * grow);
                }
                else
                {
                    float size = (move == BossController.Attack.Lunge ? boss.lungeRadius : boss.slamRadius) * 2f * grow;
                    telegraph.position = boss.StrikePoint + Vector3.up * 0.03f;
                    telegraph.localScale = new Vector3(size, 0.01f, size);
                    if (move == BossController.Attack.Slam)
                    {
                        telegraphBody.position = Floor(boss.transform.position, 0.04f);
                        telegraphBody.localScale = new Vector3(boss.bodyRadius * 2f, 0.01f, boss.bodyRadius * 2f);
                    }
                }
            }
            else if (!InDodge)
            {
                prompted = false;
                evaded = false;
            }

            if (threat && !prompted && boss.WindUpLeft <= dodgeLead && boss.DangerAt(player.transform.position))
            {
                prompted = true;
                StartCoroutine(DodgeMoment());
            }
        }

        static void Show(Transform mark, bool on)
        {
            if (mark.gameObject.activeSelf != on) mark.gameObject.SetActive(on);
        }

        Vector3 Floor(Vector3 point, float lift)
        {
            point.y = bossHome.position.y + lift;
            return point;
        }

        /// <summary>The hall itself answers its anger: the lamps go red and start to beat.</summary>
        void Escalate()
        {
            if (boss.Current == BossController.State.Dormant) return;
            float anger = Mathf.Clamp01(boss.Anger / 3f);
            bool dead = boss.Current == BossController.State.Dead;
            float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * (2.5f + 5.5f * anger));
            for (int i = 0; i < arenaLamps.Length; i++)
            {
                var red = new Color(1f, 0.16f, 0.1f);
                arenaLamps[i].color = Color.Lerp(arenaLamps[i].color, dead ? lampColours[i] : Color.Lerp(lampColours[i], red, anger), Time.deltaTime * 2f);
                arenaLamps[i].intensity = lampLevels[i] * (dead ? 1f : Mathf.Lerp(1f, 0.7f + 0.8f * beat, anger));
            }
        }

        void OnAttackBegan(BossController.Attack attack)
        {
            if (!Active || voice == null) return;
            if (attack == BossController.Attack.Lunge) voice.Roar();
            else voice.Whoosh();
        }

        /// <summary>Slow motion and the dodge bar, until the blow lands.</summary>
        IEnumerator DodgeMoment()
        {
            InDodge = true;
            evaded = false;
            int anger = Mathf.Clamp(boss.Anger, 0, dodgeSeconds.Length - 1);
            float seconds = dodgeSeconds[anger], width = windowWidths[anger];
            float windowEnd = 0.88f, windowStart = windowEnd - width;
            Time.timeScale = Mathf.Clamp(boss.WindUpLeft / seconds, 0.12f, 1f);       // the rest of the wind-up, stretched over 'seconds'
            dodge.Show(windowStart, windowEnd);
            bool pressed = false;
            for (float t = 0; t < seconds; t += Time.unscaledDeltaTime)
            {
                float p = t / seconds;
                dodge.SetProgress(p);
                if (!pressed && GameInput.DodgePressed)
                {
                    pressed = true;                       // one try only
                    evaded = p >= windowStart && p <= windowEnd;
                    dodge.Result(evaded);
                    if (evaded)
                    {
                        // thrown clear, sideways to the blow and away from it
                        var away = player.transform.position - boss.StrikePoint;
                        away.y = 0f;
                        var side = Vector3.Cross(Vector3.up, boss.transform.forward);
                        if (Vector3.Dot(side, away) < 0f) side = -side;
                        player.Evade((side + (away.sqrMagnitude > 0.01f ? away.normalized : -boss.transform.forward) * 0.5f).normalized);
                        safeUntil = Time.time + 1.0f;
                        break;
                    }
                }
                yield return null;
            }
            if (!pressed) dodge.Result(false);
            Time.timeScale = 1f;
            InDodge = false;
            yield return new WaitForSeconds(0.5f);
            dodge.Hide();
        }

        void OnImpact()
        {
            if (!Active) return;
            var move = boss.Move;
            var where = move == BossController.Attack.Sweep ? boss.transform.position + boss.transform.forward * (boss.swordReach * 0.5f) : boss.StrikePoint;
            if (move != BossController.Attack.Sweep)
                for (int i = 0; i < waves.Length; i++) waves[i].Play(Floor(where, 0.15f + i * 0.5f));
            // the whole hall jumps: a heavy shake (worse the nearer the blow) and the picture tears for a moment
            float near = Mathf.InverseLerp(26f, 4f, Vector3.Distance(player.transform.position, where));
            float weight = move == BossController.Attack.Lunge ? 1.25f : move == BossController.Attack.Sweep ? 0.6f : 1f;
            player.Shake(Mathf.Lerp(5f, 13f, near) * weight, Mathf.Lerp(0.6f, 1.0f, near));
            if (fx != null) fx.Play(Mathf.Lerp(0.45f, 0.75f, near));
            else hud.PlayGlitch(0.5f, 0.8f);
            if (voice != null) voice.Thump(weight);
            // whoever is standing inside the zone drawn on the floor is hit, unless they have just dodged
            if (evaded || Time.time < safeUntil || !boss.DangerAt(player.transform.position)) return;
            Hurt(where, move == BossController.Attack.Sweep ? 16f : 12f);
        }

        void Hurt(Vector3 from, float force)
        {
            Hearts--;
            hud.SetHearts(Hearts);
            var away = player.transform.position - from;
            away.y = 0f;
            player.Knockback((away.sqrMagnitude > 0.01f ? away.normalized : -boss.transform.forward) * force);
            player.Shake(7f, 0.7f);
            hud.PlayGlitch(0.5f, 0.8f);
            safeUntil = Time.time + 1.2f;
            if (Hearts <= 0) StartCoroutine(Beaten());
        }

        /// <summary>Out of hearts: the fight starts over from the beginning.</summary>
        IEnumerator Beaten()
        {
            Active = false;
            Show(telegraph, false);
            Show(telegraphBody, false);
            Show(telegraphArc, false);
            player.ControlEnabled = false;
            hud.ShowWarning("The colossus flattened you. Its batteries are back in: start again.", 3.2f);
            yield return hud.Fade(0f, 1f, 0.4f);
            Restart();
            yield return new WaitForSeconds(0.4f);
            yield return hud.Fade(1f, 0f, 0.5f);
            player.ControlEnabled = true;
            Active = true;
        }

        /// <summary>
        /// Start the fight over: player at the gate with full hearts and a recharged mask, the boss
        /// calm at its post, and all four batteries back in their sockets (also used by Restart Checkpoint).
        /// </summary>
        public void Restart()
        {
            Time.timeScale = 1f;
            InDodge = false;
            dodge.Hide();
            player.Teleport(arenaEntry.position, arenaEntry.eulerAngles.y);
            if (Defeated)                           // nothing left to fight: only the walk to the switch starts over
            {
                if (maskRules != null) maskRules.ResetReady();
                return;
            }
            foreach (var battery in batteries) battery.Restore();
            Removed = 0;
            boss.Calm();
            boss.ResetTo(bossHome.position, bossHome.eulerAngles.y);
            if (voice != null) voice.SetAnger(0);
            Hearts = maxHearts;
            hud.SetHearts(Hearts);
            if (maskRules != null) maskRules.ResetReady();
            flow.ShowMission(Mission());
            target = null;
            prompted = false;
            evaded = false;
            safeUntil = 0f;
        }

        /// <summary>Called by a battery once it is out.</summary>
        public void OnBatteryRemoved()
        {
            Removed++;
            flow.ShowMission(Mission());
            player.Shake(2.5f, 0.5f);
            hud.PlayGlitch(0.35f, 0.5f);
            if (Removed >= batteries.Length) StartCoroutine(Defeat());
            else
            {
                boss.Enrage();
                if (voice != null) { voice.SetAnger(boss.Anger); voice.Roar(); }
                StartCoroutine(hud.Say(BossName, Taunts[Mathf.Min(Removed - 1, Taunts.Length - 1)], 1.3f));
            }
        }

        IEnumerator Defeat()
        {
            Active = false;
            target = null;
            hud.SetObjective(null, null);
            Show(telegraph, false);
            Show(telegraphBody, false);
            Show(telegraphArc, false);
            dodge.Hide();
            Time.timeScale = 1f;
            Defeated = true;
            // wherever it was standing, it reels back toward the middle of its hall as it goes down, so that
            // its body never lies across the way to the kill switch or the ladder beside it
            var rest = boss.transform.position;
            var inward = bossHome.position - killSwitch.transform.position;
            inward.y = 0f;
            if (inward.sqrMagnitude > 0.01f)
            {
                inward.Normalize();
                float back = Vector3.Dot(rest - killSwitch.transform.position, inward);
                if (back < keepClear) rest += inward * (keepClear - back);
            }
            boss.Die(rest);
            if (voice != null) { voice.Thump(1.3f); voice.Live(false); }
            player.Shake(4f, 1.2f);
            yield return new WaitForSeconds(2.4f);
            yield return hud.Say(BossName, LastWords, 3.2f);
            hud.ShowHearts(false);
            // the floor is left clear: no danger marks, no dodge bar, the shield gone
            Show(telegraph, false);
            Show(telegraphBody, false);
            Show(telegraphArc, false);
            if (shield != null) shield.SetActive(false);
            killSwitch.Locked = false;
            flow.ShowMission("Current Mission: Pull the kill switch.");
            hud.SetObjective(killSwitch.transform, "KILL SWITCH");
        }

        void OnDestroy()
        {
            if (InDodge) Time.timeScale = 1f;
        }
    }
}
