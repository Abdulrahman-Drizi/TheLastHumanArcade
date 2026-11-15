using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// The console beside the return portal. The portal is shut and dark until the mask links to
    /// this console and powers it up: E starts the link, which then loads by itself for a few
    /// seconds while the player stands by it (see EscapeFlow).
    /// </summary>
    public class PortalConsole : Interactable
    {
        public EscapeFlow flow;

        public override string Prompt => flow.CanPowerPortal ? "Link the mask and power the portal" : null;

        public override void Interact(PlayerInteractor player) => flow.PowerPortal(this);
    }
}
