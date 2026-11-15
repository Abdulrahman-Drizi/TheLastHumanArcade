using System;
using UnityEngine;

namespace TheLastHuman
{
    public enum PlayerForm { Human, Robot }

    /// <summary>What the player currently counts as, as far as the robots are concerned.</summary>
    public class PlayerIdentity : MonoBehaviour
    {
        public PlayerForm Form { get; private set; } = PlayerForm.Human;
        public bool IsHuman => Form == PlayerForm.Human;
        public bool IsRobot => Form == PlayerForm.Robot;
        public event Action<PlayerForm> FormChanged;

        /// <summary>The mask is running out: robots can see the human underneath.</summary>
        public bool Exposed { get; set; }

        /// <summary>Tucked away in a hiding spot that no robot saw the player enter.</summary>
        public bool Hidden => hider != null;

        /// <summary>Robots treat the player as a human they could spot.</summary>
        public bool LooksHuman => (IsHuman || Exposed) && !Hidden;

        UnityEngine.Object hider;

        public void SetForm(PlayerForm form)
        {
            if (Form == form) return;
            Form = form;
            FormChanged?.Invoke(form);
        }

        /// <summary>A hiding spot claims or releases the player.</summary>
        public void SetHidden(UnityEngine.Object spot, bool hidden)
        {
            if (hidden) hider = spot;
            else if (hider == spot) hider = null;
        }
    }
}
