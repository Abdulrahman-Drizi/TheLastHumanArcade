using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// One of the crowd in the streets during the escape. It cowers under the words in the sky,
    /// then runs for its life with no idea where to: along whichever street it is on, this way or
    /// that, straight across the road, back the way it came, down the other street at a crossing,
    /// and now and then it freezes in terror before bolting again. Nobody makes for the portal
    /// (it is shut). It never leaves the streets, and one the deletion wave reaches is deleted
    /// like everything else.
    /// </summary>
    public class PanicRunner : MonoBehaviour
    {
        /// <summary>A street it may run along: a straight line, east-west (at z = line) or north-south (at x = line), between two ends.</summary>
        [System.Serializable]
        public struct Street
        {
            public bool eastWest;
            public float line, from, to;
        }

        public Street[] streets;
        public float halfWidth = 8f;            // how far either side of a street's centre line it may go (the road and its pavements)
        public float speed = 5.6f;
        public int seed;                        // every runner panics in its own way
        public Animator animator;
        public RobotGesture gesture;
        public DeletionWave wave;

        static readonly int SpeedId = Animator.StringToHash("Speed");
        System.Random random;
        Vector3 spawn, target;
        Quaternion spawnRotation;
        int street = -1;
        float way = 1f, legSpeed, frozenFor;
        bool running;

        void Awake()
        {
            spawn = transform.position;
            spawnRotation = transform.rotation;
            random = new System.Random(seed);
        }

        /// <summary>Stop, turn to the sky and shrink down in fear.</summary>
        public void Cower(Vector3 lookAt)
        {
            running = false;
            frozenFor = 0f;
            var to = lookAt - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 1f) transform.rotation = Quaternion.LookRotation(to);
            if (animator != null) { animator.SetFloat(SpeedId, 0f); animator.speed = 1f; }
            if (gesture != null) gesture.Play(Gesture.Scared);
        }

        /// <summary>Run: anywhere, as long as it is away from here.</summary>
        public void Run()
        {
            if (random == null) random = new System.Random(seed);
            running = true;
            street = -1;
            way = random.NextDouble() < 0.5 ? -1f : 1f;
            Bolt();
        }

        /// <summary>Back to where it started, running again (the wave has been pushed back).</summary>
        public void Restore()
        {
            transform.SetPositionAndRotation(spawn, spawnRotation);
            gameObject.SetActive(true);
            Run();
        }

        void Update()
        {
            if (!running || streets == null || streets.Length == 0) return;
            if (wave != null && wave.U(transform.position) < wave.Front + 1.5f)
            {
                gameObject.SetActive(false);        // caught
                return;
            }
            if (frozenFor > 0f)
            {
                frozenFor -= Time.deltaTime;
                if (frozenFor <= 0f) Bolt();
                return;
            }
            var to = target - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.5f)
            {
                NextLeg(true);
                return;
            }
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * Time.deltaTime);
            transform.position += to / distance * Mathf.Min(legSpeed * Time.deltaTime, distance);
        }

        /// <summary>Off again at a sprint.</summary>
        void Bolt()
        {
            frozenFor = 0f;
            if (animator != null) { animator.SetFloat(SpeedId, 1f); animator.speed = 2.2f; }      // the walk, at a sprint
            if (gesture != null) gesture.Play(Gesture.Flee);
            NextLeg(false);
        }

        /// <summary>
        /// Choose where to run next. Every leg ends on the street the runner is on (or, from inside
        /// a crossing, on the street that crosses it), so a straight run to it never leaves the streets.
        /// </summary>
        void NextLeg(bool mayFreeze)
        {
            var p = transform.position;
            if (street < 0) street = Nearest(p);
            // at a crossing it is as likely as not to bolt down the other street
            for (int i = 0; i < streets.Length; i++)
            {
                if (i == street || !On(streets[i], p) || random.NextDouble() >= 0.5) continue;
                street = i;
                way = random.NextDouble() < 0.5 ? -1f : 1f;
                break;
            }
            var s = streets[street];
            float along = Along(s, p), across = Mathf.Clamp(Across(s, p), -halfWidth, halfWidth);
            double roll = random.NextDouble();
            if (mayFreeze && roll < 0.08)
            {
                // frozen to the spot for a moment, cowering
                frozenFor = Between(0.6f, 1.4f);
                if (animator != null) animator.SetFloat(SpeedId, 0f);
                if (gesture != null) gesture.Play(Gesture.Scared);
                return;
            }
            float toAlong, toAcross;
            if (roll < 0.26)
            {
                // straight across the road to the far side
                toAcross = (across >= 0f ? -1f : 1f) * Between(0.4f, 1f) * halfWidth;
                toAlong = along + way * Between(1.5f, 7f);
            }
            else
            {
                if (roll < 0.46) way = -way;                // doubles back
                toAcross = Between(-1f, 1f) * halfWidth;
                toAlong = along + way * Between(9f, 40f);
            }
            // the end of a street turns it round
            float low = s.from + 1f, high = s.to - 1f;
            if (toAlong < low || toAlong > high)
            {
                toAlong = Mathf.Clamp(toAlong, low, high);
                if (Mathf.Abs(toAlong - along) < 4f)
                {
                    way = -way;
                    toAlong = Mathf.Clamp(along + way * Between(9f, 40f), low, high);
                }
            }
            target = s.eastWest ? new Vector3(toAlong, p.y, s.line + toAcross) : new Vector3(s.line + toAcross, p.y, toAlong);
            legSpeed = speed * Between(0.85f, 1.35f);
        }

        float Between(float a, float b) => a + (float)random.NextDouble() * (b - a);

        static float Along(Street s, Vector3 p) => s.eastWest ? p.x : p.z;
        static float Across(Street s, Vector3 p) => (s.eastWest ? p.z : p.x) - s.line;

        /// <summary>Is this point on the street (its road or pavements, between its ends)?</summary>
        bool On(Street s, Vector3 p)
        {
            float along = Along(s, p);
            return Mathf.Abs(Across(s, p)) <= halfWidth && along >= s.from - halfWidth && along <= s.to + halfWidth;
        }

        /// <summary>The street this point is on, or failing that the one it is nearest to.</summary>
        int Nearest(Vector3 p)
        {
            int best = 0;
            float bestGap = float.MaxValue;
            for (int i = 0; i < streets.Length; i++)
            {
                var s = streets[i];
                float along = Along(s, p);
                float past = Mathf.Max(0f, Mathf.Max(s.from - along, along - s.to));
                float gap = Mathf.Abs(Across(s, p)) + past;
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return best;
        }
    }
}
