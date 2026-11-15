using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The mark over a robot's head: "?" while it is unsure what it saw, "!" once it is coming for
    /// the player. It always faces the player's camera and pops in with a small bounce.
    /// </summary>
    public class AlertIcon : MonoBehaviour
    {
        public Text label;
        public Color suspicious = new Color32(241, 170, 72, 255);
        public Color alarmed = new Color32(229, 87, 75, 255);
        public float size = 1f;

        float shownAt = -10f;
        bool showing;
        static Camera view;

        void Awake()
        {
            label.enabled = false;
        }

        // (each mark has its own sound, so a patrol noticing the player is heard even when it is behind them)
        public void Suspect()
        {
            if (!showing || label.text != "?") Sfx.Play(Sound.Suspect);
            Show("?", suspicious);
        }

        public void Alarm()
        {
            if (!showing || label.text != "!") Sfx.Play(Sound.Alarm);
            Show("!", alarmed);
        }
        /// <summary>Stars round the head: it has been knocked silly.</summary>
        public void Stun() => Show("* *", new Color32(150, 240, 255, 255));

        void Show(string mark, Color colour)
        {
            if (showing && label.text == mark) return;
            showing = true;
            label.text = mark;
            label.color = colour;
            label.enabled = true;
            shownAt = Time.time;
        }

        public void Hide()
        {
            showing = false;
            label.enabled = false;
        }

        void LateUpdate()
        {
            if (!showing) return;
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            if (view != null) transform.rotation = Quaternion.LookRotation(transform.position - view.transform.position);
            float k = Mathf.Clamp01((Time.time - shownAt) / 0.18f);
            transform.localScale = Vector3.one * size * (1f + 0.45f * Mathf.Sin(k * Mathf.PI));      // a quick bounce as it appears
        }
    }
}
