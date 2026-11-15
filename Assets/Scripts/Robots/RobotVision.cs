using System.Collections.Generic;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// What a robot can see: a triangle on the ground that starts at the robot and points where its
    /// head points, blocked by level geometry. The minimap draws exactly this triangle.
    /// </summary>
    public class RobotVision : MonoBehaviour
    {
        public Transform head;
        public float viewAngle = 56f;       // full angle of the triangle at the robot
        public float viewDistance = 7.5f;   // how far ahead the triangle reaches
        public float eyeHeight = 0.15f;     // above the head pivot
        public string variant = "Orange";   // Orange / Purple / Blue, used for the minimap colour

        /// <summary>Set every frame by <see cref="DetectionSystem"/>.</summary>
        public bool SeesPlayer { get; set; }

        /// <summary>This robot's movement, if it has any.</summary>
        public RobotPatrol Patrol { get; private set; }

        void Awake() => Patrol = GetComponent<RobotPatrol>();

        public Vector3 EyePosition => head.position + Vector3.up * eyeHeight;

        /// <summary>Viewing direction on the ground plane. Follows the head, so idle look-arounds count.</summary>
        public Vector3 Forward
        {
            get
            {
                var f = head.forward;
                f.y = 0f;
                return f.sqrMagnitude < 1e-4f ? transform.forward : f.normalized;
            }
        }

        public bool CanSee(Vector3 point)
        {
            var eye = EyePosition;
            var to = point - eye;
            var flat = new Vector3(to.x, 0f, to.z);
            var forward = Forward;
            float ahead = Vector3.Dot(flat, forward);
            if (ahead < 0.05f || ahead > viewDistance) return false;
            if (Vector3.Angle(forward, flat) > viewAngle * 0.5f) return false;
            return !Physics.Linecast(eye, point, Layers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        public static readonly List<RobotVision> All = new List<RobotVision>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();
    }
}
