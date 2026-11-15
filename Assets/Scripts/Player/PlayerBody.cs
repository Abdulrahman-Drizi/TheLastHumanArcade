using System.Collections;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The player's own body: a complete jointed figure standing where the player stands, seen in
    /// first person (head hidden). Arms and hands are part of that figure, so they are always
    /// attached; they come into view when the camera tilts down, and every reach or grab is a fixed
    /// animation played on the figure. There are two figures, human and masked robot, in the same pose.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        [Header("Figures (children of the player)")]
        public GameObject human;
        public GameObject robot;
        public Animator humanAnimator;
        public Animator robotAnimator;
        public Transform humanHead, robotHead;
        public Transform[] robotParts;          // parent-first order: they lock on one after another
        public Renderer[] humanRenderers;

        [Header("Hands")]
        public Transform carryPoint;            // between the human's hands: where a carried ore sits
        public Transform gripPoint;             // in the human's right hand: where the mask is held
        public Vector3 sortReach;               // where the hands meet the ore, measured from the feet (body space)
        public Vector3 grabReach;               // where the right hand closes on the mask (body space)
        public float sortSeconds = 1.7f, sortGrip = 0.30f, sortRelease = 0.80f;
        public float grabSeconds = 1.0f, grabGrip = 0.42f, grabAtFace = 0.82f;

        [Header("Framing: how far down the view may tilt during a hand animation, so the hands stay low in the picture")]
        public float sortMaxPitch = 28f;
        public float grabMaxPitch = 14f;
        public float freeMaxPitch = 80f;

        [Header("Other")]
        public Camera bodyCamera;               // draws the figure on top of the world; blanked while the player is down low
        public FirstPersonController controller;
        public TransitionFx fx;
        public GameObject marker;               // arrow shown over the player in the opening flight

        static readonly int Speed = Animator.StringToHash("Speed"), WalkRate = Animator.StringToHash("WalkRate"),
                            Crouch = Animator.StringToHash("Crouch"), Present = Animator.StringToHash("Present"),
                            SortTrigger = Animator.StringToHash("Sort"), GrabTrigger = Animator.StringToHash("Grab");

        /// <summary>The first-person figure is not drawn at all (e.g. the player is inside a car, seen from outside).</summary>
        public bool Concealed { get; set; }

        PlayerForm form = PlayerForm.Human;
        bool presenting, changing;
        int firstPersonLayer, worldLayer;

        void Awake()
        {
            firstPersonLayer = Layers.Id(Layers.FirstPerson);
            worldLayer = Layers.Id(Layers.Robot);
            ShowForm(PlayerForm.Human);
            ShowInWorld(false);
        }

        void LateUpdate()
        {
            // Crouched or tucked away, the eyes are down among the folded knees and arms, so the
            // first-person figure is not drawn at all: nothing can cut into the view. It comes back
            // once the eyes have risen to standing height again.
            bool low = Concealed || controller.Stance != Stance.Standing || !controller.EyesAtStandingHeight;
            if (bodyCamera != null) bodyCamera.cullingMask = low ? 0 : 1 << firstPersonLayer;
        }

        void Update()
        {
            float speed = controller.PlanarSpeed;
            foreach (var a in new[] { humanAnimator, robotAnimator })
            {
                if (!a.gameObject.activeInHierarchy) continue;
                a.SetFloat(Speed, speed > 0.3f ? 1f : 0f, 0.1f, Time.deltaTime);
                a.SetFloat(WalkRate, Mathf.Clamp(speed / 2.6f, 0.5f, 2f));
                a.SetBool(Crouch, controller.IsCrouching);
                a.SetBool(Present, presenting);
            }
        }

        public void ShowForm(PlayerForm newForm)
        {
            form = newForm;
            human.SetActive(form == PlayerForm.Human);
            robot.SetActive(form == PlayerForm.Robot);
            foreach (var r in humanRenderers) r.enabled = true;
            foreach (var p in robotParts) p.localScale = Vector3.one;
        }

        /// <summary>
        /// Opening flight: the whole human figure, head included, is drawn in the world with an arrow
        /// over it. Otherwise the figure is first-person only and the head is hidden from the camera inside it.
        /// </summary>
        public void ShowInWorld(bool visible)
        {
            SetLayer(human, visible ? worldLayer : firstPersonLayer);
            SetLayer(robot, firstPersonLayer);
            humanHead.localScale = visible ? Vector3.one : Vector3.zero;
            robotHead.localScale = Vector3.zero;
            if (marker != null) marker.SetActive(visible);
        }

        static void SetLayer(GameObject root, int layer)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        // ---------------------------------------------------------------- fixed hand animations
        /// <summary>
        /// Sorting one ore: the player is lined up with it, both hands close on it, lift it and turn
        /// toward the bin, and let go. The ore rides in the hands between grip and release.
        /// </summary>
        public IEnumerator Sort(Transform ore, float facingYaw)
        {
            controller.BodyLocked = true;
            controller.MaxPitch = sortMaxPitch;
            yield return LineUp(ore.position, sortReach, facingYaw);
            humanAnimator.SetTrigger(SortTrigger);
            yield return new WaitForSeconds(sortSeconds * sortGrip);

            var parent = ore.parent;
            ore.SetParent(carryPoint, true);
            var from = ore.localPosition;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.08f)        // settle into the grip
            {
                ore.localPosition = Vector3.Lerp(from, Vector3.zero, t);
                yield return null;
            }
            ore.localPosition = Vector3.zero;
            yield return new WaitForSeconds(sortSeconds * (sortRelease - sortGrip) - 0.08f);
            ore.SetParent(parent, true);                                   // released: the caller tosses it into the bin
            StartCoroutine(Unlock(sortSeconds * (1f - sortRelease)));
        }

        /// <summary>The right hand reaches out, closes on the item and brings it up to the face.</summary>
        public IEnumerator Grab(Transform item, float facingYaw)
        {
            controller.BodyLocked = true;
            controller.MaxPitch = grabMaxPitch;
            yield return LineUp(item.position, grabReach, facingYaw);
            humanAnimator.SetTrigger(GrabTrigger);
            yield return new WaitForSeconds(grabSeconds * grabGrip);
            var parent = item.parent;
            item.SetParent(gripPoint, true);
            yield return new WaitForSeconds(grabSeconds * (grabAtFace - grabGrip));
            item.SetParent(parent, true);
            controller.BodyLocked = false;
            controller.MaxPitch = freeMaxPitch;
        }

        IEnumerator Unlock(float after)
        {
            yield return new WaitForSeconds(after);
            controller.BodyLocked = false;
            controller.MaxPitch = freeMaxPitch;
        }

        /// <summary>Step the player to where the fixed animation's hands will land exactly on the target.</summary>
        IEnumerator LineUp(Vector3 target, Vector3 reach, float yaw)
        {
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            var stand = target - rotation * reach;
            stand.y = transform.position.y;
            var fromPosition = transform.position;
            float fromYaw = transform.eulerAngles.y;
            for (float t = 0; t < 1f; t += Time.deltaTime / 0.22f)
            {
                float e = t * t * (3f - 2f * t);
                controller.SetPose(Vector3.Lerp(fromPosition, stand, e), Mathf.LerpAngle(fromYaw, yaw, e));
                yield return null;
            }
            controller.SetPose(stand, yaw);
        }

        // ---------------------------------------------------------------- changing form
        /// <summary>
        /// Human to robot or back. The full version raises both hands in front of the face while the
        /// robot parts lock on from the chest outward; the quick version just swaps under the glitch.
        /// </summary>
        public IEnumerator Change(PlayerForm to, bool full)
        {
            while (changing) yield return null;
            if (to == form) yield break;
            changing = true;
            if (fx != null) fx.Play(full ? 1.6f : 0.7f);

            if (full)
            {
                presenting = true;
                controller.MaxPitch = grabMaxPitch;           // the raised hands stay in the bottom of the picture
                yield return new WaitForSeconds(0.35f);       // hands come up
            }

            float stagger = full ? 0.07f : 0.025f, pop = full ? 0.2f : 0.1f;
            float total = stagger * (robotParts.Length - 1) + pop;
            if (to == PlayerForm.Robot)
            {
                robot.SetActive(true);
                SyncRobotPose();
                foreach (var p in robotParts) p.localScale = Vector3.zero;
                for (float time = 0; time < total; time += Time.deltaTime)
                {
                    for (int i = 0; i < robotParts.Length; i++)
                        robotParts[i].localScale = Vector3.one * OutBack(Mathf.Clamp01((time - i * stagger) / pop));
                    int covered = Mathf.FloorToInt(time / total * humanRenderers.Length);
                    for (int i = 0; i < humanRenderers.Length; i++) humanRenderers[i].enabled = i >= covered;
                    yield return null;
                }
            }
            else
            {
                human.SetActive(true);
                foreach (var r in humanRenderers) r.enabled = true;
                for (float time = 0; time < total; time += Time.deltaTime)
                {
                    for (int i = 0; i < robotParts.Length; i++)
                    {
                        int j = robotParts.Length - 1 - i;      // fold away from the hands back to the chest
                        robotParts[j].localScale = Vector3.one * (1f - Mathf.Clamp01((time - i * stagger) / pop));
                    }
                    yield return null;
                }
            }
            ShowForm(to);
            ShowInWorld(false);

            if (full)
            {
                yield return new WaitForSeconds(0.6f);
                presenting = false;
                yield return new WaitForSeconds(0.3f);
                controller.MaxPitch = freeMaxPitch;
            }
            changing = false;
        }

        /// <summary>Start the robot figure in the same state as the human one, so the two overlap.</summary>
        void SyncRobotPose()
        {
            robotAnimator.SetBool(Present, presenting);
            robotAnimator.SetBool(Crouch, controller.IsCrouching);
            robotAnimator.Play(presenting ? "Present" : controller.IsCrouching ? "Crouch" : "Idle", 0, 0f);
            robotAnimator.Update(0f);
        }

        static float OutBack(float p)
        {
            if (p <= 0f) return 0f;
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(p - 1f, 3) + c1 * Mathf.Pow(p - 1f, 2);
        }
    }
}
