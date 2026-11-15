using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Where the HUD's arrow should point. Never through a wall: if the goal can be seen from
    /// where the player stands, the arrow points at it; otherwise it points at the next corner on
    /// the shortest way there. The corners are a handful of points the level places at the ends of
    /// its lanes; which of them can see which is worked out once when the level starts.
    /// </summary>
    public class Guide : MonoBehaviour
    {
        public Transform[] corners;
        public Transform viewer;                // the player
        public Transform marker;                // what the arrow follows: this component moves it
        public float eye = 0.6f;                // sight lines are tested this high: low cover is in the way too

        bool[,] sees;
        float[] cost;
        int current = -1;
        Vector3 goal;
        bool hasGoal;

        void Start()
        {
            int n = corners.Length;
            sees = new bool[n, n];
            cost = new float[n];
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    sees[a, b] = sees[b, a] = Clear(corners[a].position, corners[b].position);
        }

        bool Clear(Vector3 a, Vector3 b)
        {
            a.y = b.y = eye;
            var to = b - a;
            float distance = to.magnitude;
            return distance < 0.05f || !Physics.SphereCast(a, 0.25f, to / distance, out _, distance, Layers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Aim the arrow at this place from now on.</summary>
        public void PointTo(Vector3 point)
        {
            if (hasGoal && (point - goal).sqrMagnitude < 0.01f) return;
            goal = point;
            hasGoal = true;
            current = -1;
            // how far each corner is from the goal, going round the corners
            int n = corners.Length;
            var done = new bool[n];
            for (int i = 0; i < n; i++) cost[i] = Clear(corners[i].position, goal) ? Flat(corners[i].position, goal) : float.MaxValue;
            for (int step = 0; step < n; step++)
            {
                int best = -1;
                for (int i = 0; i < n; i++)
                    if (!done[i] && (best < 0 || cost[i] < cost[best])) best = i;
                if (best < 0 || cost[best] == float.MaxValue) break;
                done[best] = true;
                for (int i = 0; i < n; i++)
                    if (!done[i] && sees[best, i])
                        cost[i] = Mathf.Min(cost[i], cost[best] + Flat(corners[best].position, corners[i].position));
            }
        }

        static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }

        void Update()
        {
            if (!hasGoal || sees == null) return;
            var from = viewer.position;
            if (Clear(from, goal))
            {
                current = -1;
                marker.position = goal;
                return;
            }
            // the visible corner with the shortest way on to the goal (the one already chosen is kept unless another is clearly better)
            int best = -1;
            float bestCost = float.MaxValue;
            for (int i = 0; i < corners.Length; i++)
            {
                if (cost[i] == float.MaxValue || !Clear(from, corners[i].position)) continue;
                float total = Flat(from, corners[i].position) + cost[i] - (i == current ? 1.5f : 0f);
                if (total < bestCost) { bestCost = total; best = i; }
            }
            if (best >= 0) current = best;
            marker.position = current >= 0 ? corners[current].position : goal;
        }
    }
}
