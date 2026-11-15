using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The opening flight: a separate camera starts at the portal, sweeps around the yard and ends
    /// exactly on the player's eyes, where the first-person cameras take over without a cut.
    /// </summary>
    public class IntroCinematic : MonoBehaviour
    {
        public Camera flightCamera;
        public Camera[] playerCameras;        // switched off during the flight, on at the end
        public Transform playerEyes;          // where the flight must finish
        public Vector3[] path;                // camera positions, first one at the portal
        public Vector3[] focus;               // what the camera looks at for each path point
        public float seconds = 11f;
        public float startFieldOfView = 52f;
        [Range(0.5f, 0.98f)] public float settleFrom = 0.9f;   // from here the view blends into the player's own
        public PlayerBody body;               // shown whole, with an arrow over it, until the camera is about to enter the head
        [Range(0.8f, 0.99f)] public float enterHeadAt = 0.965f;

        public bool Playing { get; private set; }

        public IEnumerator Play()
        {
            Playing = true;
            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            bool outside = true;
            if (body != null) body.ShowInWorld(true);
            float endFov = playerCameras.Length > 0 ? playerCameras[0].fieldOfView : 68f;

            // the route ends on the eyes, looking where the player looks
            int n = path.Length + 1;
            var points = new Vector3[n];
            var looks = new Vector3[n];
            for (int i = 0; i < path.Length; i++)
            {
                points[i] = path[i];
                looks[i] = focus[i];
            }
            points[n - 1] = playerEyes.position;
            looks[n - 1] = playerEyes.position + playerEyes.forward * 12f;

            float speed = 1f;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds * speed)
            {
                if (t > 0.08f && GameInput.ContinuePressed) speed = 5f;      // skip: fly the rest quickly
                float u = t * t * (3f - 2f * t);                               // ease in and out
                var position = Spline(points, u);
                var rotation = Quaternion.LookRotation(Spline(looks, u) - position, Vector3.up);
                float settle = Mathf.InverseLerp(settleFrom, 1f, u);
                settle = settle * settle * (3f - 2f * settle);
                flightCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(rotation, playerEyes.rotation, settle));
                flightCamera.fieldOfView = Mathf.Lerp(startFieldOfView, endFov, u * u);
                if (outside && u >= enterHeadAt)
                {
                    outside = false;
                    if (body != null) body.ShowInWorld(false);      // back to first person just before the camera reaches the eyes
                }
                yield return null;
            }

            flightCamera.transform.SetPositionAndRotation(playerEyes.position, playerEyes.rotation);
            if (body != null) body.ShowInWorld(false);
            flightCamera.enabled = false;
            foreach (var c in playerCameras) c.enabled = true;
            Playing = false;
        }

        /// <summary>Smooth curve through all points (Catmull-Rom), u from 0 to 1.</summary>
        static Vector3 Spline(Vector3[] p, float u)
        {
            float f = Mathf.Clamp01(u) * (p.Length - 1);
            int i = Mathf.Min((int)f, p.Length - 2);
            float t = f - i;
            Vector3 a = p[Mathf.Max(i - 1, 0)], b = p[i], c = p[i + 1], d = p[Mathf.Min(i + 2, p.Length - 1)];
            return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t * t + (3f * b - a - 3f * c + d) * t * t * t);
        }
    }
}
