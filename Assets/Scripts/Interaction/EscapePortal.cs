using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The portal at the foot of the escape course: the way home.</summary>
    [RequireComponent(typeof(Collider))]
    public class EscapePortal : MonoBehaviour
    {
        public EscapeFlow flow;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerIdentity>() != null) flow.EnterPortal();
        }
    }
}
