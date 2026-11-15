using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The Face Scan on the 10th floor: E holds the robot mask up to it. It is its own piece of
    /// hardware, across the room from the workstation it unlocks.
    /// </summary>
    public class FaceScanTerminal : Interactable
    {
        public TowerSequence tower;
        public bool Used { get; set; }

        public override string Prompt => Used ? null : "Scan the mask";

        public override void Interact(PlayerInteractor player)
        {
            if (Used) return;
            Used = true;
            tower.BeginFaceScan();
        }
    }
}
