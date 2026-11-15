using System;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>The keypad beside the Monolith's main door. Using it opens the hacking terminal.</summary>
    public class PasswordTerminal : Interactable
    {
        public HackTerminal terminal;
        public event Action Opened;

        public override string Prompt => terminal.Open || terminal.Cracked ? null : "Use the keypad";

        public override void Interact(PlayerInteractor player)
        {
            if (terminal.Open || terminal.Cracked) return;
            terminal.Show();
            Opened?.Invoke();
        }
    }
}
