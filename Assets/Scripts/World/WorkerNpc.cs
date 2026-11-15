using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// A background human at work. Plays the sorting loop at its own pace and shows an ore chunk
    /// in its hands for the part of the loop where it is carrying one to the bin.
    /// </summary>
    public class WorkerNpc : MonoBehaviour
    {
        public Animator animator;
        public GameObject carriedOre;         // child of a hand
        public float pace = 1f;               // playback speed, so workers do not move in step
        [Range(0f, 1f)] public float startOffset;
        [Range(0f, 1f)] public float carryFrom = 0.4f;
        [Range(0f, 1f)] public float carryTo = 0.76f;

        void Start()
        {
            animator.speed = pace;
            animator.Play("Sort", 0, startOffset);
        }

        void Update()
        {
            if (carriedOre == null) return;
            float t = Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, 1f);
            bool carrying = t >= carryFrom && t <= carryTo;
            if (carriedOre.activeSelf != carrying) carriedOre.SetActive(carrying);
        }
    }
}
