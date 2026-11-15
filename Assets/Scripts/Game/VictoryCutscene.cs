using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The ending, back in the human world. The robots are gone from the yard, the workers are
    /// gathered cheering under a banner hung on the tower: the player's portrait, "THE LAST HUMAN
    /// HERO", and their number. A camera rises from among the crowd up to the banner; then the end card.
    /// </summary>
    public class VictoryCutscene : MonoBehaviour
    {
        public const string Title = "THE LAST HUMAN HERO";

        public Camera flightCamera;
        public Camera[] playerCameras;
        public HudController hud;
        public GameObject[] hideForCelebration;     // robots, the work station's ore, anything that should be gone
        public GameObject celebration;              // the banner, the crowd and the lights, off until now
        public Camera portraitCamera;               // films the hero's face for the banner
        public UnityEngine.UI.RawImage portrait;
        public RobotGesture[] crowd;
        public Vector3[] path;                      // the camera's way from the crowd up to the banner
        public Vector3[] focus;
        public float seconds = 13f;

        RenderTexture picture;

        public IEnumerator Play()
        {
            foreach (var go in hideForCelebration) if (go != null) go.SetActive(false);
            celebration.SetActive(true);
            picture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) { name = "HeroPortrait", antiAliasing = 4 };
            portraitCamera.targetTexture = picture;
            portrait.texture = picture;
            foreach (var cheerer in crowd) cheerer.Play(Gesture.Cheer);

            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            StartCoroutine(hud.Fade(1f, 0f, 1.2f));
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                float u = t * t * (3f - 2f * t);
                var position = Along(path, u);
                flightCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Along(focus, u) - position, Vector3.up));
                flightCamera.fieldOfView = Mathf.Lerp(60f, 48f, u);
                yield return null;
            }
            yield return new WaitForSeconds(3.5f);         // hold on the banner
            hud.SetCompleteText(Title, "Human 017 switched the robots off and came home.", "Thank you for playing.");
            // the card goes over the banner, which says the same words: let almost nothing of it show through
            var plate = hud.completePanel.GetComponent<UnityEngine.UI.Image>();
            if (plate != null) plate.color = new Color(0f, 0.03f, 0.04f, 0.97f);
            FirstPersonController.CaptureCursor(false);
            hud.ShowComplete();
        }

        static Vector3 Along(Vector3[] points, float u)
        {
            float f = Mathf.Clamp01(u) * (points.Length - 1);
            int i = Mathf.Min((int)f, points.Length - 2);
            return Vector3.Lerp(points[i], points[i + 1], Mathf.SmoothStep(0f, 1f, f - i));
        }

        void OnDestroy()
        {
            if (picture == null) return;
            if (portraitCamera != null) portraitCamera.targetTexture = null;
            picture.Release();
            Destroy(picture);
        }
    }
}
