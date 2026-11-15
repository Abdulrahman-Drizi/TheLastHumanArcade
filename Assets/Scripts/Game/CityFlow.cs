using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastHuman
{
    public enum CityStage { Arriving, FindCar, Drive, Entrance, Hacking, Inside, Elevator, FloorTen, Boss, Ladder, Complete }

    /// <summary>
    /// Runs the Robots City level:
    ///   1. arrive through the (now dead) portal: the loading card lifts on a flight from the
    ///      Monolith, past the car, down into the player's eyes;
    ///   2. reach the car on foot, masked, through crowded streets watched by security units;
    ///   3. drive to the Monolith Data Center and get out at the main entrance;
    ///   4. brute-force the door terminal (robot interface only): four timing checks, one miss throws the player back;
    ///   5. inside, cross the working floor to the elevator, masked, past managers and Human Detectors;
    ///   6. the elevator ride and the 10th floor (see <see cref="TowerSequence"/>), ending when the gate opens;
    ///   7. the fight for the kill switch (see <see cref="BossFight"/>), then its lever: pulling it sends the
    ///      player straight up the exit ladder to the roof, where the escape begins in its own scene.
    /// </summary>
    public class CityFlow : MonoBehaviour, ILevelFlow
    {
        public const string MainMission = "Main Mission: Activate the Kill Switch";
        public const string FindCarMission = "Current Mission: Find a car to reach the Monolith Data Center.";
        public const string DriveMission = "Current Mission: Drive to the Monolith Data Center.";
        public const string LeaveCarMission = "Current Mission: Leave the car at the main entrance.";
        public const string PasswordMission = "Current Mission: Break the password at the main entrance.";
        public const string ElevatorMission = "Current Mission: Go thhrough the elevator to the 10th floor";
        public const string FloorTenMission = "Current Mission: Find the gate password on the workstation.";

        public HudController hud;
        public LoadingScreen loading;
        public IntroCinematic intro;
        public CaptureDirector capture;
        public FirstPersonController player;
        public PlayerIdentity identity;
        public MaskController maskRules;
        public DetectionSystem detection;
        public Vehicle car;
        public PasswordTerminal terminal;
        public HackTerminal hack;
        public SlidingDoor door;
        public Shockwave[] shockwaves;
        public GameObject interior;         // everything that lives inside the facility; switched on when the door opens
        public Transform arrivalSpawn;      // in front of the dead portal: the first checkpoint
        public Transform insideSpawn;       // just inside the main door: the checkpoint once it is open
        public Transform entrance;          // the forecourt of the Monolith Data Center
        public Transform elevator;
        public TowerSequence tower;         // the elevator ride and the 10th floor
        public BossFight boss;              // the fight for the kill switch
        public ExitLadder ladder;
        public const string EscapeScene = "RooftopEscape";

        /// <summary>Esc opens the pause menu only while the player is actually playing (not in cutscenes or terminals).</summary>
        public bool CanPause => !busy && !hack.Open && !tower.desktop.Open && !tower.keypad.Busy && !boss.InDodge
                                && Stage != CityStage.Arriving && Stage != CityStage.Elevator && Stage != CityStage.Complete;

        public CityStage Stage { get; private set; }
        bool busy, announcedArrival;
        Transform checkpoint;

        IEnumerator Start()
        {
            detection.Caught += OnCaught;
            identity.FormChanged += hud.SetForm;
            hud.SetForm(identity.Form);
            car.Entered += OnCarEntered;
            car.Exited += OnCarExited;
            terminal.Opened += () => Stage = CityStage.Hacking;
            hack.Unlocked += OnUnlocked;
            hack.Kicked += OnKicked;
            checkpoint = arrivalSpawn;
            interior.SetActive(false);

            // the loading card from the last world is still up; it lifts on the establishing flight
            Stage = CityStage.Arriving;
            Pause(true);
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            hud.SetComms(MainMission, FindCarMission, false);
            StartCoroutine(loading.Reveal(1.0f, "ARRIVED  -  ROBOTS CITY"));
            yield return new WaitForSeconds(1.0f);
            yield return intro.Play();          // from the Monolith, past the car, down to the player
            hud.SetLetterbox(false);
            hud.SetGameplayVisible(true);
            maskRules.GrantReady();
            Pause(false);
            Stage = CityStage.FindCar;
            hud.SetComms(MainMission, FindCarMission);
            hud.SetObjective(car.transform, "CAR");
            hud.ShowWarning("Painted units are watching: orange security, blue police. The pale ones only get in your way.", 6f);
        }

        void Update()
        {
            hud.SetRobotStatus(detection.AnyChasing, detection.AnyWatching, identity.Hidden, identity.IsRobot && !identity.Exposed);
            hud.SetCaptureHint(player.ControlEnabled && !player.CursorCaptured && Stage != CityStage.Arriving);
            hud.SetStance(player.Stance);
            hud.ShowStance(!car.Driving);

            if (Stage == CityStage.Drive && car.AtDestination != announcedArrival)
            {
                announcedArrival = car.AtDestination;
                hud.SetComms(MainMission, announcedArrival ? LeaveCarMission : DriveMission);
            }
        }

        void Pause(bool paused)
        {
            busy = paused;
            player.ControlEnabled = !paused;
            detection.Paused = paused;
            maskRules.Paused = paused;
            if (paused) hud.SetPrompt(null);
        }

        // ---------------------------------------------------------------- the car
        void OnCarEntered()
        {
            Stage = CityStage.Drive;
            announcedArrival = false;
            hud.SetComms(MainMission, DriveMission);
            hud.SetObjective(entrance, "MONOLITH");
            hud.ShowDriveKeys(9f);
        }

        void OnCarExited()
        {
            Stage = CityStage.Entrance;
            car.Locked = true;
            // from here on, being caught by the police at the door means starting again beside the car
            var beside = new GameObject("ForecourtCheckpoint").transform;
            beside.SetPositionAndRotation(player.transform.position, player.transform.rotation);
            checkpoint = beside;
            hud.SetComms(MainMission, PasswordMission);
            hud.SetObjective(terminal.transform, "KEYPAD");
        }

        // ---------------------------------------------------------------- the door
        /// <summary>A timing check was missed: the door's defences throw the player back from it.</summary>
        void OnKicked()
        {
            Stage = CityStage.Entrance;
            var away = player.transform.position - door.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
            player.Knockback(away * 11f);                     // about three metres, over in half a second
            player.Shake(5f, 0.6f);
            var burst = new Vector3(door.transform.position.x, 0f, door.transform.position.z) + away * 1.2f;
            for (int i = 0; i < shockwaves.Length; i++) shockwaves[i].Play(burst + Vector3.up * (0.25f + i * 1.1f));
            hud.PlayGlitch(0.6f);
            hud.ShowWarning("Brute force failed: the door's defences threw you back.", 3f);
        }

        /// <summary>All four characters are in: the door opens on the working floor.</summary>
        void OnUnlocked()
        {
            Stage = CityStage.Inside;
            checkpoint = insideSpawn;
            interior.SetActive(true);
            StartCoroutine(door.Open());
            hud.SetComms(MainMission, ElevatorMission);
            hud.SetObjective(elevator, "ELEVATOR");
        }

        /// <summary>Called by the elevator when a masked player steps in: the ride begins.</summary>
        public void ReachElevator()
        {
            if (Stage != CityStage.Inside || busy) return;
            Stage = CityStage.Elevator;
            Pause(true);
            hud.HideWarning();
            detection.CallOff();
            StartCoroutine(tower.Ride());
        }

        /// <summary>The elevator doors have opened on the 10th floor.</summary>
        public void ArriveOnFloorTen()
        {
            Stage = CityStage.FloorTen;
            checkpoint = tower.carSpawn;
            Pause(false);
            hud.SetComms(MainMission, FloorTenMission);
            hud.SetObjective(tower.workstation.transform, "WORKSTATION");      // (which will send them to the Face Scan first)
        }

        /// <summary>Change the Current Mission line (the Main Mission stays).</summary>
        public void ShowMission(string mission) => hud.SetComms(MainMission, mission);

        /// <summary>The electric gate is open: the boss rises to block the way to the kill switch.</summary>
        public void BeginBossFight()
        {
            if (Stage != CityStage.FloorTen) return;
            Stage = CityStage.Boss;
            checkpoint = boss.arenaEntry;
            StartCoroutine(boss.Begin());
        }

        /// <summary>
        /// Called by the kill switch as its lever is pulled. Pulling it only arms it: the shutdown
        /// signal goes out from the roof, so nothing happens to the robots' world until the player
        /// is standing up there. The moment the lever is down the player is taken, with no further
        /// input, across to the exit ladder and up it through the hatch in one unbroken move; the
        /// dissolution itself starts on the rooftop (see EscapeFlow).
        /// </summary>
        public void ActivateKillSwitch(KillSwitch lever)
        {
            if (Stage != CityStage.Boss) return;
            Stage = CityStage.Ladder;
            StartCoroutine(PullAndClimb(lever));
        }

        IEnumerator PullAndClimb(KillSwitch lever)
        {
            Pause(true);                                    // no control and no pause menu from here to the roof
            Time.timeScale = 1f;
            hud.SetObjective(null, null);
            hud.HideWarning();
            player.StandUp();
            if (tower != null && tower.body != null) tower.body.Concealed = true;      // no limbs across the view on the ladder

            // 1. the lever comes down and lands
            yield return lever.Swing();
            player.Shake(5f, 0.5f);
            hud.PlayGlitch(0.7f);
            // from here it is a cinematic: black bars, the play HUD out of the picture, one line under it
            hud.SetGameplayVisible(false);
            hud.SetLetterbox(true);
            hud.SetCaption("KILL SWITCH ARMED  -  IT FIRES FROM THE ROOF");
            hud.SetComms(MainMission, "Current Mission: Kill switch pulled. Up the ladder to the roof.", false);
            yield return new WaitForSeconds(0.25f);

            // 2. across the back of the hall to the foot of the ladder, the view swinging round and up to it
            var eyes = player.cameraPivot;
            const float startTilt = 40f;                    // degrees the view is raised as the climb begins
            Vector3 from = player.transform.position, foot = ladder.foot.position;
            foot.y = from.y;
            var wall = ladder.foot.forward;                 // the way the climber faces: into the rungs
            // (a curve out from the wall and back in, round whatever stands beside the switch)
            var bend = Vector3.Lerp(from, foot, 0.8f) - wall * 3.1f;
            var footEyes = foot + Vector3.up * (eyes.position.y - from.y);
            Vector3 lookFrom = eyes.position + eyes.forward * 4f;
            Vector3 lookTo = footEyes + (wall * Mathf.Cos(startTilt * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(startTilt * Mathf.Deg2Rad)) * 3f;
            float run = Mathf.Clamp(Vector3.Distance(from, foot) / 5.5f, 0.7f, 1.7f);
            for (float t = 0; t < 1f; t += Time.deltaTime / run)
            {
                float e = t * t * (3f - 2f * t), u = 1f - e;
                player.SetPose(u * u * from + 2f * u * e * bend + e * e * foot, player.transform.eulerAngles.y);
                player.LookAt(Vector3.Lerp(lookFrom, lookTo, e));
                yield return null;
            }
            player.SetPose(foot, ladder.foot.eulerAngles.y);
            player.LookAt(lookTo);

            // 3. up the ladder hand over hand, eyes going up to the lit hatch; the picture goes dark as the head passes through it
            float height = ladder.top.position.y - foot.y;
            int rungs = Mathf.Max(1, Mathf.RoundToInt(height / ladder.rung));
            float seconds = rungs * ladder.secondsPerRung;
            var right = Vector3.Cross(Vector3.up, wall);
            bool fading = false;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float p = t / ladder.secondsPerRung;                    // rungs climbed so far
                float f = p - Mathf.Floor(p);
                float lift = Mathf.Floor(p) + Mathf.Lerp(f, f * f * (3f - 2f * f), 0.65f);     // every reach surges, none of them stops
                float rock = Mathf.Sin(p * Mathf.PI);                   // over with one hand, back with the other
                player.SetPose(foot + Vector3.up * Mathf.Min(height, lift * ladder.rung) + right * (0.035f * rock), ladder.foot.eulerAngles.y);
                player.ExtraRoll = 1.6f * rock;
                float k = Mathf.Clamp01(t / seconds);
                float tilt = Mathf.Lerp(startTilt, 76f, k * k) * Mathf.Deg2Rad;
                player.LookAt(eyes.position + wall * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt));
                if (!fading && seconds - t < 0.75f)
                {
                    fading = true;
                    StartCoroutine(hud.Fade(0f, 1f, 0.7f));
                }
                yield return null;
            }
            player.ExtraRoll = 0f;
            if (!fading) yield return hud.Fade(0f, 1f, 0.4f);
            else yield return new WaitForSeconds(0.1f);
            Stage = CityStage.Complete;
            SceneManager.LoadScene(EscapeScene);
        }

        /// <summary>The gate on the 10th floor is open: the end of the demo.</summary>
        public void Finish()
        {
            if (Stage == CityStage.Complete) return;
            Stage = CityStage.Complete;
            StartCoroutine(EndCard());
        }

        IEnumerator EndCard()
        {
            Pause(true);
            hud.SetObjective(null, null);
            hud.HideWarning();
            yield return hud.Fade(0f, 1f, 0.6f);
            FirstPersonController.CaptureCursor(false);
            hud.ShowComplete();
            yield return hud.Fade(1f, 0f, 0.4f);
        }

        public void LoadMenu() => SceneManager.LoadScene(GameFlow.MenuScene);
        public void Replay() => SceneManager.LoadScene(GameFlow.CityScene);

        // ---------------------------------------------------------------- back to the checkpoint
        /// <summary>From the pause menu: start again from the last checkpoint.</summary>
        public void RestartCheckpoint()
        {
            if (!CanPause) return;
            StartCoroutine(ReturnToCheckpoint(null));
        }

        void OnCaught()
        {
            bool onFoot = Stage == CityStage.FindCar || Stage == CityStage.Entrance || Stage == CityStage.Hacking || Stage == CityStage.Inside;
            if (busy || !onFoot) return;
            if (hack.Open) hack.Close();
            StartCoroutine(CaughtSequence());
        }

        IEnumerator ReturnToCheckpoint(string message)
        {
            Pause(true);
            yield return hud.Fade(0f, 1f, 0.35f);
            foreach (var spot in HideSpot.Spots) spot.Eject();
            maskRules.ResetReady();      // a fresh try starts with the mask charged
            detection.CallOff();
            if (car.Driving) car.Recall();                      // still at the wheel: the car goes back to where it was parked
            else if (Stage == CityStage.Boss) boss.Restart();   // the fight starts over from the gate (batteries already out stay out)
            else player.Teleport(checkpoint.position, checkpoint.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
            if (message != null) hud.ShowWarning(message);
            yield return hud.Fade(1f, 0f, 0.5f);
            Pause(false);
        }

        IEnumerator CaughtSequence()
        {
            Pause(true);
            var catcher = detection.Catcher;
            yield return capture.Play(catcher);          // the view snaps to its face as it reaches out
            hud.ShowWarning("Caught.");
            yield return hud.Fade(0f, 1f, 0.35f);
            capture.Restore(catcher);
            foreach (var spot in HideSpot.Spots) spot.Eject();
            maskRules.ResetReady();      // a fresh try starts with the mask charged
            detection.CallOff();
            player.Teleport(checkpoint.position, checkpoint.eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            hud.ShowWarning(Stage == CityStage.Inside ? "Security threw you back to the main door."
                          : Stage == CityStage.FindCar ? "A patrol dragged you back to the dead portal."
                          : "The police threw you back to your car.");
            yield return hud.Fade(1f, 0f, 0.5f);
            Pause(false);
        }
    }
}
