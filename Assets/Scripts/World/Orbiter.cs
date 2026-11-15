using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Flies a slow circle at a fixed height, nose along the path (airships and sky traffic).</summary>
    public class Orbiter : MonoBehaviour
    {
        public Vector3 centre;
        public float radius = 100f;
        public float degreesPerSecond = 3f;     // negative flies the other way round
        public float startAngle;

        void Update()
        {
            float a = (startAngle + degreesPerSecond * Time.time) * Mathf.Deg2Rad;
            var p = centre + new Vector3(Mathf.Sin(a) * radius, 0f, Mathf.Cos(a) * radius);
            var heading = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a)) * Mathf.Sign(degreesPerSecond);
            transform.SetPositionAndRotation(p, Quaternion.LookRotation(heading));
        }
    }
}
