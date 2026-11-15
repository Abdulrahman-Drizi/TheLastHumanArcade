using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The lift at the far end of the facility. It only takes robots; reaching it masked ends the level.</summary>
    [RequireComponent(typeof(Collider))]
    public class Elevator : MonoBehaviour
    {
        public CityFlow flow;
        public HudController hud;

        void OnTriggerEnter(Collider other)
        {
            var identity = other.GetComponentInParent<PlayerIdentity>();
            if (identity == null) return;
            if (identity.IsRobot) flow.ReachElevator();
            else hud.ShowWarning("The elevator only answers robots. Put the mask on.");
        }

        void OnTriggerStay(Collider other)
        {
            // the mask may go on while the player is already standing in the doorway
            var identity = other.GetComponentInParent<PlayerIdentity>();
            if (identity != null && identity.IsRobot) flow.ReachElevator();
        }
    }
}
