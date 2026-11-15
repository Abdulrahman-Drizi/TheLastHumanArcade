using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastHuman
{
    /// <summary>
    /// Runs the escape, a race against the kill switch's own work.
    ///   - The scene opens on the last rungs of the ladder from the kill switch's hall: the view
    ///     comes up out of the roof hatch, hand over hand, and steps out onto the Monolith's roof
    ///     garden (no input). Nothing has happened yet: the switch fires from up here, so the
    ///     cinematic (robots cowering under the words in the sky, the deletion wave rising behind
    ///     the Monolith) only starts once the player has walked out onto the roof.
    ///   - Then the wave comes south down the city, deleting it, and the player goes down the
    ///     parkour course ahead of it: zig-zagging over the forecourt and down the avenue (where
    ///     the robots are running about in blind panic), sweeping round over the junction and along
    ///     Arrival Street to the portal they came in by, which stays shut until the mask powers it
    ///     on at its console.
    ///   - A fall puts the player back at the last checkpoint, and the wave keeps coming. If the
    ///     wave reaches them, they are deleted: game over, with a retry from the last checkpoint.
    /// Each new move gets a tutorial card the first time, and the mask and Human 017 argue about it.
    /// </summary>
    public class EscapeFlow : MonoBehaviour, ILevelFlow
    {
        public const string MainMission = "Main Mission: Escape the Robots World";
        public const string RoofMission = "Current Mission: Walk out onto the roof. The kill switch fires from up here.";
        public const string DownMission = "Current Mission: Outrun the deletion wave. Parkour down to the portal.";

        public HudController hud;
        public FirstPersonController player;
        public ParkourMotor motor;
        public PlayerBody body;
        public PlayerIdentity identity;
        public TutorialCard card;
        public Transform start;                 // on the roof, a step from the hatch: the first place to come back to
        public Transform portal;

        [Header("Arriving: up the ladder and out of the roof hatch")]
        public Transform hatch;                 // the climber's feet as they reach roof level, facing the way they will step out
        public Transform hatchLid;              // stands open; drops shut (to its unturned rest) once the player is out
        public float climbDepth = 2.2f;         // how far down the ladder the view starts
        public float climbSeconds = 1.5f;       // up the rungs; stepping out takes another 0.7 s
        public Checkpoint[] checkpoints;        // in order down the course
        public float fallMargin = 6f;           // this far below the stage being crossed counts as a fall (the street below is no way round)

        [Header("The kill switch going off")]
        public DissolutionSequence dissolution;
        public DeletionWave wave;
        public PanicRunner[] runners;
        public float roofSteps = 3f;            // how far from the hatch counts as "out on the roof"
        public float headStart = 55f;           // how far behind a retry puts the wave

        [Header("The portal: shut and dark until the mask powers it up at its console")]
        public GameObject[] deadPortal;
        public GameObject[] livePortal;
        public int portalNoticeAt = 7;          // the checkpoint where the player is told how to open it
        public float portalLoadSeconds = 3.2f;
        public float consoleReach = 4.5f;       // walk further than this from the console and the link drops

        public bool CanPause => !busy;
        /// <summary>The console will take the mask's link now.</summary>
        public bool CanPowerPortal => racing && !busy && !portalOnline && !loading;
        bool busy, fired, racing, portalOnline, loading, portalTold;
        int reached = -1;
        Transform respawn;
        float fallBelow, nextGlitch;
        Coroutine talk;

        IEnumerator Start()
        {
            respawn = start;
            fallBelow = FloorAfter(start.position.y, 0);
            identity.SetForm(PlayerForm.Robot);         // the mask is what moves like this; it stays on now
            hud.SetForm(PlayerForm.Robot);
            body.Concealed = true;                      // nothing of the body crosses the view during slides and swings
            motor.Did += OnMove;
            foreach (var part in livePortal) part.SetActive(false);
            busy = true;
            player.ControlEnabled = false;
            hud.SetComms(MainMission, RoofMission, false);
            hud.SetObjective(null, null);
            // the climb from the control room ends here: up the last rungs and out of the hatch, in one go
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            yield return ClimbOut();
            // where are we? The camera starts at the dead portal on the ground and sweeps up over the city to the
            // figure standing on the Monolith's roof, so there is no doubt about it
            yield return dissolution.Arrive();
            player.ControlEnabled = true;
            busy = false;
        }

        /// <summary>
        /// The scene's first seconds: the view rises out of the roof hatch on the ladder, each rung
        /// a pull and a pause with the body swaying from hand to hand, then steps forward onto the
        /// roof as the lid drops shut behind. Nobody is at the controls; it ends on black, and the
        /// arrival shot fades in from that.
        /// </summary>
        IEnumerator ClimbOut()
        {
            if (hatch == null)
            {
                yield return hud.Fade(1f, 1f, 0.05f);
                yield break;
            }
            Vector3 top = hatch.position, bottom = top + Vector3.down * climbDepth;
            float yaw = hatch.eulerAngles.y;
            // looking up the ladder at the open hatch; the view comes level as the head clears the rim
            Vector3 rim = top + hatch.forward * 1.1f + Vector3.up * 2.3f;
            Vector3 garden = start.position + start.forward * 9f + Vector3.up * 1.65f;
            motor.Halt();
            player.Teleport(bottom, yaw);
            player.LookAt(rim);
            StartCoroutine(hud.Fade(1f, 0f, 0.7f));
            const int rungs = 5;
            // (the frame time is capped: the first frames after a scene load are long, and must not swallow the climb)
            for (float t = 0; t < 1f; t += Mathf.Min(Time.deltaTime, 0.05f) / climbSeconds)
            {
                float rung = t * rungs, whole = Mathf.Floor(rung), part = rung - whole;
                float lift = (whole + part * part * (3f - 2f * part)) / rungs;
                float sway = Mathf.Sin(rung * Mathf.PI) * 0.045f;
                motor.Halt();                   // (the motor would otherwise let the body drop between frames)
                player.SetPose(Vector3.Lerp(bottom, top, lift) + hatch.right * sway, yaw);
                player.LookAt(Vector3.Lerp(rim, garden, Mathf.Clamp01(lift * 1.7f - 0.7f)));
                yield return null;
            }
            // over the rim and forward onto the roof
            var lidOpen = hatchLid != null ? hatchLid.localRotation : Quaternion.identity;
            for (float t = 0; t < 1f; t += Mathf.Min(Time.deltaTime, 0.05f) / 0.7f)
            {
                float e = t * t * (3f - 2f * t);
                motor.Halt();
                player.SetPose(Vector3.Lerp(top, start.position, e) + Vector3.up * (Mathf.Sin(e * Mathf.PI) * 0.1f), yaw);
                player.LookAt(garden);
                if (hatchLid != null) hatchLid.localRotation = Quaternion.Slerp(lidOpen, Quaternion.identity, Mathf.Clamp01(t * 2f - 0.6f));
                yield return null;
            }
            if (hatchLid != null) hatchLid.localRotation = Quaternion.identity;
            motor.Halt();
            player.Teleport(start.position, start.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            yield return hud.Fade(0f, 1f, 0.3f);
        }

        void Update()
        {
            hud.SetCaptureHint(player.ControlEnabled && !player.CursorCaptured);
            if (busy) return;

            if (player.transform.position.y < fallBelow)
            {
                StartCoroutine(Fall());
                return;
            }
            // nothing happens until the player is physically out on the roof
            if (!fired)
            {
                var from = player.transform.position - start.position;
                from.y = 0f;
                if (from.magnitude >= roofSteps) StartCoroutine(Fire());
                return;
            }
            if (!racing) return;

            float lead = wave.U(player.transform.position) - wave.Front;
            wave.Tick(wave.U(player.transform.position), Time.deltaTime);
            hud.SetWave(Mathf.Max(0f, lead));
            if (lead < 18f && Time.time >= nextGlitch)          // the picture starts to tear as it closes in
            {
                nextGlitch = Time.time + 0.6f;
                hud.PlayGlitch(0.45f, Mathf.Lerp(0.7f, 0.25f, lead / 18f));
            }
            if (lead <= 0.5f) StartCoroutine(Deleted());
        }

        /// <summary>The player is on the roof: the kill switch goes off.</summary>
        IEnumerator Fire()
        {
            fired = true;
            busy = true;
            player.ControlEnabled = false;
            motor.Halt();
            hud.PlayGlitch(0.8f);
            player.Shake(4f, 0.8f);
            yield return new WaitForSeconds(0.6f);
            yield return dissolution.Play();
            foreach (var runner in runners) runner.Run();
            hud.SetComms(MainMission, DownMission);
            hud.SetObjective(portal, "PORTAL (OFFLINE)");
            hud.ShowNotice(DissolutionSequence.Unlock, 4f);
            player.ControlEnabled = true;
            busy = false;
            racing = true;
        }

        void OnMove(string move)
        {
            card.Done(move == "jump" ? TutorialKind.Jump : move == "slide" ? TutorialKind.Slide
                    : move == "wallrun" ? TutorialKind.WallRun : TutorialKind.Grapple);
        }

        /// <summary>Called by a checkpoint the player has run through.</summary>
        public void Reach(Checkpoint point)
        {
            if (point.order <= reached) return;
            reached = point.order;
            respawn = point.spawn;
            fallBelow = FloorAfter(point.spawn.position.y, System.Array.IndexOf(checkpoints, point) + 1);
            if (point.tutorial != TutorialKind.None) card.Show(point.tutorial);
            if (point.lines != null && point.lines.Length > 0) Say(point.lines);
            if (!portalOnline && !portalTold && point.order >= portalNoticeAt)
            {
                portalTold = true;
                hud.SetComms(MainMission, "Current Mission: The portal is shut. Reach its console and power it on with the mask.");
                hud.ShowNotice("THE PORTAL IS OFFLINE  -  THE MASK CAN POWER IT ON", 4.5f);
            }
        }

        /// <summary>E at the portal's console: the mask links to it and the portal loads.</summary>
        public void PowerPortal(PortalConsole console)
        {
            if (CanPowerPortal) StartCoroutine(LoadPortal(console.transform));
        }

        IEnumerator LoadPortal(Transform console)
        {
            loading = true;
            hud.PlayGlitch(0.3f, 0.4f);
            string[] steps = { "MASK LINK: HANDSHAKE", "MASK LINK: UNLOCKING THE PORTAL", "MASK LINK: POWERING IT ON", "MASK LINK: OPENING" };
            for (float t = 0; t < portalLoadSeconds; t += Time.deltaTime)
            {
                // walk away and the link is lost; the wave does not wait either way
                var gap = player.transform.position - console.position;
                gap.y = 0f;
                if (gap.magnitude > consoleReach || !racing)
                {
                    hud.SetAction(null, -1f);
                    if (racing) hud.ShowWarning("Link lost. Stay by the console while the portal loads.", 2.6f);
                    loading = false;
                    yield break;
                }
                float p = t / portalLoadSeconds;
                hud.SetAction(steps[Mathf.Min(steps.Length - 1, (int)(p * steps.Length))] + "   " + Mathf.RoundToInt(p * 100f) + "%", p);
                yield return null;
            }
            hud.SetAction(null, -1f);
            loading = false;
            WakePortal();
        }

        /// <summary>
        /// The height below which the player has fallen off the stage that starts at 'height' and
        /// ends at the checkpoint with this index: a margin under the lower of its two ends.
        /// (After the last checkpoint there is only the street, which cannot be fallen off.)
        /// </summary>
        float FloorAfter(float height, int nextIndex)
        {
            if (checkpoints == null || nextIndex < 0 || nextIndex >= checkpoints.Length) return -40f;
            return Mathf.Min(height, checkpoints[nextIndex].spawn.position.y) - fallMargin;
        }

        /// <summary>The portal the player came in by, dead all this time, comes back to life.</summary>
        void WakePortal()
        {
            portalOnline = true;
            foreach (var part in deadPortal) if (part != null) part.SetActive(false);
            foreach (var part in livePortal) part.SetActive(true);
            hud.ShowNotice("THE PORTAL IS BACK ONLINE", 4f);
            hud.SetComms(MainMission, "Current Mission: The portal is open. Go!");
            hud.SetObjective(portal, "PORTAL");
            hud.PlayGlitch(0.4f, 0.5f);
        }

        /// <summary>A short exchange: each entry is "SPEAKER|line", said one after another without stopping play.</summary>
        /// <remarks>
        /// An exchange already being said is never cut off (the one at the top of the course has to be
        /// heard whole): new lines wait their turn. If the player is outrunning the talk, the
        /// newcomers are simply dropped rather than left to pile up.
        /// </remarks>
        void Say(params string[] lines)
        {
            if (pending.Count > 1) return;
            pending.AddRange(lines);
            if (talk == null) talk = StartCoroutine(Talk());
        }

        readonly System.Collections.Generic.List<string> pending = new System.Collections.Generic.List<string>();

        IEnumerator Talk()
        {
            while (pending.Count > 0)
            {
                var entry = pending[0];
                int bar = entry.IndexOf('|');
                yield return hud.Say(bar > 0 ? entry.Substring(0, bar) : "", bar > 0 ? entry.Substring(bar + 1) : entry, 1.3f);
                pending.RemoveAt(0);
            }
            talk = null;
        }

        /// <summary>Fell off the course: back to the last checkpoint. The wave does not wait.</summary>
        IEnumerator Fall()
        {
            busy = true;
            player.ControlEnabled = false;
            yield return hud.Fade(0f, 1f, 0.18f);
            motor.Halt();
            player.Teleport(respawn.position, respawn.eulerAngles.y);
            yield return new WaitForSeconds(0.15f);
            yield return hud.Fade(1f, 0f, 0.25f);
            player.ControlEnabled = true;
            busy = false;
        }

        /// <summary>The wave has caught the player.</summary>
        IEnumerator Deleted()
        {
            busy = true;
            racing = false;
            player.ControlEnabled = false;
            hud.SetWave(-1f);
            hud.SetObjective(null, null);
            if (talk != null) { StopCoroutine(talk); talk = null; hud.HideSay(); }
            pending.Clear();
            hud.PlayGlitch(0.9f);
            player.Shake(6f, 0.8f);
            yield return hud.Fade(0f, 1f, 0.5f);
            motor.Halt();
            hud.SetCompleteText("DELETED", "The deletion wave caught you.", "You were erased along with the robots' world.");
            FirstPersonController.CaptureCursor(false);
            hud.ShowComplete();
            yield return hud.Fade(1f, 0f, 0.4f);
        }

        /// <summary>From the game-over card: back to the last checkpoint, with the wave pushed back behind it.</summary>
        public void Retry()
        {
            if (racing || !fired) return;
            StartCoroutine(RetryFromCheckpoint());
        }

        IEnumerator RetryFromCheckpoint()
        {
            yield return hud.Fade(0f, 1f, 0.3f);
            hud.completePanel.SetActive(false);
            FirstPersonController.CaptureCursor(true);
            wave.SetFront(wave.U(respawn.position) - headStart);
            foreach (var runner in runners) runner.Restore();
            motor.Halt();
            player.Teleport(respawn.position, respawn.eulerAngles.y);
            hud.SetObjective(portal, portalOnline ? "PORTAL" : "PORTAL (OFFLINE)");
            yield return new WaitForSeconds(0.2f);
            yield return hud.Fade(1f, 0f, 0.4f);
            player.ControlEnabled = true;
            busy = false;
            racing = true;
        }

        /// <summary>From the pause menu: back to the last checkpoint.</summary>
        public void RestartCheckpoint()
        {
            if (!busy) StartCoroutine(Fall());
        }

        public void LoadMenu() => SceneManager.LoadScene(GameFlow.MenuScene);

        /// <summary>Called by the portal: home, and the celebration.</summary>
        public void EnterPortal()
        {
            if (busy || !portalOnline) return;
            busy = true;
            racing = false;
            StartCoroutine(GoHome());
        }

        IEnumerator GoHome()
        {
            player.ControlEnabled = false;
            hud.SetWave(-1f);
            hud.SetObjective(null, null);
            hud.PlayGlitch(0.8f);
            yield return hud.Fade(0f, 1f, 0.8f);
            GameProgress.HeroReturns = true;
            SceneManager.LoadScene(GameFlow.GameScene);
        }
    }
}
