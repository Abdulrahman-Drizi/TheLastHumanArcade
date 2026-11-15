using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastHuman
{
    public enum ArcadeStage { Yard, Fight, Plug, Ending, Lost }

    /// <summary>
    /// Runs the whole game, which is one scene and about a minute and a half long:
    ///   1. the yard: find the four batteries past the patrols (walk into one to take it) and seat
    ///      them in the security gate; E is the disguise, F hides and uses;
    ///   2. through the gate: the colossus, outlasted until its tokens run out (see TokenFight);
    ///   3. the plug, and the ending (see Finale).
    /// A patrol that reaches the player while they look human ends the run, and so does the
    /// colossus. Trying again is one key and takes no time; from the colossus on, a retry starts at
    /// the gate rather than back in the yard. There is no clock: the game is short because it is small.
    /// </summary>
    public class ArcadeFlow : MonoBehaviour, ILevelFlow
    {
        public HudController hud;
        public ArcadeHud arcadeHud;
        public FirstPersonController player;
        public PlayerIdentity identity;
        public DetectionSystem detection;
        public Disguise disguise;
        public Guide guide;
        public SecurityGate gate;
        public BatteryPickup[] batteries;
        public TokenFight fight;
        public PlugTerminal plug;
        public Finale finale;
        public Transform arenaEntry;            // where a retry of the fight starts, just inside the gate
        public float arenaLine;                 // z beyond which the player is in the colossus's hall

        public ArcadeStage Stage { get; private set; }
        /// <summary>Batteries in the player's hands.</summary>
        public int Carried { get; private set; }
        public bool CanCollect => Stage == ArcadeStage.Yard && player.ControlEnabled;
        public bool CanPause => Stage == ArcadeStage.Yard || Stage == ArcadeStage.Plug || (Stage == ArcadeStage.Fight && !fight.InDodge);

        const string Header = "OBJECTIVE";
        static bool startAtBoss;                // a retry after reaching the colossus starts there
        float lostAt, startedAt;
        bool taughtDisguise, taughtGate, gateAnnounced;
        Transform goalMarker;
        string goalName;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => startAtBoss = false;

        void Start()
        {
            Time.timeScale = 1f;
            detection.Caught += OnCaught;
            FirstPersonController.CaptureCursor(true);
            hud.SetLetterbox(false, true);
            hud.SetGameplayVisible(true, true);
            startedAt = Time.time;
            StartCoroutine(hud.Fade(1f, 0f, 0.5f));
            if (startAtBoss)
            {
                foreach (var b in batteries) b.gameObject.SetActive(false);
                gate.OpenAtOnce();
                player.Teleport(arenaEntry.position, arenaEntry.eulerAngles.y);
                EnterArena();
                return;
            }
            Stage = ArcadeStage.Yard;
            Sfx.Hum(0.1f);
            hud.SetComms(Header, "Find 4 batteries and power the gate.", false);
            arcadeHud.ShowBatteries(true);
            arcadeHud.SetBatteries(0, 0);
            arcadeHud.ShowTitle(3.2f);
            arcadeHud.Hint("WASD  MOVE     MOUSE  LOOK     SHIFT  RUN", 3.5f);
        }

        void Update()
        {
            if (Stage == ArcadeStage.Lost)
            {
                if (Time.unscaledTime - lostAt > 0.3f && (GameInput.RetryPressed || GameInput.ClickPressed)) Retry();
                return;
            }
            hud.SetCaptureHint(false);
            if (Stage != ArcadeStage.Yard)
            {
                arcadeHud.SetDanger(0);
                return;
            }

            // "?" while one is only looking (or searching where it lost the player), "!" once one is actually running at them
            bool running = false;
            foreach (var robot in RobotVision.All)
                if (robot.Patrol != null && robot.Patrol.Running) running = true;
            arcadeHud.SetDanger(running ? 2 : detection.AnyChasing || detection.AnyWatching ? 1 : 0);
            // the disguise is taught the moment it is first worth having: a patrol has just noticed the player,
            // or a few seconds have gone by
            if (!taughtDisguise && disguise.Ready && (detection.AnyWatching || Time.time - startedAt > 4f))
            {
                taughtDisguise = true;
                arcadeHud.Hint("E  ROBOT DISGUISE     5 SECONDS ON, 5 TO RECHARGE", 5f);
            }

            // where to next: the nearest battery still out there; with none left, the gate
            BatteryPickup nearest = null;
            float best = float.MaxValue;
            foreach (var b in batteries)
            {
                if (b.Taken) continue;
                float d = (b.transform.position - player.transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = b; }
            }
            if (gate.IsOpen) Aim(arenaEntry.position, "COLOSSUS");
            else if (nearest != null) Aim(nearest.transform.position, "BATTERY");
            else Aim(gate.Position, "GATE");

            if (gate.IsOpen && player.transform.position.z > arenaLine) EnterArena();
        }

        void Aim(Vector3 point, string name)
        {
            guide.PointTo(point);
            if (goalMarker == guide.marker && goalName == name) return;
            goalMarker = guide.marker;
            goalName = name;
            hud.SetObjective(guide.marker, name);
        }

        // ---------------------------------------------------------------- the yard
        public void Collected(BatteryPickup battery)
        {
            Carried++;
            arcadeHud.SetBatteries(Carried, gate.Inserted);
            int left = 0;
            foreach (var b in batteries) if (!b.Taken) left++;
            if (left == 0) hud.SetComms(Header, "Take the batteries to the gate.");
            if (!taughtGate)
            {
                taughtGate = true;
                arcadeHud.Hint("BATTERIES GO IN THE GATE  -  F AT THE GATE", 4.5f);
            }
        }

        /// <summary>The gate has seated one of the carried batteries ('total' are now in).</summary>
        public void Inserted(int total)
        {
            Carried = Mathf.Max(0, Carried - 1);
            arcadeHud.SetBatteries(Carried, total);
        }

        public void GateOpened()
        {
            hud.SetComms(Header, "The gate is open. Go through.");
        }

        // ---------------------------------------------------------------- the colossus
        void EnterArena()
        {
            if (Stage == ArcadeStage.Fight) return;
            Stage = ArcadeStage.Fight;
            startAtBoss = true;
            disguise.Locked = true;
            detection.Paused = true;
            detection.CallOff();
            arcadeHud.SetDanger(0);
            arcadeHud.ShowBatteries(false);
            arcadeHud.HideHint();
            hud.SetObjective(null, null);
            hud.SetComms(Header, "Dodge its slams until its tokens run out.");
            Sfx.Hum(0.32f, 1.05f);
            StartCoroutine(gate.Shut());
            StartCoroutine(fight.Begin());
        }

        public void BossDraining() => Sfx.Hum(0.16f, 0.85f);

        public void BossDown()
        {
            Stage = ArcadeStage.Plug;
            plug.Unlock();
            hud.SetComms(Header, "Pull the Plug of Malicious Data Center.");
            hud.SetObjective(plug.transform, "PLUG");
        }

        // ---------------------------------------------------------------- the plug
        public void PlugGrabbed()
        {
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
        }

        public void PlugPulled()
        {
            Stage = ArcadeStage.Ending;
            startAtBoss = false;
            StartCoroutine(finale.Play());
        }

        // ---------------------------------------------------------------- losing, and trying again
        void OnCaught()
        {
            if (Stage != ArcadeStage.Yard) return;
            if (detection.Catcher != null) player.LookAt(detection.Catcher.EyePosition);
            player.Shake(5f, 0.4f);
            Lose("CAUGHT", "The patrol was faster.");
        }

        public void Lose(string title, string body)
        {
            if (Stage == ArcadeStage.Lost || Stage == ArcadeStage.Ending) return;
            Stage = ArcadeStage.Lost;
            Time.timeScale = 1f;
            lostAt = Time.unscaledTime;
            player.ControlEnabled = false;
            detection.Paused = true;
            disguise.Locked = true;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
            arcadeHud.SetDanger(0);
            Sfx.Play(Sound.Caught);
            Sfx.Hum(0.04f, 0.8f);
            FirstPersonController.CaptureCursor(false);
            arcadeHud.ShowLoss(title, body);
        }

        /// <summary>Again, at once: from the yard, or from the gate if the colossus had been reached.</summary>
        public void Retry() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        /// <summary>The whole game again from the first second (the Replay button).</summary>
        public void Replay()
        {
            startAtBoss = false;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void RestartCheckpoint() => Retry();
        public void LoadMenu() => Replay();
    }
}
