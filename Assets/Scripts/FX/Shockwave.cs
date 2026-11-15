using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>A ring of light that bursts outward along the ground and fades (the door's answer to a failed hack).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class Shockwave : MonoBehaviour
    {
        public float radius = 10f;
        public float seconds = 0.65f;
        public float width = 0.55f;
        public Color colour = new Color32(150, 240, 255, 255);

        const int Points = 48;
        LineRenderer line;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Points;
            for (int i = 0; i < Points; i++)
            {
                float a = i * Mathf.PI * 2f / Points;
                line.SetPosition(i, new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)));
            }
            line.enabled = false;
        }

        public void Play(Vector3 centre)
        {
            transform.position = centre;
            StopAllCoroutines();
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            line.enabled = true;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float e = 1f - (1f - t) * (1f - t);                 // fast at first, slowing as it spreads
                transform.localScale = Vector3.one * Mathf.Lerp(0.4f, radius, e);
                line.widthMultiplier = Mathf.Lerp(width, 0.06f, t);
                var c = colour;
                c.a = 1f - t;
                line.startColor = line.endColor = c;
                yield return null;
            }
            line.enabled = false;
        }
    }
}
