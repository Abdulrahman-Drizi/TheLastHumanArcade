using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The keypad post beside the electric gate on the 10th floor: E brings its keys up on screen.</summary>
    public class GateKeypad : Interactable
    {
        public KeypadUI keypad;
        /// <summary>Set once the gate is open: the keypad has nothing more to do.</summary>
        public bool Done { get; set; }

        public override string Prompt => Done || keypad.Open ? null : "Use the keypad";

        public override void Interact(PlayerInteractor player)
        {
            if (Done) return;
            keypad.Show();
        }
    }
}
