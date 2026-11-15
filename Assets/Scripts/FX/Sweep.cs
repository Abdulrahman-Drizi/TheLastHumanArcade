using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Swings an object from side to side about the vertical (a searchlight beam). Beams given the
    /// same timing and opposite 'mirror' signs cross and part in step with each other.
    /// </summary>
    public class Sweep : MonoBehaviour
    {
        public float angle = 38f;             // degrees either side of where it was built
        public float period = 7f;
        public float mirror = 1f;             // -1 swings the opposite way
        public float delay;

        Quaternion rest;

        void Awake() => rest = transform.localRotation;

        void Update()
        {
            float swing = Mathf.Sin((Time.time + delay) * Mathf.PI * 2f / period) * angle * mirror;
            transform.localRotation = Quaternion.Euler(0f, swing, 0f) * rest;
        }
    }
}
