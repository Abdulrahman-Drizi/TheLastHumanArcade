using System.Collections.Generic;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// A point the grapple can take hold of. It bobs in the air, and when it is the one the player
    /// would hook (or is hanging from) its ring swells and its light comes up, so it is always
    /// clear which anchor the button will take.
    /// </summary>
    public class GrappleAnchor : MonoBehaviour
    {
        public Transform ring;
        public Light beacon;
        public bool Lit { get; set; }

        public static readonly List<GrappleAnchor> All = new List<GrappleAnchor>();
        float shown;
        Vector3 rest;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        void Start() => rest = transform.position;

        void Update()
        {
            shown = Mathf.MoveTowards(shown, Lit ? 1f : 0f, Time.deltaTime * 6f);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 9f) * shown;
            if (ring != null)
            {
                ring.localScale = Vector3.one * (1f + 0.9f * shown) * pulse;
                ring.Rotate(0f, (40f + 200f * shown) * Time.deltaTime, 0f, Space.Self);
            }
            if (beacon != null) beacon.intensity = 2.5f + 9f * shown;
            transform.position = rest + Vector3.up * (0.25f * Mathf.Sin(Time.time * 1.3f + rest.z));
        }
    }
}
