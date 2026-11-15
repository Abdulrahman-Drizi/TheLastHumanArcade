using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// How every panel of the interface arrives and leaves: a short slide from the nearest edge of
    /// the screen together with a fade, instead of popping in and out. Show(true / false) moves it;
    /// Pulse() replays the arrival when what the panel says has changed. It runs on real time, so
    /// it also works while the game is paused.
    /// </summary>
    public class UiFade : MonoBehaviour
    {
        public CanvasGroup group;
        public Vector2 slide = new Vector2(0f, -20f);   // where it comes in from, relative to where it rests
        public float seconds = 0.2f;

        RectTransform rect;
        Vector2 rest;
        float shown, target;
        bool ready;

        public bool Visible => target > 0.5f;

        /// <summary>The fader of a group (added the first time), sliding in from the screen edge the group is anchored to.</summary>
        public static UiFade For(CanvasGroup g)
        {
            var fade = g.GetComponent<UiFade>();
            if (fade != null) return fade;
            fade = g.gameObject.AddComponent<UiFade>();
            fade.group = g;
            var rt = (RectTransform)g.transform;
            var anchor = (rt.anchorMin + rt.anchorMax) / 2f;
            if (anchor.x < 0.3f) fade.slide = new Vector2(-28f, 0f);
            else if (anchor.x > 0.7f) fade.slide = new Vector2(28f, 0f);
            else fade.slide = new Vector2(0f, anchor.y > 0.6f ? 20f : -20f);
            return fade;
        }

        void Init()
        {
            rect = (RectTransform)transform;
            rest = rect.anchoredPosition;
            shown = target = group.alpha > 0.5f ? 1f : 0f;
            ready = true;
        }

        public void Show(bool on, bool instant = false)
        {
            if (!ready) Init();
            target = on ? 1f : 0f;
            if (instant) shown = target;
            Apply();
            enabled = shown != target;
        }

        /// <summary>What it says has changed: it steps back a little and arrives again.</summary>
        public void Pulse()
        {
            if (!ready) Init();
            if (target < 0.5f) return;
            shown = Mathf.Min(shown, 0.35f);
            enabled = true;
        }

        void Update()
        {
            if (!ready) Init();
            shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, seconds));
            Apply();
            if (shown == target) enabled = false;
        }

        void Apply()
        {
            float e = shown * shown * (3f - 2f * shown);
            group.alpha = e;
            rect.anchoredPosition = rest + slide * (1f - e);
        }
    }
}
