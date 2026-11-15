using System.Collections.Generic;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The kill switch's deletion wave: a wall of white that starts at the back of the map (north
    /// of the Monolith) and sweeps south down the city, erasing everything it passes. When it
    /// reaches the last street it turns and runs west along it, toward the portal.
    ///
    /// Every point of the map has a place 'u' along that sweep (metres): in the north part it is
    /// simply how far south the point is; on the last street it is how far west. The wave's front
    /// is one such number; whatever lies behind it is switched off. The player's own u, compared
    /// with the front, is how far ahead of the wave they are.
    ///
    /// Its speed depends on that lead: it hurries to catch up when the player is far ahead and
    /// eases off when it is at their heels, so it is always close enough to be felt and never
    /// impossible to outrun.
    /// </summary>
    public class DeletionWave : MonoBehaviour
    {
        public Transform[] roots;               // the children of these are the pieces of the world
        public GameObject[] singles;            // whole objects deleted in one piece (the sky banner, searchlights)
        public Transform wallNorth;             // the visible front while it sweeps south
        public Transform wallWest;              // and once it has turned along the last street

        [Header("The sweep")]
        public float northEdge = 170f;          // z of the back of the map: u is 0 here
        public float turnZ = -182f;             // south of this line the wave runs west instead

        [Header("Speed (metres per second) by how far ahead the player is")]
        public float nearGap = 22f, nearSpeed = 3.8f;
        public float farGap = 60f, farSpeed = 12f;
        public float cruiseSpeed = 5.6f;

        public float Front { get; private set; } = -1000f;
        public float TurnU => northEdge - turnZ;

        struct Piece { public GameObject go; public float u; }
        readonly List<Piece> pieces = new List<Piece>();
        int next;                               // pieces before this index are deleted

        /// <summary>Where a point lies along the sweep.</summary>
        public float U(Vector3 p)
        {
            if (p.z > turnZ) return northEdge - p.z;
            return TurnU + (p.x < 0f ? -p.x : -0.5f * p.x);      // east of the avenue the last street goes as the wave arrives
        }

        void Awake()
        {
            var skip = new HashSet<Transform>(roots);
            foreach (var root in roots)
                foreach (Transform child in root)
                    if (!skip.Contains(child) && child.gameObject.activeSelf) Add(child.gameObject);
            if (singles != null) foreach (var single in singles) if (single != null) Add(single);
            pieces.Sort((a, b) => a.u.CompareTo(b.u));
            ShowWalls();
        }

        void Add(GameObject go)
        {
            // a piece goes when the wave reaches its middle
            var renderers = go.GetComponentsInChildren<Renderer>();
            Vector3 centre = go.transform.position;
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                centre = bounds.center;
            }
            pieces.Add(new Piece { go = go, u = U(centre) });
        }

        /// <summary>Put the front somewhere: everything behind it goes, everything ahead of it is (back) there.</summary>
        public void SetFront(float u)
        {
            Front = u;
            while (next < pieces.Count && pieces[next].u < u)
            {
                if (pieces[next].go != null) pieces[next].go.SetActive(false);
                next++;
            }
            while (next > 0 && pieces[next - 1].u >= u)
            {
                next--;
                if (pieces[next].go != null) pieces[next].go.SetActive(true);
            }
            ShowWalls();
        }

        /// <summary>Advance for one frame, given where the player is.</summary>
        public void Tick(float playerU, float dt)
        {
            float gap = playerU - Front;
            float speed = gap > farGap ? farSpeed : gap < nearGap ? nearSpeed : cruiseSpeed;
            SetFront(Front + speed * dt);
        }

        void ShowWalls()
        {
            bool south = Front < TurnU;
            if (wallNorth != null)
            {
                if (wallNorth.gameObject.activeSelf != south) wallNorth.gameObject.SetActive(south);
                var p = wallNorth.position;
                p.z = northEdge - Front;
                wallNorth.position = p;
            }
            if (wallWest != null)
            {
                if (wallWest.gameObject.activeSelf == south) wallWest.gameObject.SetActive(!south);
                var p = wallWest.position;
                p.x = -(Front - TurnU);
                wallWest.position = p;
            }
        }
    }
}
