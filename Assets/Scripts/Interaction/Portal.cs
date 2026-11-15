using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Walking in as a robot ends the round. Humans are turned away.</summary>
    [RequireComponent(typeof(Collider))]
    public class Portal : MonoBehaviour
    {
        public GameFlow flow;
        public HudController hud;

        void OnTriggerEnter(Collider other)
        {
            var identity = other.GetComponentInParent<PlayerIdentity>();
            if (identity == null) return;
            if (identity.IsRobot) flow.CompleteRound();
            else hud.ShowWarning("The portal only opens for robots.");
        }

        void OnTriggerStay(Collider other)
        {
            // the mask may go on while the player is already standing in front of the portal
            var identity = other.GetComponentInParent<PlayerIdentity>();
            if (identity != null && identity.IsRobot) flow.CompleteRound();
        }
    }
}
