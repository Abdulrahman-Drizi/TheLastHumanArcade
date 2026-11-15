using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TheLastHuman
{
    public enum Stage { Intro, Work, GetMask, Portal, Complete }

    /// <summary>
    /// Runs the round as a three-part story:
    ///   1. story card, flight over the yard, then the work shift (sort three ores);
    ///   2. the feeder sends a mask by mistake, story card, take it while unseen and become a robot;
    ///   3. story card with the mission, then reach the portal while the mask lasts, hiding while it recharges.
    /// </summary>
    public class GameFlow : MonoBehaviour, ILevelFlow
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Workplace";
        public const string CityScene = "RobotsCity";

        public const string StoryOne = "The AI Robots have taken over the world and stole human freedom. Now, all humans are working for the robots.";
        public const string StoryTwo = "Once upon a time, a human found a mask that brings hope.";
        public const string StoryThree = "Your mission is to enter the robots world via the portal and use the robot mask as stealth, and activate the kill switch in the \"Monolith Data Center\"";

        public HudController hud;
        public StoryScreen story;
        public LoadingScreen loading;
        public CaptureDirector capture;
        public VictoryCutscene victory;
        public IntroCinematic intro;
        public FirstPersonController player;
        public PlayerIdentity identity;
        public PlayerBody body;
        public MaskController maskRules;
        public DetectionSystem detection;
        public WorkStation workStation;
        public MaskPickup mask;
        public GameObject feederAlarm;      // red light on the feeder, on while the mask comes down the belt
        public GameObject portalBeacon;     // lit once the portal is the goal
        public Transform portalTarget;      // what the objective arrow points at in the last part
        public Transform stationSpawn;      // where a caught player is put back

        public Stage Stage { get; private set; }
        bool busy;

        IEnumerator Start()
        {
            workStation.Progress += OnWorkProgress;
            detection.Caught += OnCaught;
            identity.FormChanged += hud.SetForm;
            hud.SetForm(identity.Form);
            if (portalBeacon != null) portalBeacon.SetActive(false);
            if (feederAlarm != null) feederAlarm.SetActive(false);

            // back from the robots' world with the job done: this scene is the celebration, not the first shift
            if (GameProgress.HeroReturns && victory != null)
            {
                GameProgress.HeroReturns = false;
                Stage = Stage.Complete;
                Pause(true);
                story.gameObject.SetActive(false);
                yield return victory.Play();
                yield break;
            }

            // part one: story card, then the flight from the portal to the player's eyes
            Stage = Stage.Intro;
            Pause(true);
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            story.Cover();
            yield return story.Show(StoryOne);
            StartCoroutine(story.Hide());
            yield return intro.Play();
            hud.SetLetterbox(false);
            hud.SetGameplayVisible(true);
            Pause(false);
            SetStage(Stage.Work);
        }

        void Update()
        {
            hud.SetRobotStatus(detection.AnyChasing, detection.AnyWatching, identity.Hidden, identity.IsRobot && !identity.Exposed);
            hud.SetCaptureHint(player.ControlEnabled && !player.CursorCaptured);
            hud.SetStance(player.Stance);
        }

        /// <summary>Freeze the player and stop robots and the mask clock (story cards, cutscenes).</summary>
        void Pause(bool paused)
        {
            busy = paused;
            player.ControlEnabled = !paused;
            detection.Paused = paused;
            maskRules.Paused = paused;
            if (paused) hud.SetPrompt(null);
        }

        void SetStage(Stage stage)
        {
            Stage = stage;
            switch (stage)
            {
                case Stage.Work:
                    hud.SetObjective(workStation.workPoint, "ORE");
                    hud.SetComms("SHIFT 017", WorkLine(workStation.Done));
                    break;
                case Stage.GetMask:
                    mask.Unlock();
                    hud.SetObjective(mask.transform, "MASK");
                    hud.SetComms("FEEDER ERROR", "The feeder sent a mask by mistake. Pick it up when no robot is watching.");
                    break;
                case Stage.Portal:
                    hud.SetObjective(portalTarget, "PORTAL");
                    hud.SetComms("MASK ONLINE", "Reach the portal. The mask lasts 10 seconds: hide in a marked closet or under a crate while it recharges.");
                    if (portalBeacon != null) portalBeacon.SetActive(true);
                    break;
            }
        }

        string WorkLine(int done) => $"Sort ore at your station ({done}/{workStation.required}).";

        void OnWorkProgress(int done, int required)
        {
            if (Stage != Stage.Work) return;
            if (done >= required) StartCoroutine(MaskArrives());
            else hud.SetComms("SHIFT 017", WorkLine(done), false);
        }

        /// <summary>Part two opens: the fourth item on the belt is not ore.</summary>
        IEnumerator MaskArrives()
        {
            busy = true;
            hud.SetComms("SHIFT 017", WorkLine(workStation.required), false);
            if (feederAlarm != null) feederAlarm.SetActive(true);
            yield return mask.Arrive(workStation.feedPoint.position, 1.7f);
            hud.SetObjective(mask.transform, "MASK");       // the arrow moves to the mask the moment it arrives
            yield return new WaitForSeconds(1.1f);          // a moment to see what just arrived
            Pause(true);
            hud.SetGameplayVisible(false);
            yield return story.Show(StoryTwo);
            if (feederAlarm != null) feederAlarm.SetActive(false);
            hud.SetGameplayVisible(true);
            yield return story.Hide();
            Pause(false);
            SetStage(Stage.GetMask);
        }

        /// <summary>Called by the mask once the player has it in hand, unseen.</summary>
        public void OnMaskTaken()
        {
            if (Stage != Stage.GetMask) return;
            StartCoroutine(MaskSequence());
        }

        /// <summary>The change, then part three: the mission card. The mask's clock starts after the card.</summary>
        IEnumerator MaskSequence()
        {
            Pause(true);
            yield return body.Change(PlayerForm.Robot, true);
            identity.SetForm(PlayerForm.Robot);
            yield return new WaitForSeconds(0.2f);
            hud.SetGameplayVisible(false);
            yield return story.Show(StoryThree);
            hud.SetGameplayVisible(true);
            SetStage(Stage.Portal);
            yield return story.Hide();
            Pause(false);
            maskRules.Begin();
            workStation.KeepFeeding();      // the belt never stops: ore keeps coming for as long as the player stays
        }

        void OnCaught()
        {
            if (busy || Stage == Stage.Complete || Stage == Stage.Intro) return;
            StartCoroutine(CaughtSequence());
        }

        /// <summary>Esc opens the pause menu only while the player is actually playing.</summary>
        public bool CanPause => !busy && (Stage == Stage.Work || Stage == Stage.GetMask || Stage == Stage.Portal);

        /// <summary>From the pause menu: back to the station, as after being caught but without the robot.</summary>
        public void RestartCheckpoint()
        {
            if (CanPause) StartCoroutine(BackToStation());
        }

        IEnumerator BackToStation()
        {
            Pause(true);
            yield return hud.Fade(0f, 1f, 0.35f);
            foreach (var spot in HideSpot.Spots) spot.Eject();
            maskRules.ResetReady();      // a fresh try starts with the mask charged
            detection.CallOff();
            player.Teleport(stationSpawn.position, stationSpawn.eulerAngles.y);
            yield return new WaitForSeconds(0.3f);
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
            player.StandUp();
            player.Teleport(stationSpawn.position, stationSpawn.eulerAngles.y);
            yield return new WaitForSeconds(0.4f);
            hud.ShowWarning("A robot marched you back to your station.");
            yield return hud.Fade(1f, 0f, 0.5f);
            Pause(false);
        }

        /// <summary>Called by the portal when a masked player walks in: travel to the robots' world.</summary>
        public void CompleteRound()
        {
            if (Stage != Stage.Portal) return;
            Stage = Stage.Complete;
            hud.SetObjective(null, null);
            maskRules.Paused = true;
            detection.Paused = true;
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.HideWarning();
            StartCoroutine(Travel());
        }

        /// <summary>The picture breaks up, the loading card comes up with the objective, and Robots City loads behind it.</summary>
        IEnumerator Travel()
        {
            hud.PlayGlitch(0.9f);
            hud.SetGameplayVisible(false);
            yield return loading.Travel(CityScene, "ROBOTS CITY");
        }

        public void LoadMenu() => SceneManager.LoadScene(MenuScene);
        public void Replay() => SceneManager.LoadScene(GameScene);
    }
}
