using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The kill switch going off, seen from outside. It only ever runs once the player is standing
    /// on the Monolith's roof (EscapeFlow starts it there). Two shots on a moving camera:
    ///   1. street level: the picture breaks up, a knot of robots cowers under "KILL SWITCH
    ///      ACTIVATED" hanging in the sky over the Monolith;
    ///   2. high over the avenue, looking back at the Monolith: behind it the deletion wave rises
    ///      at the edge of the map and starts eating the city.
    /// Then the view returns to the player on the roof, and the race begins.
    /// </summary>
    public class DissolutionSequence : MonoBehaviour
    {
        public Camera flightCamera;
        public Camera[] playerCameras;
        public HudController hud;
        public TransitionFx fx;
        public GameObject banner;               // the words in the sky
        public Text[] bannerTexts;
        public PanicRunner[] crowd;             // nearest the gathering point first
        public DeletionWave wave;
        public GameObject[] hideForStreetShot;  // the course over the avenue: it would stand between the street camera and the words
        public Vector3 bannerPoint = new Vector3(0f, 150f, 60f);
        public Vector3 gatherPoint = new Vector3(0f, 0f, -158f);    // on the avenue: where the first shot's crowd stands
        public float waveFrom = -260f, waveTo = 14f;                // the wave's front over the second shot
        public const string Unlock = "Unlocked The Parkour Robot Skills.";

        // where the robots of the first shot stand, relative to gatherPoint (three loose rows, nearest the camera first)
        static readonly Vector3[] gather =
        {
            new Vector3(-1.1f, 0f, 0f), new Vector3(1.3f, 0f, 0.4f), new Vector3(-3.4f, 0f, 1.6f), new Vector3(3.6f, 0f, 2.0f),
            new Vector3(0.2f, 0f, 3.2f), new Vector3(-2.2f, 0f, 4.6f), new Vector3(2.4f, 0f, 5.0f), new Vector3(-4.8f, 0f, 5.6f),
            new Vector3(4.9f, 0f, 6.2f), new Vector3(0.0f, 0f, 7.4f), new Vector3(-3.0f, 0f, 8.8f), new Vector3(3.2f, 0f, 9.4f),
        };

        [Header("The arrival: from the dead portal on the ground up to the player on the roof")]
        public GameObject roofFigure;           // stands in for the player on the roof while the camera is outside their head
        public Vector3[] arrivalPath;           // the camera's way up
        public Vector3[] arrivalFocus;          // what it looks at along the way
        public float arrivalSeconds = 9.5f;

        /// <summary>
        /// The scene opens here: the camera is at the dead portal at the end of Arrival Street, lifts
        /// away over the city and up the face of the Monolith, and comes to rest on the masked
        /// figure on its roof. Any "continue" key cuts it short.
        /// </summary>
        public IEnumerator Arrive()
        {
            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            if (roofFigure != null) roofFigure.SetActive(true);
            Aim(arrivalPath[0], arrivalFocus[0], 60f);
            StartCoroutine(hud.Fade(1f, 0f, 0.9f));
            hud.SetCaption("THE PORTAL YOU CAME IN BY  -  OFFLINE");
            bool skipped = false;
            for (float t = 0; t < 1f && !skipped; t += Time.deltaTime / arrivalSeconds)
            {
                float e = t * t * (3f - 2f * t);
                Aim(Along(arrivalPath, e), Along(arrivalFocus, e), Mathf.Lerp(60f, 50f, e));
                if (t > 0.2f) hud.SetCaption(t < 0.62f ? "ROBOTS CITY" : "THE ROOF OF THE MONOLITH DATA CENTER  -  YOU ARE HERE");
                if (t > 0.12f && GameInput.ContinuePressed) skipped = true;
                yield return null;
            }
            if (!skipped) yield return new WaitForSeconds(1.4f);
            hud.SetCaption(null);
            if (roofFigure != null) roofFigure.SetActive(false);
            flightCamera.enabled = false;
            foreach (var c in playerCameras) c.enabled = true;
            hud.SetLetterbox(false);
            hud.SetGameplayVisible(true);
        }

        static Vector3 Along(Vector3[] points, float u)
        {
            float f = Mathf.Clamp01(u) * (points.Length - 1);
            int i = Mathf.Min((int)f, points.Length - 2);
            float s = f - i;
            // a smooth curve through the points (Catmull-Rom), so the camera never turns a corner
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Length - 1)];
            return 0.5f * (2f * p1 + (p2 - p0) * s + (2f * p0 - 5f * p1 + 4f * p2 - p3) * (s * s) + (3f * p1 - p0 - 3f * p2 + p3) * (s * s * s));
        }

        public IEnumerator Play()
        {
            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            hud.SetGameplayVisible(false);
            hud.SetLetterbox(true);
            banner.SetActive(true);
            fx.PlaySustained(9.2f);

            // every robot in the streets stops and cowers under the words; a knot of them is right in front of the camera
            for (int i = 0; i < crowd.Length; i++)
            {
                if (i < gather.Length) crowd[i].transform.position = gatherPoint + gather[i];
                crowd[i].Cower(bannerPoint);
            }

            foreach (var go in hideForStreetShot) go.SetActive(false);

            // shot 1: low behind them, the words hanging over them; then the view lifts to the words
            Vector3 a0 = gatherPoint + new Vector3(-2.6f, 1.0f, -8.5f), a1 = gatherPoint + new Vector3(1.2f, 1.9f, -5.5f);
            Vector3 l0 = gatherPoint + new Vector3(0f, 9f, 14f), l1 = new Vector3(0f, 118f, 40f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 4.6f)
            {
                float e = t * t * (3f - 2f * t);
                Aim(Vector3.Lerp(a0, a1, e), Vector3.Lerp(l0, l1, Mathf.Clamp01(e * 1.9f - 0.9f)), Mathf.Lerp(66f, 58f, e));
                Blink(t * 4.6f);
                if (t > 0.1f && t < 0.12f) hud.PlayGlitch(1.2f, 0.7f);
                yield return null;
            }

            foreach (var go in hideForStreetShot) go.SetActive(true);

            // shot 2: high over the avenue, pulling away south: the wave comes up behind the Monolith and starts down the city
            Vector3 b0 = new Vector3(0f, 58f, -112f), b1 = new Vector3(0f, 92f, -176f);
            hud.PlayGlitch(4.4f, 0.6f);
            for (float t = 0; t < 1f; t += Time.deltaTime / 4.4f)
            {
                float e = t * t * (3f - 2f * t);
                Aim(Vector3.Lerp(b0, b1, e), Vector3.Lerp(new Vector3(0f, 84f, 98f), new Vector3(0f, 60f, 70f), e), Mathf.Lerp(60f, 70f, e));
                Blink(4.6f + t * 4.4f);
                wave.SetFront(Mathf.Lerp(waveFrom, waveTo, t));
                yield return null;
            }
            wave.SetFront(waveTo);
            foreach (var text in bannerTexts) text.color = new Color32(255, 60, 50, 255);

            fx.StopSustained();
            flightCamera.enabled = false;
            foreach (var c in playerCameras) c.enabled = true;
            hud.SetLetterbox(false, true);
            hud.SetGameplayVisible(true);
        }

        void Aim(Vector3 from, Vector3 at, float fieldOfView)
        {
            flightCamera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
            flightCamera.fieldOfView = fieldOfView;
        }

        /// <summary>The words flash between red and white.</summary>
        void Blink(float seconds)
        {
            bool on = Mathf.Repeat(seconds, 0.5f) < 0.3f;
            foreach (var text in bannerTexts) text.color = on ? new Color32(255, 60, 50, 255) : Color.white;
        }
    }
}
