using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The computer on the 10th floor. It stays locked until the Face Scan across the room has been
    /// passed; tried before that, it refuses ("Access denied: Scan face first"). Once unlocked, E
    /// sits the player down at its desktop, as often as they like until the gate is open.
    /// </summary>
    public class DeskComputer : Interactable
    {
        public TowerSequence tower;

        public override string Prompt => tower.DeskFree ? "Use the workstation" : null;

        public override void Interact(PlayerInteractor player) => tower.UseWorkstation();
    }
}
