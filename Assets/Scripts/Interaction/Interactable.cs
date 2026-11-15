using System.Collections.Generic;
using UnityEngine;

namespace TheLastHuman
{
    /// <summary>Something the player can use with E. Subclasses say what the prompt is and what happens.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public float range = 2.3f;

        /// <summary>Text after "E", e.g. "Pick up the mask". Null hides the prompt.</summary>
        public abstract string Prompt { get; }
        public virtual bool IsAvailable => true;
        public virtual Vector3 Position => transform.position;
        public abstract void Interact(PlayerInteractor player);

        /// <summary>
        /// More than zero makes this a held interaction: E has to stay down this many seconds (a bar
        /// fills) before Interact is called. Letting go, or looking away, starts it over.
        /// </summary>
        public virtual float HoldSeconds => 0f;
        /// <summary>What the bar says while E is held, e.g. "RIPPING OUT".</summary>
        public virtual string HoldLabel => "HOLD";
        /// <summary>E has just gone down on a held interaction: return false to refuse it (and say why).</summary>
        public virtual bool BeginHold(PlayerInteractor player) => true;
        /// <summary>Called every frame with how far the hold has got (0 to 1; 0 when it is dropped).</summary>
        public virtual void Holding(float progress) { }

        public static readonly List<Interactable> All = new List<Interactable>();

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);

        // Play Mode can start without reloading scripts, so static lists must be cleared by hand.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();
    }
}
