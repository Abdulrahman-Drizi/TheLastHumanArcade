using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastHuman
{
    public enum HideKind { Closet, Box }

    /// <summary>
    /// A place to hide from the robots. Both kinds are used with E, and left with E:
    ///   Closet: step in, the door closes and you watch through the slot.
    ///   Box:    kneel beside the crate and slide into the low gap underneath; look around from
    ///           there and crawl out on the side you are facing.
    /// Hiding only works if no robot is looking at you as you go in: one that watched you hide keeps coming.
    /// </summary>
    public class HideSpot : Interactable
    {
        public HideKind kind;
        public FirstPersonController controller;
        public PlayerIdentity identity;
        public DetectionSystem detection;
        public HudController hud;

        [Header("Closet")]
        public Transform insidePoint;      // where the player stands, facing out
        public Transform exitPoint;
        public Transform door;
        public float doorOpenAngle = -105f;

        [Header("Box")]
        public float sideDistance = 1.3f;  // from the middle of the box to where the player kneels beside it
        public float kneelSeconds = 0.4f;  // getting down beside the box
        public float slideSeconds = 0.55f; // sliding under it, or back out

        public static readonly List<HideSpot> Spots = new List<HideSpot>();

        /// <summary>The hiding spot the player is in right now (seen going in or not), if any.</summary>
        public static HideSpot Occupied
        {
            get
            {
                foreach (var spot in Spots)
                    if (spot.inside) return spot;
                return null;
            }
        }

        bool inside, busy;
        Vector3 entrySide = Vector3.forward;   // which side of the box the player went in by (world direction)

        public override string Prompt =>
            busy ? null
            : kind == HideKind.Closet ? (inside ? "Leave the closet" : "Hide in the closet")
            : (inside ? "Crawl out" : "Hide under the crate");

        protected override void OnEnable()
        {
            base.OnEnable();
            Spots.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            Spots.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSpots() => Spots.Clear();

        void Start()
        {
            if (door != null) door.localRotation = Quaternion.Euler(0f, doorOpenAngle, 0f);
        }

        public override void Interact(PlayerInteractor player)
        {
            if (busy) return;
            if (kind == HideKind.Closet) StartCoroutine(inside ? Leave() : Enter());
            else StartCoroutine(inside ? CrawlOut() : TuckIn());
        }

        // ---------------------------------------------------------------- closet
        IEnumerator Enter()
        {
            busy = true;
            bool seen = detection.AnyWatching;
            controller.StandUp();
            controller.MoveLocked = true;
            controller.SetPose(insidePoint.position, insidePoint.eulerAngles.y);
            controller.EyeAdjust = -0.1f;          // level with the slot in the door
            inside = true;
            yield return SwingDoor(doorOpenAngle, 0f);
            identity.SetHidden(this, !seen);
            if (seen) hud.ShowWarning("A robot saw you hide.");
            busy = false;
        }

        IEnumerator Leave()
        {
            busy = true;
            identity.SetHidden(this, false);
            yield return SwingDoor(0f, doorOpenAngle);
            controller.EyeAdjust = 0f;
            controller.SetPose(exitPoint.position, exitPoint.eulerAngles.y);
            controller.MoveLocked = false;
            inside = false;
            busy = false;
        }

        IEnumerator SwingDoor(float from, float to)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                door.localRotation = Quaternion.Euler(0f, Mathf.Lerp(from, to, t * t * (3f - 2f * t)), 0f);
                yield return null;
            }
            door.localRotation = Quaternion.Euler(0f, to, 0f);
        }

        // ---------------------------------------------------------------- box
        /// <summary>Kneel at the nearest side of the crate, then slide underneath. The player keeps control of where they look.</summary>
        IEnumerator TuckIn()
        {
            busy = true;
            inside = true;
            bool seen = detection.AnyWatching;
            controller.MoveLocked = true;
            entrySide = NearestSide(controller.transform.position);
            controller.Tuck();                                   // the eyes start coming down at once
            yield return Slide(controller.transform.position, SidePoint(entrySide), kneelSeconds);
            yield return Slide(SidePoint(entrySide), Floor(transform.position), slideSeconds);
            identity.SetHidden(this, !seen);
            if (seen) hud.ShowWarning("A robot saw you hide.");
            busy = false;
        }

        /// <summary>Slide out on the side the player is facing (if there is room to stand there), then get up.</summary>
        IEnumerator CrawlOut()
        {
            busy = true;
            identity.SetHidden(this, false);
            var side = ExitSide();
            yield return Slide(controller.transform.position, SidePoint(side), slideSeconds);
            controller.StandUp();
            yield return new WaitForSeconds(0.25f);              // on the way up before the feet are free again
            controller.MoveLocked = false;
            inside = false;
            busy = false;
        }

        IEnumerator Slide(Vector3 from, Vector3 to, float seconds)
        {
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                controller.SetPose(Vector3.Lerp(from, to, e), controller.transform.eulerAngles.y);
                yield return null;
            }
            controller.SetPose(to, controller.transform.eulerAngles.y);
        }

        Vector3 Floor(Vector3 p) => new Vector3(p.x, controller.transform.position.y, p.z);

        Vector3 SidePoint(Vector3 side) => Floor(transform.position + side * sideDistance);

        Vector3[] Sides() => new[] { transform.forward, -transform.forward, transform.right, -transform.right };

        Vector3 NearestSide(Vector3 from)
        {
            var to = from - transform.position;
            to.y = 0f;
            var best = transform.forward;
            float bestDot = float.MinValue;
            foreach (var s in Sides())
            {
                float d = Vector3.Dot(to, s);
                if (d > bestDot) { bestDot = d; best = s; }
            }
            return best;
        }

        /// <summary>The clear side closest to where the player is looking; the way they came in if nothing else is free.</summary>
        Vector3 ExitSide()
        {
            var look = controller.transform.forward;
            var best = entrySide;
            float bestDot = float.MinValue;
            int blockers = Layers.SightBlockers | 1 << Layers.Id(Layers.Robot);
            float r = 0.33f;
            foreach (var s in Sides())
            {
                var p = SidePoint(s);
                if (Physics.CheckCapsule(p + Vector3.up * (r + 0.1f), p + Vector3.up * (controller.standHeight - r), r,
                                         blockers, QueryTriggerInteraction.Ignore)) continue;
                float d = Vector3.Dot(look, s);
                if (d > bestDot) { bestDot = d; best = s; }
            }
            return best;
        }

        // ---------------------------------------------------------------- found
        /// <summary>
        /// A robot that watched the player hide comes to fetch them: it walks to the front of the
        /// closet and the door swings open in its face, or it steps up to the side of the crate the
        /// player went in by. The capture scene carries on from there (the robot reaches in).
        /// </summary>
        public IEnumerator Discover(Transform robot)
        {
            StopAllCoroutines();                 // whatever the player was in the middle of
            busy = true;
            Vector3 stand, face;
            if (kind == HideKind.Closet)
            {
                // a step back from the doorway and a little to the side of the hinge, clear of the swinging door
                stand = exitPoint.position + exitPoint.forward * 0.25f + exitPoint.right * 0.3f;
                face = insidePoint.position;
            }
            else
            {
                stand = transform.position + entrySide * (sideDistance + 0.5f);      // room at its feet for the player to be hauled into
                face = transform.position;
            }
            stand.y = robot.position.y;
            var from = robot.position;
            var look = face - stand;
            look.y = 0f;
            var fromRotation = robot.rotation;
            var toRotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look) : robot.rotation;
            float seconds = Mathf.Clamp(Vector3.Distance(from, stand) / 2.2f, 0.35f, 1.2f);
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = t * t * (3f - 2f * t);
                robot.SetPositionAndRotation(Vector3.Lerp(from, stand, e), Quaternion.Slerp(fromRotation, toRotation, e));
                yield return null;
            }
            robot.SetPositionAndRotation(stand, toRotation);
            if (kind == HideKind.Closet)
            {
                yield return SwingDoor(door.localEulerAngles.y > 180f ? door.localEulerAngles.y - 360f : door.localEulerAngles.y, doorOpenAngle);
                yield break;
            }

            // under a crate: the robot bends down, gets a hand in, and hauls the player out along the floor
            // into the open at its feet, where the capture scene finishes the grab
            var gesture = robot.GetComponent<RobotGesture>();
            if (gesture != null) gesture.Play(Gesture.ReachDown);
            var robotFace = robot.position + Vector3.up * 1.35f;
            for (float t = 0; t < 0.4f; t += Time.deltaTime)            // the hand comes in
            {
                controller.LookAt(robotFace);
                yield return null;
            }
            var dragFrom = controller.transform.position;
            var dragTo = transform.position + entrySide * (sideDistance - 0.4f);        // head and shoulders clear of the crate
            dragTo.y = dragFrom.y;
            float yaw = controller.transform.eulerAngles.y;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.55f)
            {
                float e = t * t * (3f - 2f * t);
                // a little judder, as if dragged by one arm
                var jolt = entrySide * (0.03f * Mathf.Sin(t * 40f) * (1f - t));
                controller.SetPose(Vector3.Lerp(dragFrom, dragTo, e) + jolt, yaw);
                controller.LookAt(robotFace);
                yield return null;
            }
            controller.SetPose(dragTo, yaw);
            controller.LookAt(robotFace);
        }

        // ---------------------------------------------------------------- reset
        /// <summary>Put the player back outside without the animation (when caught or reset).</summary>
        public void Eject()
        {
            if (!inside) return;
            StopAllCoroutines();
            identity.SetHidden(this, false);
            if (kind == HideKind.Closet)
            {
                door.localRotation = Quaternion.Euler(0f, doorOpenAngle, 0f);
                controller.EyeAdjust = 0f;
            }
            else
            {
                controller.SetPose(SidePoint(entrySide), controller.transform.eulerAngles.y);
                controller.StandUp();
            }
            controller.MoveLocked = false;
            inside = false;
            busy = false;
        }
    }
}
