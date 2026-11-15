using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The dodge bar shown in slow motion as the boss's fist comes down: a marker runs along the
    /// bar, and SPACE must be pressed while it is inside the lit window.
    /// </summary>
    public class DodgePrompt : MonoBehaviour
    {
        public CanvasGroup group;
        public RectTransform window;
        public RectTransform marker;
        public Image windowImage;
        public Text label;
        public Color ready = new Color32(120, 255, 235, 255);
        public Color missed = new Color32(255, 84, 60, 255);
        UiFade fade;

        void Awake()
        {
            fade = UiFade.For(group);
            fade.seconds = 0.12f;       // it has to be up at once: the blow is already coming
            fade.Show(false, true);
        }

        /// <summary>Show the bar with its window between two points along it (0 to 1).</summary>
        public void Show(float windowStart, float windowEnd)
        {
            window.anchorMin = new Vector2(windowStart, 0f);
            window.anchorMax = new Vector2(windowEnd, 1f);
            windowImage.color = new Color(ready.r, ready.g, ready.b, 0.55f);
            label.text = "DODGE";
            label.color = Color.white;
            SetProgress(0f);
            fade.Show(true);
        }

        public void SetProgress(float p)
        {
            p = Mathf.Clamp01(p);
            marker.anchorMin = new Vector2(p, 0f);
            marker.anchorMax = new Vector2(p, 1f);
        }

        /// <summary>Say how it went; the bar goes away shortly after.</summary>
        public void Result(bool dodged)
        {
            label.text = dodged ? "DODGED" : "TOO SLOW";
            label.color = dodged ? ready : missed;
            windowImage.color = dodged ? ready : new Color(missed.r, missed.g, missed.b, 0.6f);
        }

        public void Hide() => fade.Show(false);
    }
}
