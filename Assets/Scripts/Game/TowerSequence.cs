using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// Everything from the elevator doors to the open gate on the 10th floor.
    ///
    /// The ride: the hall doors part, the player walks in and rides up beside a worker robot. The
    /// mask gives out, the worker sees a human and warns them. On floor 6 the doors open, the worker
    /// steps out into a corridor full of police, their "?" marks start going off, and the doors slam
    /// just before they reach the car. The car carries on to floor 10.
    ///
    /// Floor 10: a small secure room behind an electric gate. E at the Face Scan holds the mask up
    /// to it, which powers the workstation; the player sits down to its desktop, opens the PASSWORDS
    /// folder and reads Password.txt: a 4-digit code. They then walk to the gate's keypad and type
    /// it in themselves; the gate lifts, and the colossus behind it wakes (see BossFight).
    ///
    /// The elevator car never really moves: lights rush past its window, the floor number counts
    /// up, and what lies beyond its doors is swapped while they are shut.
    /// </summary>
    public class TowerSequence : MonoBehaviour
    {
        public const string WorkerName = "WORKER UNIT 2231";

        [Header("Shared")]
        public CityFlow flow;
        public HudController hud;
        public FirstPersonController player;
        public PlayerBody body;
        public MaskController maskRules;
        public TransitionFx fx;

        [Header("The elevator doors in the hall")]
        public Transform hallLeafLeft;
        public Transform hallLeafRight;
        public Transform hallEntry;             // where the player ends up, just inside the doors

        [Header("The elevator car")]
        public Transform carSpawn;              // where the player stands, facing the doors
        public Transform carLeafLeft, carLeafRight;
        public float leafTravel = 0.86f;        // how far each leaf slides to open
        public Text floorDisplay;
        public Transform shaftBars;             // lights seen through the car's window; they rush past while it moves
        public float shaftSpan = 7f;
        public Transform worker;
        public RobotGesture workerGesture;
        public Animator workerAnimator;
        public AlertIcon workerIcon;
        public Transform workerExit;            // where the worker walks to on floor 6
        public Vector3[] workerPath;            // the way there: doorway, landing, then its place in the corridor
        public GameObject hallway;              // the police corridor outside the doors on floor 6
        public Transform[] police;
        public AlertIcon[] policeIcons;
        public GameObject floorTen;             // the secure room outside the doors on floor 10

        [Header("Floor ten")]
        public FaceScanTerminal faceScan;
        public Transform scanEye;               // the scanner's lens
        public Light scanLight;
        public Transform maskProp;              // the mask held up in front of the camera
        public Renderer workstationScreen;      // lit once the scan passes
        public DeskComputer workstation;        // the desk itself (E): locked until the face scan is passed
        public GameObject deniedScreen;         // the red refusal on its monitor
        public GameObject lockedScreen;         // what the monitor shows until the scan passes: locked, face scan required
        public Transform seat;                  // where the player sits (feet position, facing the screen)
        public Transform screenCentre;
        public DesktopUI desktop;
        public Transform standPoint;            // where the player stands to watch the gate
        public Text keypadDisplay;
        public Renderer[] keypadKeys;           // the lit faces of its keys
        public KeypadUI keypad;                 // the keypad on screen: the player types the code
        public GateKeypad gateKeypad;           // the post itself (E opens the keypad)
        public Transform keypadLook;
        public Transform gate;
        public float gateRise = 3.6f;
        public Light gateLight;
        public GameObject questMarker;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        Vector3 hallLeftRest, hallRightRest, carLeftRest, carRightRest;
        bool riding;

        void Awake()
        {
            hallLeftRest = hallLeafLeft.localPosition;
            hallRightRest = hallLeafRight.localPosition;
            carLeftRest = carLeafLeft.localPosition;
            carRightRest = carLeafRight.localPosition;
            desktop.Viewed += password => StartCoroutine(LeaveDesk());
            keypad.Submitted += OnCode;
        }

        bool deskBusy, gateOpen, denying;

        void Update()
        {
            if (!riding) return;
            // the shaft's lights fall past the window; each one wraps back to the top
            foreach (Transform bar in shaftBars)
            {
                var p = bar.localPosition;
                p.y -= 9f * Time.deltaTime;
                if (p.y < -shaftSpan / 2f) p.y += shaftSpan;
                bar.localPosition = p;
            }
        }

        // ---------------------------------------------------------------- the ride
        public IEnumerator Ride()
        {
            hud.SetObjective(null, null);
            hud.HideMask();                     // its clock is stopped for the ride; it comes back on floor 10

            // the doors part and the player walks in
            yield return Slide(hallLeafLeft, hallLeafRight, hallLeftRest, hallRightRest, 0f, 1.3f, 0.8f);
            yield return Walk(hallEntry.position, hallEntry.eulerAngles.y, 1.0f);
            yield return hud.Fade(0f, 1f, 0.35f);
            SetLeaves(hallLeafLeft, hallLeafRight, hallLeftRest, hallRightRest, 0f);

            // inside the car, beside the worker
            hallway.SetActive(false);
            floorTen.SetActive(false);
            SetLeaves(carLeafLeft, carLeafRight, carLeftRest, carRightRest, 0f);
            player.Teleport(carSpawn.position, carSpawn.eulerAngles.y);
            SetFloor(1);
            riding = true;
            yield return hud.Fade(1f, 0f, 0.5f);
            player.ControlEnabled = true;       // free to look round the car, feet planted
            player.MoveLocked = true;
            yield return CountTo(1, 3, 1.6f);
            yield return hud.Say(WorkerName, "Floor ten. That is a long way up for a courier unit.", 1.6f);

            // the mask gives out right beside it
            if (maskRules.Worn)
            {
                maskRules.ForceOff();
                fx.Play(0.8f);
            }
            yield return new WaitForSeconds(0.6f);
            yield return Face(worker, player.transform.position, 0.4f);
            workerIcon.Suspect();
            yield return new WaitForSeconds(0.7f);
            workerIcon.Alarm();
            workerGesture.Play(Gesture.Reach);                  // pointing straight at the player
            yield return hud.Say(WorkerName, "A human?! What are you doing in here? If the police units spot you, you're finished!", 2.4f);
            workerGesture.Play(Gesture.None);
            workerIcon.Hide();
            yield return hud.Say(WorkerName, "Stay back from the doors. Not a sound.", 1.4f);

            // floor 6: the doors open on a corridor full of police
            yield return CountTo(3, 6, 1.5f);
            riding = false;
            player.Shake(1.6f, 0.3f);
            hallway.SetActive(true);
            yield return Face(worker, worker.position + carSpawn.forward, 0.35f);
            yield return Slide(carLeafLeft, carLeafRight, carLeftRest, carRightRest, 0f, leafTravel, 0.7f);
            // out through the middle of the doorway and round into the corridor: never through a wall
            workerAnimator.SetFloat(SpeedId, 1f);
            if (workerPath != null && workerPath.Length > 0)
                foreach (var point in workerPath)
                {
                    StartCoroutine(Face(worker, point, 0.25f));
                    yield return Move(worker, point, Mathf.Max(0.25f, Vector3.Distance(worker.position, point) / 1.9f));
                }
            else yield return Move(worker, workerExit.position, 1.7f);
            workerAnimator.SetFloat(SpeedId, 0f);

            // one after another the police turn and wonder
            for (int i = 0; i < police.Length; i++)
            {
                StartCoroutine(Face(police[i], carSpawn.position, 0.3f));
                policeIcons[i].Suspect();
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.45f);
            for (int i = 0; i < police.Length && i < 3; i++)
            {
                policeIcons[i].Alarm();                           // the nearest ones are sure, and coming
                StartCoroutine(Move(police[i], police[i].position + (carSpawn.position - police[i].position).normalized * 1.1f, 0.45f));
            }
            yield return new WaitForSeconds(0.3f);

            // the doors slam shut in their faces
            yield return Slide(carLeafLeft, carLeafRight, carLeftRest, carRightRest, leafTravel, 0f, 0.14f);
            player.Shake(4.5f, 0.5f);
            hud.PlayGlitch(0.35f, 0.5f);
            yield return new WaitForSeconds(0.9f);
            hallway.SetActive(false);
            worker.gameObject.SetActive(false);
            riding = true;
            yield return hud.Say("HUMAN 017", "...That was too close.", 1.0f);
            yield return CountTo(6, 10, 2.4f);
            riding = false;
            player.Shake(1.6f, 0.3f);

            // floor 10
            floorTen.SetActive(true);
            yield return Slide(carLeafLeft, carLeafRight, carLeftRest, carRightRest, 0f, leafTravel, 0.8f);
            player.MoveLocked = false;
            flow.ArriveOnFloorTen();
        }

        // ---------------------------------------------------------------- floor ten
        public void BeginFaceScan() => StartCoroutine(FaceScan());

        IEnumerator FaceScan()
        {
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
            player.LookAt(scanEye.position);

            // the mask comes up into the scanner's beam
            Vector3 lowered = new Vector3(0.16f, -0.55f, 0.5f), raised = new Vector3(0.0f, -0.06f, 0.44f);
            maskProp.gameObject.SetActive(true);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.6f)
            {
                maskProp.localPosition = Vector3.Lerp(lowered, raised, t * t * (3f - 2f * t));
                yield return null;
            }
            maskProp.localPosition = raised;
            scanLight.enabled = true;
            hud.PlayGlitch(0.3f, 0.35f);
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                scanLight.intensity = 3f + 5f * Mathf.Abs(Mathf.Sin(t * 9f));      // the beam strobes over the mask
                yield return null;
            }
            scanLight.intensity = 2.5f;
            scanLight.color = new Color32(120, 255, 235, 255);
            workstationScreen.enabled = true;                                        // the workstation wakes
            if (lockedScreen != null) lockedScreen.SetActive(false);
            hud.ShowNotice("FACE SCAN PASSED  -  WORKSTATION UNLOCKED", 4f);          // said on screen as well, so it cannot be missed
            yield return hud.Say("FACE SCAN", "Unit 017 recognised. Workstation unlocked.", 1.0f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.4f)
            {
                maskProp.localPosition = Vector3.Lerp(raised, lowered, t * t);
                yield return null;
            }
            maskProp.gameObject.SetActive(false);

            // the scanner is one machine and the workstation another: the player walks over to it themselves
            Scanned = true;
            if (deniedScreen != null) deniedScreen.SetActive(false);
            flow.ShowMission("Current Mission: Workstation unlocked. Find the gate password on it.");
            hud.SetObjective(workstation.transform, "WORKSTATION");
            player.ControlEnabled = true;
        }

        /// <summary>The face scan has been passed: the workstation will let the player in.</summary>
        public bool Scanned { get; private set; }
        /// <summary>The desk can be used (or tried) now: nobody is at it, and the gate is still shut.</summary>
        public bool DeskFree => !deskBusy && !gateOpen && !desktop.Open && !keypad.Open && !denying;

        /// <summary>E at the workstation.</summary>
        public void UseWorkstation()
        {
            if (!DeskFree) return;
            if (Scanned) StartCoroutine(SitDown());
            else StartCoroutine(Deny());
        }

        /// <summary>Tried before the face scan: the screen throws the player out, loudly, and says what it wants.</summary>
        IEnumerator Deny()
        {
            denying = true;
            hud.ShowWarning("Access denied: Scan face first", 3.2f);
            hud.PlayGlitch(0.45f, 0.6f);
            player.Shake(2.2f, 0.4f);
            flow.ShowMission("Current Mission: Access denied. Pass the Face Scan, then come back to the workstation.");
            hud.SetObjective(faceScan.transform, "FACE SCAN");
            if (deniedScreen != null)
            {
                // the red refusal blinks on the monitor, then stays up until the scan is passed
                for (int i = 0; i < 6; i++)
                {
                    deniedScreen.SetActive(i % 2 == 0);
                    yield return new WaitForSeconds(0.12f);
                }
                deniedScreen.SetActive(true);
            }
            denying = false;
        }

        IEnumerator SitDown()
        {
            deskBusy = true;
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
            questMarker.SetActive(false);
            body.Concealed = true;
            yield return Walk(seat.position, seat.eulerAngles.y, 0.8f);
            player.EyeAdjust = -0.4f;
            player.LookAt(screenCentre.position);
            desktop.Show();
        }

        /// <summary>The player has read the code and closed the desktop: up from the desk, and over to them.</summary>
        IEnumerator LeaveDesk()
        {
            player.EyeAdjust = 0f;
            yield return Walk(standPoint.position, standPoint.eulerAngles.y, 0.9f);
            player.LookAt(keypadLook.position);
            body.Concealed = false;
            deskBusy = false;
            if (gateOpen) yield break;
            flow.ShowMission("Current Mission: Type the 4-digit code into the gate's keypad.");
            hud.SetObjective(keypadLook, "KEYPAD");
            player.ControlEnabled = true;
        }

        /// <summary>Four digits have gone into the keypad.</summary>
        void OnCode(string code)
        {
            if (!gateOpen && !string.IsNullOrEmpty(desktop.Password) && code == desktop.Password)
            {
                gateOpen = true;
                gateKeypad.Done = true;
                keypad.Accept();
                StartCoroutine(OpenGate());
            }
            else keypad.Deny();
        }

        IEnumerator OpenGate()
        {
            player.ControlEnabled = false;
            hud.SetPrompt(null);
            hud.SetObjective(null, null);
            flow.ShowMission("Current Mission: The gate is opening.");
            yield return new WaitForSeconds(0.9f);          // the keypad shows ACCESS GRANTED and goes away
            player.LookAt(gate.position + Vector3.up * 1.8f);
            gateLight.color = new Color32(120, 255, 235, 255);

            // the gate lifts
            var from = gate.position;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.9f)
            {
                gate.position = from + Vector3.up * (gateRise * t * t * (3f - 2f * t));
                yield return null;
            }
            gate.position = from + Vector3.up * gateRise;
            yield return new WaitForSeconds(0.5f);
            player.ControlEnabled = true;
            flow.BeginBossFight();              // something very large was waiting behind it
        }

        // ---------------------------------------------------------------- helpers
        void SetFloor(int floor) => floorDisplay.text = floor.ToString();

        IEnumerator CountTo(int from, int to, float seconds)
        {
            float each = seconds / Mathf.Max(1, to - from);
            for (int f = from + 1; f <= to; f++)
            {
                yield return new WaitForSeconds(each);
                SetFloor(f);
            }
        }

        static void SetLeaves(Transform left, Transform right, Vector3 leftRest, Vector3 rightRest, float open)
        {
            left.localPosition = leftRest + Vector3.left * open;
            right.localPosition = rightRest + Vector3.right * open;
        }

        static IEnumerator Slide(Transform left, Transform right, Vector3 leftRest, Vector3 rightRest, float from, float to, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                SetLeaves(left, right, leftRest, rightRest, Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
                yield return null;
            }
            SetLeaves(left, right, leftRest, rightRest, to);
        }

        /// <summary>Carry the player to a spot and a heading (they are not in control while this runs).</summary>
        IEnumerator Walk(Vector3 to, float yaw, float seconds)
        {
            var from = player.transform.position;
            float fromYaw = player.transform.eulerAngles.y;
            to.y = from.y;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                player.SetPose(Vector3.Lerp(from, to, e), Mathf.LerpAngle(fromYaw, yaw, e));
                yield return null;
            }
            player.SetPose(to, yaw);
        }

        static IEnumerator Move(Transform who, Vector3 to, float seconds)
        {
            var from = who.position;
            to.y = from.y;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                who.position = Vector3.Lerp(from, to, t);
                yield return null;
            }
            who.position = to;
        }

        static IEnumerator Face(Transform who, Vector3 target, float seconds)
        {
            var look = target - who.position;
            look.y = 0f;
            if (look.sqrMagnitude < 0.01f) yield break;
            var from = who.rotation;
            var to = Quaternion.LookRotation(look);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                who.rotation = Quaternion.Slerp(from, to, t);
                yield return null;
            }
            who.rotation = to;
        }
    }
}
