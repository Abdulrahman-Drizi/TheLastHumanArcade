using UnityEngine;

namespace TheLastHuman
{
    public enum TutorialKind { None, Jump, Slide, WallRun, Grapple }

    /// <summary>
    /// A stage of the escape course. Crossing it makes it the place the player comes back to after
    /// a fall, and can bring up a tutorial card and a few lines of talk the first time.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        public EscapeFlow flow;
        public int order;                       // later checkpoints have higher numbers
        public Transform spawn;                 // where (and facing which way) the player is put back
        public TutorialKind tutorial;
        [TextArea] public string[] lines;       // "SPEAKER|what they say"

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerIdentity>() != null) flow.Reach(this);
        }
    }
}
