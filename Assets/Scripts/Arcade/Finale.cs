using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The plug is out. First the building falls quiet where the player stands: the hum winds
    /// down and the lamps go out one after another. Then the picture cuts to the yard: the patrols
    /// have stopped dead where they were, the gate of the work pen slides back, and the people
    /// who were kept in it walk out into the open and cheer. The title comes up over them:
    /// THE LAST HUMAN HERO, with one button, Replay.
    /// </summary>
    public class Finale : MonoBehaviour
    {
        public HudController hud;
        public ArcadeHud arcadeHud;
        public FirstPersonController player;
        public Camera flightCamera;
        public Camera[] playerCameras;

        [Header("The machinery going quiet")]
        public Light[] machineLights;           // nearest the plug first
        public Renderer[] machineGlow;          // everything that carries a lit material: lamps, screens, sockets, eye slits
        public Material[] liveMaterials;        // the lit materials themselves...
        public Material deadMaterial;           // ...and what they become
        public RobotPatrol[] patrols;           // they stop where they stand

        /// <summary>Every lit material on this renderer goes dark.</summary>
        public static void Kill(Renderer renderer, Material[] live, Material dead)
        {
            if (renderer == null) return;
            var mats = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
                if (System.Array.IndexOf(live, mats[i]) >= 0) { mats[i] = dead; changed = true; }
            if (changed) renderer.sharedMaterials = mats;
        }

        [Header("The work pen opening")]
        public Transform penGate;
        public Vector3 penGateSlide = new Vector3(3.2f, 0f, 0f);
        public Transform[] workers;
        public Animator[] workerAnimators;
        public RobotGesture[] workerGestures;
        public Hover[] workerHops;
        public Vector3[] workerGoals;           // where each one ends up, out in the yard
        public Vector3 faceToward;              // what they turn to once they are out
        public Light dawn;                      // a warm light that comes up over the yard
        public float dawnIntensity = 1.1f;
        public Vector3 cameraFrom, cameraTo, lookFrom, lookTo;

        static readonly int SpeedId = Animator.StringToHash("Speed");

        public IEnumerator Play()
        {
            // ---- quiet: the hum runs down, the lamps die one by one from the plug outward
            Sfx.Hum(0f, 0.45f);
            Sfx.Play(Sound.PowerDown, 0.7f, 0.8f);
            foreach (var p in patrols) if (p != null) p.Held = true;
            int steps = Mathf.Max(1, machineLights.Length), done = 0;
            for (int i = 0; i < steps; i++)
            {
                if (i < machineLights.Length && machineLights[i] != null) machineLights[i].enabled = false;
                int upTo = (i + 1) * machineGlow.Length / steps;
                for (; done < upTo; done++) Kill(machineGlow[done], liveMaterials, deadMaterial);
                Sfx.Play(Sound.Snap, 0.35f, 0.7f - 0.03f * i);
                yield return new WaitForSeconds(0.2f);
            }
            yield return new WaitForSeconds(0.9f);           // a moment of nothing at all

            // ---- cut to the yard
            yield return hud.Fade(0f, 1f, 0.5f);
            foreach (var c in playerCameras) c.enabled = false;
            flightCamera.enabled = true;
            hud.SetGameplayVisible(false, true);
            hud.SetLetterbox(true, true);
            Aim(0f);
            if (dawn != null) { dawn.enabled = true; dawn.intensity = 0f; }
            StartCoroutine(hud.Fade(1f, 0f, 0.7f));

            // the pen's gate slides back
            yield return new WaitForSeconds(0.5f);
            Sfx.Play(Sound.GateOpen, 0.6f, 1.25f);
            var gateFrom = penGate.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 1.1f)
            {
                penGate.localPosition = gateFrom + penGateSlide * (t * t * (3f - 2f * t));
                Aim(t * 0.12f);
                yield return null;
            }

            // and they come out, a few at a time
            var starts = new Vector3[workers.Length];
            for (int i = 0; i < workers.Length; i++)
            {
                starts[i] = workers[i].position;
                workerGestures[i].Play(Gesture.None);
            }
            const float walk = 3.2f, stagger = 0.28f;
            float total = walk + stagger * (workers.Length - 1);
            for (float time = 0; time < total; time += Time.deltaTime)
            {
                for (int i = 0; i < workers.Length; i++)
                {
                    float u = Mathf.Clamp01((time - i * stagger) / walk);
                    bool moving = u > 0f && u < 1f;
                    var at = Vector3.Lerp(starts[i], workerGoals[i], u * u * (3f - 2f * u));
                    var step = at - workers[i].position;
                    step.y = 0f;
                    if (step.sqrMagnitude > 1e-6f)
                        workers[i].rotation = Quaternion.Slerp(workers[i].rotation, Quaternion.LookRotation(step), Time.deltaTime * 8f);
                    workers[i].position = at;
                    workerAnimators[i].SetFloat(SpeedId, moving ? 1f : 0f, 0.15f, Time.deltaTime);
                    if (u >= 1f && workerGestures[i].gesture != Gesture.Cheer) Arrive(i);
                }
                float k = time / total;
                Aim(0.12f + 0.88f * k);
                if (dawn != null) dawn.intensity = dawnIntensity * k;
                yield return null;
            }
            for (int i = 0; i < workers.Length; i++)
                if (workerGestures[i].gesture != Gesture.Cheer) Arrive(i);
            Sfx.Play(Sound.Fanfare);
            yield return new WaitForSeconds(0.6f);
            FirstPersonController.CaptureCursor(false);
            arcadeHud.ShowWin();
        }

        /// <summary>One of them is out: turn to the others, arms up.</summary>
        void Arrive(int i)
        {
            var to = faceToward - workers[i].position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f) workers[i].rotation = Quaternion.LookRotation(to);
            workerAnimators[i].SetFloat(SpeedId, 0f);
            workerGestures[i].Play(Gesture.Cheer);
            if (workerHops[i] != null) workerHops[i].enabled = true;
        }

        void Aim(float u)
        {
            float e = u * u * (3f - 2f * u);
            var from = Vector3.Lerp(cameraFrom, cameraTo, e);
            flightCamera.transform.SetPositionAndRotation(from, Quaternion.LookRotation(Vector3.Lerp(lookFrom, lookTo, e) - from, Vector3.up));
        }
    }
}
