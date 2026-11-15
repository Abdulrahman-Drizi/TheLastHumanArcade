using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Slow spin and float, used to make the mask easy to spot.</summary>
    public class Hover : MonoBehaviour
    {
        public float spin = 40f;
        public float amplitude = 0.05f;
        public float period = 2.4f;
        Vector3 origin;

        void Start() => origin = transform.localPosition;

        void Update()
        {
            transform.Rotate(0f, spin * Time.deltaTime, 0f, Space.World);
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(Time.time * Mathf.PI * 2f / period) * amplitude);
        }
    }
}
