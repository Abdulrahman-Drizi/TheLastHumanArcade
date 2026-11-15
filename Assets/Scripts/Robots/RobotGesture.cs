using UnityEngine;

namespace TheLastHuman
{
    public enum Gesture { None, Typing, Writing, Carrying, Reach, ReachDown, Scared, Cheer, Flee, Stunned, Welding, Sweeping, Talking, Inspecting, Charging }

    /// <summary>
    /// Poses a robot on top of whatever its animator is doing: typing at a desk, writing on a
    /// board, carrying a box, reaching out to grab the player, cowering, fleeing. The pose is
    /// blended in after the animator each frame, so the legs keep their own walk or idle unless the
    /// pose itself bends the knees (cowering does: the robot shrinks down, arms over its head).
    /// One small script covers every job, so the crowd needs no extra animation clips.
    /// </summary>
    public class RobotGesture : MonoBehaviour
    {
        public Transform chest, head, upperArmL, forearmL, upperArmR, forearmR;
        [Header("Only needed for poses that bend the knees")]
        public Transform hips;
        public Transform thighL, shinL, footL, thighR, shinR, footR;
        public Gesture gesture;
        public float blendSeconds = 0.25f;

        Gesture shown;
        float weight, phase, thighLength, shinLength;
        Vector3 hipsRest;
        bool lowered;

        void Awake()
        {
            phase = transform.position.x * 1.7f + transform.position.z * 2.3f;     // robots side by side do not move in step
            if (hips != null) hipsRest = hips.localPosition;
            if (thighL != null && shinL != null && footL != null)
            {
                thighLength = Vector3.Distance(thighL.position, shinL.position) / Mathf.Max(0.01f, hips.lossyScale.y);
                shinLength = Vector3.Distance(shinL.position, footL.position) / Mathf.Max(0.01f, hips.lossyScale.y);
            }
        }

        public void Play(Gesture g) => gesture = g;

        void LateUpdate()
        {
            if (gesture != Gesture.None) shown = gesture;
            weight = Mathf.MoveTowards(weight, gesture == Gesture.None ? 0f : 1f, Time.deltaTime / blendSeconds);
            if (weight <= 0f)
            {
                if (lowered) { hips.localPosition = hipsRest; lowered = false; }      // stand back up exactly
                return;
            }

            // angles in degrees; a negative arm angle swings it forward, a positive chest or head angle leans it forward
            float t = Time.time + phase;
            float lean = 0f, nod = 0f, armL = 0f, foreL = 0f, armR = 0f, foreR = 0f, turnR = 0f, spreadL = -5f, spreadR = 5f, sway = 0f;
            float knee = 0f;                    // how far the thighs fold forward; the shins fold back twice as much
            float headTurn = 0f;                // the head looking round (degrees to its right)
            switch (shown)
            {
                case Gesture.Typing:
                    lean = 7f; nod = 14f;
                    armL = -30f; foreL = -64f + 7f * Mathf.Sin(t * 11f);
                    armR = -30f; foreR = -64f + 7f * Mathf.Sin(t * 11f + 1.9f);
                    break;
                case Gesture.Writing:
                    lean = 4f; nod = 4f;
                    armL = -6f; foreL = -12f;
                    armR = -74f + 5f * Mathf.Sin(t * 6f); foreR = -28f + 6f * Mathf.Sin(t * 9f); turnR = 8f * Mathf.Sin(t * 4.5f);
                    break;
                case Gesture.Carrying:
                    lean = 3f;
                    armL = -36f; foreL = -66f;
                    armR = -36f; foreR = -66f;
                    break;
                case Gesture.Reach:
                    lean = 12f; nod = 8f;
                    armL = -14f; foreL = -20f;
                    armR = -84f; foreR = -8f;
                    break;
                case Gesture.ReachDown:
                    lean = 46f; nod = 18f;
                    armL = -20f; foreL = -24f;
                    armR = -52f; foreR = -10f;
                    break;
                case Gesture.Scared:
                {
                    // cowering: knees bent, body hunched small, forearms crossed over the head, the whole
                    // frame trembling; every so often it flinches lower and peeks up at the sky again
                    // Nothing here may read as arms thrown up in joy: the arms never rise above the shoulders.
                    // The robot folds up small: deep in the knees, back curled over, forearms pulled in tight
                    // across its face, the whole frame trembling. Now and then it peeks up at the sky and ducks again.
                    float tremble = Mathf.Sin(t * 31f) * 1.8f;
                    float peek = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 1.3f)), 4f);          // a quick frightened look up
                    knee = 58f - 10f * peek;
                    lean = 46f - 22f * peek + tremble * 0.5f;
                    nod = 26f - 58f * peek + tremble;                                         // head down between the arms, except when it peeks
                    armL = -72f + tremble; foreL = -148f;
                    armR = -72f - tremble; foreR = -148f;
                    spreadL = 22f; spreadR = -22f;                                            // elbows in, fists at its face
                    sway = Mathf.Sin(t * 27f) * 1.4f;
                    break;
                }
                case Gesture.Flee:
                {
                    // running for its life: bent right over, head ducked, both forearms held up in front of the
                    // face as if something were about to hit it, elbows pumping; every few strides it throws a
                    // look back over its shoulder
                    float stride = Mathf.Sin(t * 13f);
                    float back = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 0.9f)), 8f);
                    lean = 34f + 4f * stride; nod = 14f - 10f * back;
                    armL = -58f - 16f * stride; foreL = -128f; spreadL = 18f;
                    armR = -58f + 16f * stride; foreR = -128f; spreadR = -18f;
                    sway = 6f * stride;
                    turnR = 0f;
                    headTurn = 70f * back;
                    break;
                }
                case Gesture.Stunned:
                    // knocked silly: head lolling, arms hanging, swaying on the spot
                    lean = 10f + 5f * Mathf.Sin(t * 2.6f); nod = 26f;
                    sway = 9f * Mathf.Sin(t * 3.1f);
                    armL = 6f; foreL = -6f; armR = 6f; foreR = -6f;
                    break;
                // ---- working poses for the facility's staff
                case Gesture.Welding:           // bent over a job, one hand steadying it, the torch hand making small careful moves
                    lean = 30f; nod = 20f;
                    armL = -46f; foreL = -52f;
                    armR = -58f + 2f * Mathf.Sin(t * 5f); foreR = -38f + 3f * Mathf.Sin(t * 7f);
                    break;
                case Gesture.Sweeping:          // both hands on a broom, the body swinging with each stroke
                {
                    float stroke = Mathf.Sin(t * 2.6f);
                    lean = 20f; nod = 14f;
                    sway = 4f * stroke;
                    armL = -40f + 12f * stroke; foreL = -44f;
                    armR = -54f + 16f * stroke; foreR = -30f;
                    headTurn = 10f * stroke;
                    break;
                }
                case Gesture.Talking:           // explaining something with one hand, nodding along
                    lean = 3f; nod = 4f + 5f * Mathf.Sin(t * 3.1f);
                    armL = -8f; foreL = -20f;
                    armR = -46f + 14f * Mathf.Sin(t * 2.3f); foreR = -84f + 18f * Mathf.Sin(t * 3.7f + 1f); turnR = 14f * Mathf.Sin(t * 1.9f);
                    headTurn = 8f * Mathf.Sin(t * 0.8f);
                    break;
                case Gesture.Inspecting:        // a slate held up in one hand, the other tapping at it; the head goes from slate to work and back
                {
                    float glance = Mathf.Sin(t * 0.9f) > 0.55f ? 1f : 0f;
                    lean = 5f; nod = Mathf.Lerp(22f, -4f, glance);
                    armL = -38f; foreL = -96f;
                    armR = -34f; foreR = -104f + 6f * Mathf.Sin(t * 12f) * (1f - glance);
                    spreadL = 10f; spreadR = -14f;
                    break;
                }
                case Gesture.Charging:          // plugged in: slumped a little, quite still, a slow breath of the frame
                    lean = -6f + 1.5f * Mathf.Sin(t * 0.9f); nod = 16f;
                    armL = 4f; foreL = -6f; armR = 4f; foreR = -6f;
                    break;
                case Gesture.Cheer:             // both arms up, waving
                    lean = -4f; nod = -12f;
                    armL = -160f + 16f * Mathf.Sin(t * 7f); foreL = -18f + 10f * Mathf.Sin(t * 7f + 1f);
                    armR = -160f + 16f * Mathf.Sin(t * 7f + 2.1f); foreR = -18f + 10f * Mathf.Sin(t * 7f + 3f);
                    break;
            }
            Blend(chest, lean, 0f, sway);
            Blend(head, nod, headTurn, -sway * 0.5f);
            Blend(upperArmL, armL, 0f, spreadL);
            Blend(forearmL, foreL, 0f, 0f);
            Blend(upperArmR, armR, turnR, spreadR);
            Blend(forearmR, foreR, 0f, 0f);

            if (knee > 0f && thighLength > 0f)
            {
                Blend(thighL, -knee, 0f, 0f);
                Blend(thighR, -knee, 0f, 0f);
                Blend(shinL, knee * 2f, 0f, 0f);
                Blend(shinR, knee * 2f, 0f, 0f);
                Blend(footL, -knee, 0f, 0f);
                Blend(footR, -knee, 0f, 0f);
                // the hips come down by as much as the legs have folded, so the feet stay on the ground
                float fold = knee * Mathf.Deg2Rad;
                float drop = (thighLength + shinLength) * (1f - Mathf.Cos(fold));
                hips.localPosition = hipsRest + Vector3.down * (drop * weight);
                lowered = true;
            }
        }

        void Blend(Transform joint, float x, float y, float z)
        {
            if (joint != null) joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(x, y, z), weight);
        }
    }
}
