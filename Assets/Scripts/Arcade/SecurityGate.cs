using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The main security gate. Four empty sockets across its face; F at the gate seats every
    /// battery the player is carrying, one after another, each with a hard click and its socket
    /// coming alight. With all four in, the lamps over the gate change, and it opens.
    /// </summary>
    public class SecurityGate : Interactable
    {
        public ArcadeFlow flow;
        public Transform[] sockets;             // where each battery sits
        public GameObject[] seated;             // the battery shown in each socket once it is in
        public Renderer[] socketLamps;          // the ring of each socket
        public Material lampOff, lampOn;
        public Transform leafLeft, leafRight;   // the two halves of the door
        public float leafTravel = 2.3f;
        public Collider barrier;                // what actually stops the player
        public Light[] gateLights;              // over the gate: amber while locked, cyan once it is powered
        public Renderer[] statusLamps;          // the lamps in its header, which change with them
        public Material statusOn;
        public Color poweredColour = new Color32(106, 202, 197, 255);
        public Transform standPoint;

        public int Inserted { get; private set; }
        public bool IsOpen { get; private set; }
        bool busy;

        public override Vector3 Position => standPoint != null ? standPoint.position : transform.position;
        public override string Prompt => IsOpen || busy || flow.Carried == 0 ? null
            : flow.Carried == 1 ? "Insert the battery" : "Insert " + flow.Carried + " batteries";

        void Awake()
        {
            foreach (var go in seated) go.SetActive(false);
            foreach (var lamp in socketLamps) lamp.sharedMaterial = lampOff;
        }

        public override void Interact(PlayerInteractor player)
        {
            if (IsOpen || busy || flow.Carried == 0) return;
            StartCoroutine(Insert());
        }

        IEnumerator Insert()
        {
            busy = true;
            while (flow.Carried > 0 && Inserted < sockets.Length)
            {
                int i = Inserted;
                // the battery leaves the hands and drives home into its socket
                var battery = seated[i].transform;
                var home = battery.position;
                var homeScale = battery.localScale;
                var from = flow.player.cameraPivot.position + flow.player.cameraPivot.forward * 0.45f - Vector3.up * 0.3f;
                seated[i].SetActive(true);
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.2f)
                {
                    float e = t * t * t;                                    // slow off the hands, fast into the socket
                    battery.position = Vector3.Lerp(from, home, e);
                    battery.localScale = homeScale * Mathf.Lerp(0.45f, 1f, t);
                    yield return null;
                }
                battery.position = home;
                battery.localScale = homeScale;
                socketLamps[i].sharedMaterial = lampOn;
                Sfx.Play(Sound.Snap, 1f, 1f + 0.06f * i);
                flow.player.Shake(0.9f, 0.12f);
                Inserted++;
                flow.Inserted(Inserted);
                // seated with a little overshoot: it knocks in and settles
                for (float t = 0; t < 1f; t += Time.deltaTime / 0.12f)
                {
                    battery.position = home + sockets[i].forward * (0.05f * Mathf.Sin(t * Mathf.PI));
                    yield return null;
                }
                battery.position = home;
                yield return new WaitForSeconds(0.08f);
            }
            busy = false;
            if (Inserted >= sockets.Length) yield return Open();
        }

        IEnumerator Open()
        {
            IsOpen = true;
            yield return new WaitForSeconds(0.25f);
            foreach (var l in gateLights) l.color = poweredColour;
            foreach (var lamp in statusLamps) lamp.sharedMaterial = statusOn;
            Sfx.Play(Sound.GateOpen);
            flow.player.Shake(1.4f, 0.5f);
            Vector3 leftRest = leafLeft.localPosition, rightRest = leafRight.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.5f)
            {
                float e = t * t * (3f - 2f * t);
                leafLeft.localPosition = leftRest + Vector3.left * (leafTravel * e);
                leafRight.localPosition = rightRest + Vector3.right * (leafTravel * e);
                if (t > 0.45f && barrier.enabled) barrier.enabled = false;      // wide enough to walk through
                yield return null;
            }
            barrier.enabled = false;
            flow.GateOpened();
        }

        /// <summary>The player is through: it slams shut behind them. There is no going back to the yard.</summary>
        public IEnumerator Shut()
        {
            barrier.enabled = true;
            Vector3 left = leafLeft.localPosition, right = leafRight.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
            {
                leafLeft.localPosition = left + Vector3.right * (leafTravel * t * t);
                leafRight.localPosition = right + Vector3.left * (leafTravel * t * t);
                yield return null;
            }
            leafLeft.localPosition = left + Vector3.right * leafTravel;
            leafRight.localPosition = right + Vector3.left * leafTravel;
            Sfx.Play(Sound.Snap, 1f, 0.6f);
        }

        /// <summary>Already powered and open (a retry that starts at the boss).</summary>
        public void OpenAtOnce()
        {
            IsOpen = true;
            Inserted = sockets.Length;
            for (int i = 0; i < seated.Length; i++)
            {
                seated[i].SetActive(true);
                socketLamps[i].sharedMaterial = lampOn;
            }
            foreach (var l in gateLights) l.color = poweredColour;
            foreach (var lamp in statusLamps) lamp.sharedMaterial = statusOn;
            leafLeft.localPosition += Vector3.left * leafTravel;
            leafRight.localPosition += Vector3.right * leafTravel;
            barrier.enabled = false;
        }
    }
}
