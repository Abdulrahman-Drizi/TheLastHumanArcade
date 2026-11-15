using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The card that introduces a parkour move the first time it is needed: its name, the keys (as
    /// key caps), one line of instruction, and a small looping animation of the move, drawn with a
    /// stick figure, so the action can be read at a glance without stopping the game.
    /// It goes away once the move has been done, or after a few seconds.
    /// </summary>
    public class TutorialCard : MonoBehaviour
    {
        public CanvasGroup group;
        public Text title;
        public Text instruction;
        public GameObject[] keyRows;            // one row of key caps per TutorialKind (index = kind - 1)

        [Header("The animated demo")]
        public RectTransform figure;
        public RectTransform rope;
        public GameObject jumpSet, slideSet, wallSet, grappleSet;      // the scenery of each demo
        public RectTransform key;               // a key cap that is pressed in time with the move
        public float loopSeconds = 1.9f;
        public float showSeconds = 9f;

        TutorialKind kind;
        float until;

        static readonly string[] Titles = { "", "JUMP", "SLIDE", "WALL RUN", "GRAPPLE" };
        static readonly string[] Lines =
        {
            "",
            "Run at the gap and jump.",
            "Hold while running: low, fast, and faster downhill.",
            "Jump at a lit wall and keep running. Jump again to kick off.",
            "Look at an anchor and hold. Swing, then let go.",
        };

        UiFade fade;

        void Awake()
        {
            fade = UiFade.For(group);       // it slides in from the right edge, where it sits
            fade.seconds = 0.3f;
            fade.Show(false, true);
        }

        public void Show(TutorialKind which)
        {
            if (which == TutorialKind.None) return;
            kind = which;
            title.text = Titles[(int)which];
            instruction.text = Lines[(int)which];
            for (int i = 0; i < keyRows.Length; i++) keyRows[i].SetActive(i == (int)which - 1);
            jumpSet.SetActive(which == TutorialKind.Jump);
            slideSet.SetActive(which == TutorialKind.Slide);
            wallSet.SetActive(which == TutorialKind.WallRun);
            grappleSet.SetActive(which == TutorialKind.Grapple);
            rope.gameObject.SetActive(which == TutorialKind.Grapple);
            until = Time.unscaledTime + showSeconds;
        }

        /// <summary>The player has just done this move: the card has served its purpose.</summary>
        public void Done(TutorialKind which)
        {
            if (which == kind) until = Mathf.Min(until, Time.unscaledTime + 1.2f);
        }

        void Update()
        {
            bool show = kind != TutorialKind.None && Time.unscaledTime < until;
            if (fade.Visible != show) fade.Show(show);
            if (group.alpha <= 0f) return;

            float u = Mathf.Repeat(Time.unscaledTime, loopSeconds) / loopSeconds;      // 0..1 through the loop
            Vector2 at = Vector2.zero;
            float turn = 0f, squash = 1f;
            bool pressed = false;
            switch (kind)
            {
                case TutorialKind.Jump:             // run, leap the gap in an arc, run on
                {
                    at.x = Mathf.Lerp(-78f, 78f, u);
                    float air = Mathf.InverseLerp(0.32f, 0.68f, u);
                    bool flying = u > 0.32f && u < 0.68f;
                    at.y = -22f + (flying ? Mathf.Sin(air * Mathf.PI) * 34f : 0f);
                    pressed = u > 0.28f && u < 0.42f;
                    break;
                }
                case TutorialKind.Slide:            // upright for a moment, then down low along the slope
                {
                    at = Vector2.Lerp(new Vector2(-74f, 26f), new Vector2(74f, -30f), u);
                    bool low = u > 0.22f;
                    squash = low ? 0.5f : 1f;
                    turn = low ? -70f : -8f;
                    at.y += low ? -4f : 8f;
                    pressed = low;
                    break;
                }
                case TutorialKind.WallRun:          // along the wall, leaning, then a kick off the far end
                {
                    bool on = u > 0.15f && u < 0.72f;
                    at.x = Mathf.Lerp(-80f, 80f, u);
                    at.y = on ? 6f - 10f * Mathf.InverseLerp(0.15f, 0.72f, u)
                         : u >= 0.72f ? -4f + Mathf.Sin(Mathf.InverseLerp(0.72f, 1f, u) * Mathf.PI) * 22f - 22f * Mathf.InverseLerp(0.72f, 1f, u)
                         : -24f + 30f * Mathf.InverseLerp(0f, 0.15f, u);
                    turn = on ? -24f : 0f;
                    pressed = (u > 0.08f && u < 0.2f) || (u > 0.68f && u < 0.8f);
                    break;
                }
                case TutorialKind.Grapple:          // hang from the anchor and swing through
                {
                    float angle = Mathf.Lerp(-58f, 58f, 0.5f - 0.5f * Mathf.Cos(u * Mathf.PI * 2f));
                    const float length = 62f;
                    var anchor = new Vector2(0f, 46f);
                    at = anchor + new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad), -Mathf.Cos(angle * Mathf.Deg2Rad)) * length;
                    turn = angle * 0.6f;
                    rope.anchoredPosition = anchor;
                    rope.sizeDelta = new Vector2(2f, length);
                    rope.localRotation = Quaternion.Euler(0f, 0f, angle);
                    pressed = true;
                    break;
                }
            }
            figure.anchoredPosition = at;
            figure.localRotation = Quaternion.Euler(0f, 0f, turn);
            figure.localScale = new Vector3(1f, squash, 1f);
            // the key cap dips while it should be held
            key.localScale = Vector3.one * (pressed ? 0.86f : 1f);
            key.anchoredPosition = new Vector2(key.anchoredPosition.x, pressed ? -3f : 0f);
        }
    }
}
