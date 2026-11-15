using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Walks a loop of stops. At each stop the robot can wait and face a set direction, so a
    /// "sentry" is simply two stops at the same spot facing different ways.
    /// When it spots the player it stops and stares for a second ("?" over its head), then runs
    /// ("!") to where it last saw them, looks around there if they are gone, and returns to its route.
    /// Turning is eased and always plays the stepping animation.
    /// </summary>
    public class RobotPatrol : MonoBehaviour
    {
        [Serializable]
        public struct Stop
        {
            public Vector3 position;
            public float wait;         // seconds spent here
            public bool faceDirection; // turn to lookYaw while waiting
            public float lookYaw;      // degrees, 0 = +Z (north on the minimap)
        }

        public Stop[] route;
        public float walkSpeed = 1.3f;
        public float chaseSpeedFactor = 2.3f;       // a brisk run once it is sure...
        public float chaseSpeed = 6.6f;             // ...and never slower than this: faster than the player can sprint (5 m/s)
        public float suspectSeconds = 1f;           // how long it stares ("?") before it comes ("!")
        public float closeRange = 4.5f;             // seen from nearer than this, it hardly hesitates at all
        public float closeSuspectSeconds = 0.35f;
        public AlertIcon icon;                      // the mark over its head
        public float searchSeconds = 2.5f;          // how long it looks around where it lost the player
        public float turnSpeed = 110f;        // top turning rate, degrees per second
        public float turnSmoothing = 0.28f;   // seconds to ease into and out of a turn
        public float walkWhileTurningBelow = 50f;   // sharper turns than this are made on the spot
        public Animator animator;

        public bool Chasing => mode != Mode.Patrol;
        /// <summary>Stand still (a cutscene is moving or posing this robot).</summary>
        public bool Held { get; set; }
        /// <summary>Knocked silly (the car ran into it): it stands swaying and sees nothing until this runs out.</summary>
        public bool Stunned => stunLeft > 0f;
        float stunLeft;

        /// <summary>Freeze for a while. Whatever it was chasing is forgotten.</summary>
        public void Stun(float seconds)
        {
            stunLeft = Mathf.Max(stunLeft, seconds);
            mode = Mode.Patrol;
            if (icon != null) icon.Stun();
            var gesture = GetComponent<RobotGesture>();
            if (gesture != null) gesture.Play(Gesture.Stunned);
        }

        /// <summary>It is running the player down (not merely suspicious or searching).</summary>
        public bool Running => mode == Mode.Chase;

        enum Mode { Patrol, Suspect, Chase, Search }
        float suspectLeft;
        static readonly int SpeedId = Animator.StringToHash("Speed");
        Mode mode;
        int index;
        float waitLeft, yawVelocity, searchLeft, searchYaw, stuck;
        Vector3 chasePoint;

        void Start()
        {
            if (route != null && route.Length > 0) waitLeft = route[0].wait;
        }

        /// <summary>The robot has seen the player at this point: walk there.</summary>
        public void Pursue(Vector3 point)
        {
            if (Stunned) return;
            chasePoint = new Vector3(point.x, transform.position.y, point.z);
            stuck = 0f;
            if (mode == Mode.Chase) return;
            if (mode == Mode.Suspect) return;             // still making up its mind; it keeps the newest position
            // first sight: stop and stare for a moment before giving chase
            mode = Mode.Suspect;
            suspectLeft = Vector3.Distance(chasePoint, transform.position) < closeRange ? closeSuspectSeconds : suspectSeconds;
            if (icon != null) icon.Suspect();
        }

        /// <summary>An alarm went off: no hesitation, run straight to the point.</summary>
        public void Alarm(Vector3 point)
        {
            if (Stunned) return;
            chasePoint = new Vector3(point.x, transform.position.y, point.z);
            stuck = 0f;
            mode = Mode.Chase;
            if (icon != null) icon.Alarm();
        }

        /// <summary>Drop the chase and go back to the route.</summary>
        public void GiveUp()
        {
            mode = Mode.Patrol;
            if (icon != null) icon.Hide();
            if (animator != null) animator.speed = 1f;
        }

        void Suspect()
        {
            Turn(chasePoint - transform.position);
            Animate(false);
            suspectLeft -= Time.deltaTime;
            if (suspectLeft > 0f) return;
            mode = Mode.Chase;
            if (icon != null) icon.Alarm();
        }

        void Update()
        {
            if (Held)
            {
                Animate(false);
                return;
            }
            if (stunLeft > 0f)
            {
                stunLeft -= Time.deltaTime;
                Animate(false);
                if (stunLeft <= 0f)
                {
                    if (icon != null) icon.Hide();
                    var gesture = GetComponent<RobotGesture>();
                    if (gesture != null && gesture.gesture == Gesture.Stunned) gesture.Play(Gesture.None);
                }
                return;
            }
            if (animator != null) animator.speed = mode == Mode.Chase ? 2.6f : 1f;      // legs keep up with the run
            switch (mode)
            {
                case Mode.Suspect: Suspect(); break;
                case Mode.Chase: Chase(); break;
                case Mode.Search: Search(); break;
                default: Patrol(); break;
            }
        }

        void Chase()
        {
            var to = chasePoint - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.5f || stuck > 2.5f)
            {
                mode = Mode.Search;
                searchLeft = searchSeconds;
                searchYaw = transform.eulerAngles.y;
                if (icon != null) icon.Suspect();
                return;
            }
            float off = Turn(to);
            if (off < walkWhileTurningBelow)
            {
                float step = Mathf.Max(walkSpeed * chaseSpeedFactor, chaseSpeed) * Time.deltaTime;
                if (!Walk(to / distance, Mathf.Min(step, distance))) stuck += Time.deltaTime;
                else stuck = 0f;
            }
            Animate(true);
        }

        void Search()
        {
            searchLeft -= Time.deltaTime;
            // sweep left and right of where it was heading
            float sweep = Mathf.Sin((searchSeconds - searchLeft) * 2.4f) * 70f;
            Turn(Quaternion.Euler(0f, searchYaw + sweep, 0f) * Vector3.forward);
            Animate(true);
            if (searchLeft <= 0f) GiveUp();
        }

        void Patrol()
        {
            if (route == null || route.Length == 0)
            {
                Animate(false);
                return;
            }
            var stop = route[index];
            var to = stop.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance > 0.06f)
            {
                float off = Turn(to);
                if (off < walkWhileTurningBelow)
                {
                    // slow down a little while still swinging round, so the path curves instead of snapping
                    float pace = Mathf.Lerp(1f, 0.45f, off / walkWhileTurningBelow);
                    transform.position += to / distance * Mathf.Min(walkSpeed * pace * Time.deltaTime, distance);
                }
                Animate(true);      // walking, or stepping on the spot through a sharp turn
                return;
            }

            bool turning = stop.faceDirection && Turn(Quaternion.Euler(0f, stop.lookYaw, 0f) * Vector3.forward) > 4f;
            Animate(turning);
            waitLeft -= Time.deltaTime;
            if (waitLeft <= 0f)
            {
                index = (index + 1) % route.Length;
                waitLeft = route[index].wait;
            }
        }

        /// <summary>One step in a direction, sliding along walls and containers. False if it could not move.</summary>
        bool Walk(Vector3 direction, float step)
        {
            var origin = transform.position + Vector3.up * 1.0f;
            const float radius = 0.4f;
            if (Physics.SphereCast(origin, radius, direction, out var hit, step + 0.1f, Layers.SightBlockers, QueryTriggerInteraction.Ignore))
            {
                var slide = Vector3.ProjectOnPlane(direction, hit.normal);
                slide.y = 0f;
                if (slide.sqrMagnitude < 0.01f) return false;
                slide.Normalize();
                if (Physics.SphereCast(origin, radius, slide, out _, step + 0.1f, Layers.SightBlockers, QueryTriggerInteraction.Ignore)) return false;
                direction = slide;
            }
            transform.position += direction * step;
            return true;
        }

        /// <summary>Eases the body toward a direction and returns how many degrees are still left.</summary>
        float Turn(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return 0f;
            float target = Quaternion.LookRotation(direction).eulerAngles.y;
            float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, target, ref yawVelocity, turnSmoothing, turnSpeed);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            return Mathf.Abs(Mathf.DeltaAngle(yaw, target));
        }

        /// <summary>Legs move whenever the robot walks or turns.</summary>
        void Animate(bool moving)
        {
            if (!moving) yawVelocity = Mathf.MoveTowards(yawVelocity, 0f, 400f * Time.deltaTime);
            if (animator != null) animator.SetFloat(SpeedId, moving ? 1f : 0f, 0.12f, Time.deltaTime);
        }
    }
}
