using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// A robot that is just going about its day: it walks a loop of points and never looks for
    /// the player. Deliberately tiny, so dozens can fill the streets: no sight checks, no physics
    /// queries, one transform move a frame.
    /// </summary>
    public class CrowdWalker : MonoBehaviour
    {
        public Vector3[] route;
        public float speed = 1.2f;
        public float turnRate = 200f;         // degrees per second
        public int startAt;
        public Animator animator;
        public AlertIcon icon;                // stars over its head while it is stunned (street walkers have one)

        static readonly int SpeedId = Animator.StringToHash("Speed");
        int index;
        float stunLeft;
        Gesture gestureBefore;

        /// <summary>Knocked silly (the car ran into it): it stops where it is for a while, then walks on.</summary>
        public void Stun(float seconds)
        {
            var gesture = GetComponent<RobotGesture>();
            if (stunLeft <= 0f && gesture != null) gestureBefore = gesture.gesture;
            stunLeft = Mathf.Max(stunLeft, seconds);
            if (animator != null) animator.SetFloat(SpeedId, 0f);
            if (gesture != null) gesture.Play(Gesture.Stunned);
            if (icon != null) icon.Stun();
        }

        void Start()
        {
            index = route != null && route.Length > 0 ? startAt % route.Length : 0;
            if (animator != null) animator.SetFloat(SpeedId, route != null && route.Length > 1 ? 1f : 0f);
        }

        void Update()
        {
            if (stunLeft > 0f)
            {
                stunLeft -= Time.deltaTime;
                if (stunLeft > 0f) return;
                var gesture = GetComponent<RobotGesture>();
                if (gesture != null) gesture.Play(gestureBefore);
                if (icon != null) icon.Hide();
                if (animator != null) animator.SetFloat(SpeedId, route != null && route.Length > 1 ? 1f : 0f);
            }
            if (route == null || route.Length < 2) return;
            var to = route[index] - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.15f)
            {
                index = (index + 1) % route.Length;
                return;
            }
            var heading = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, heading, turnRate * Time.deltaTime);
            // walk only once it is roughly facing the way it is going, so corners are turned, not slid round
            if (Quaternion.Angle(transform.rotation, heading) < 50f)
                transform.position += to / distance * Mathf.Min(speed * Time.deltaTime, distance);
        }
    }
}
