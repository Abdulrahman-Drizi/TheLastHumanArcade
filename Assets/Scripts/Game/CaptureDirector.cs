using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The moment of being caught. The view snaps to the face of the robot that caught the player
    /// and pushes in while it reaches out to grab them. If the player was inside a hiding spot, the
    /// robot first walks up to it, opens the closet door (or bends down to the crate) and puts its
    /// hand on them. The level's flow then fades out and puts the player back at the checkpoint.
    /// </summary>
    public class CaptureDirector : MonoBehaviour
    {
        public FirstPersonController player;
        public Camera view;
        public HudController hud;
        public float grabSeconds = 1.0f;
        public float pushIn = 18f;            // degrees of field of view taken off during the grab

        float restFieldOfView;
        bool playing;

        public IEnumerator Play(RobotVision catcher)
        {
            if (catcher == null) yield break;
            playing = true;
            restFieldOfView = view.fieldOfView;
            var patrol = catcher.Patrol;
            var gesture = catcher.GetComponent<RobotGesture>();
            if (patrol != null) patrol.Held = true;

            var spot = HideSpot.Occupied;
            if (spot != null) yield return spot.Discover(catcher.transform);        // walk up, open the door
            else
            {
                var toPlayer = player.transform.position - catcher.transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f) catcher.transform.rotation = Quaternion.LookRotation(toPlayer);
            }

            if (gesture != null) gesture.Play(spot != null && spot.kind == HideKind.Box ? Gesture.ReachDown : Gesture.Reach);
            hud.PlayGlitch(0.45f, 0.7f);
            for (float t = 0; t < grabSeconds; t += Time.deltaTime)
            {
                player.LookAt(catcher.EyePosition);                                  // the face stays dead centre as it leans in
                float k = Mathf.Clamp01(t / 0.3f);
                view.fieldOfView = restFieldOfView - pushIn * k * k * (3f - 2f * k);
                yield return null;
            }
        }

        /// <summary>Put the camera and the robot back to normal (call once the screen is black).</summary>
        public void Restore(RobotVision catcher)
        {
            if (!playing) return;
            playing = false;
            view.fieldOfView = restFieldOfView;
            if (catcher == null) return;
            var gesture = catcher.GetComponent<RobotGesture>();
            if (gesture != null) gesture.Play(Gesture.None);
            if (catcher.Patrol != null) catcher.Patrol.Held = false;
        }
    }
}
