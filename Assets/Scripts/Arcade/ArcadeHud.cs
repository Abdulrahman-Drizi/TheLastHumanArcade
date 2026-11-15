using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    public enum DisguiseState { Ready, Active, Cooling }

    /// <summary>
    /// The few readouts the arcade game adds to the shared HUD, each shown only while it matters:
    /// the disguise (ready / seconds left / recharging), the four batteries, a danger chip while a
    /// patrol is suspicious or coming, the colossus's tokens during the fight, one line of
    /// teaching when an input first becomes useful, and the loss and victory cards.
    /// It only displays what it is told.
    /// </summary>
    public class ArcadeHud : MonoBehaviour
    {
        [Header("Disguise (bottom centre)")]
        public CanvasGroup disguiseGroup;
        public Text disguiseLabel;
        public Image disguiseFill;
        public CanvasGroup visorCorners;        // four corner brackets round the picture while the disguise is on

        [Header("Batteries (under the objective)")]
        public CanvasGroup batteryGroup;
        public Image[] pips;                    // one per battery: hollow, carried (amber), in the gate (cyan)
        public RectTransform[] pipRects;

        [Header("Danger (top centre)")]
        public CanvasGroup dangerGroup;
        public Text dangerText;
        public Graphic dangerEdge;              // a red edge round the whole picture while a patrol is coming

        [Header("The colossus's tokens (top centre, during the fight)")]
        public CanvasGroup tokenGroup;
        public Image tokenFill;
        public Text tokenValue;

        [Header("One line of teaching, the opening title, and the two end cards")]
        public CanvasGroup hintGroup;
        public Text hintText;
        public CanvasGroup titleGroup;
        public CanvasGroup lossGroup;
        public Text lossTitle, lossBody;
        public CanvasGroup winGroup;

        public Color amber = new Color32(241, 170, 72, 255);
        public Color cyan = new Color32(106, 202, 197, 255);
        public Color red = new Color32(229, 87, 75, 255);
        public Color cream = new Color32(244, 236, 214, 255);
        public Color hollow = new Color32(244, 236, 214, 60);

        float hintUntil = -1f, titleUntil = -1f, edge, edgeTarget;
        DisguiseState shownState = DisguiseState.Ready;
        int shownCarried = -1, shownInserted = -1, danger;
        float[] pipPunch;

        static UiFade Fade(CanvasGroup group) => UiFade.For(group);

        void Awake()
        {
            foreach (var group in new[] { disguiseGroup, batteryGroup, dangerGroup, tokenGroup, hintGroup, titleGroup, lossGroup, winGroup })
            {
                group.blocksRaycasts = false;
                Fade(group).Show(false, true);
            }
            // the cards and the title are whole pictures: they fade, they do not slide
            Fade(lossGroup).slide = Vector2.zero;
            Fade(winGroup).slide = new Vector2(0f, -30f);
            Fade(titleGroup).slide = new Vector2(-30f, 0f);
            visorCorners.alpha = 0f;
            SetEdge(0f);
            pipPunch = new float[pips.Length];
        }

        void Update()
        {
            Fade(hintGroup).Show(Time.unscaledTime < hintUntil);
            Fade(titleGroup).Show(Time.unscaledTime < titleUntil);
            // the red edge breathes while something is coming for the player
            edge = Mathf.MoveTowards(edge, edgeTarget, Time.unscaledDeltaTime * 3f);
            SetEdge(edge * (danger >= 2 ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f) : 1f));
            for (int i = 0; i < pips.Length; i++)
            {
                pipPunch[i] = Mathf.MoveTowards(pipPunch[i], 0f, Time.unscaledDeltaTime * 4f);
                pipRects[i].localScale = Vector3.one * (1f + 0.6f * pipPunch[i] * pipPunch[i]);
            }
        }

        void SetEdge(float a)
        {
            if (dangerEdge == null) return;         // (optional: the game ships without it, the chip and the marks say enough)
            var c = red;
            c.a = Mathf.Clamp01(a);
            dangerEdge.color = c;
        }

        // ---------------------------------------------------------------- the disguise
        public void ShowDisguise(bool show)
        {
            Fade(disguiseGroup).Show(show);
            if (!show) visorCorners.alpha = 0f;
        }

        /// <summary>'fill' is the bar (time left while active, charge while cooling); 'seconds' is what is left of either.</summary>
        public void SetDisguise(DisguiseState state, float fill, float seconds)
        {
            Fade(disguiseGroup).Show(true);
            bool ending = state == DisguiseState.Active && seconds < 1.5f;
            disguiseFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            disguiseFill.color = state == DisguiseState.Cooling ? hollow : ending ? red : amber;
            disguiseLabel.text = state == DisguiseState.Ready ? "DISGUISE" : state == DisguiseState.Active ? "DISGUISED  " + seconds.ToString("0.0") : "RECHARGING";
            disguiseLabel.color = state == DisguiseState.Cooling ? hollow : ending ? red : cream;
            if (state != shownState)
            {
                shownState = state;
                Fade(disguiseGroup).Pulse();
            }
            // the brackets are steady while it is safe, and blink for the last second and a half
            float want = state != DisguiseState.Active ? 0f : ending ? (Mathf.Repeat(Time.time, 0.3f) < 0.15f ? 1f : 0.25f) : 1f;
            visorCorners.alpha = Mathf.MoveTowards(visorCorners.alpha, want, Time.deltaTime * 10f);
            foreach (var g in visorCorners.GetComponentsInChildren<Graphic>())
            {
                var c = ending ? red : amber;
                c.a = g.color.a;
                g.color = c;
            }
        }

        // ---------------------------------------------------------------- batteries
        public void ShowBatteries(bool show) => Fade(batteryGroup).Show(show);

        /// <summary>How many batteries are in the player's hands and how many are already in the gate.</summary>
        public void SetBatteries(int carried, int inserted)
        {
            if (carried == shownCarried && inserted == shownInserted) return;
            for (int i = 0; i < pips.Length; i++)
            {
                bool wasLit = shownInserted >= 0 && i < shownInserted + shownCarried;
                bool inGate = i < inserted, held = i < inserted + carried;
                pips[i].color = inGate ? cyan : held ? amber : hollow;
                bool changed = held != wasLit || (inGate && i >= shownInserted);
                if (changed && shownCarried >= 0) pipPunch[i] = 1f;       // the one that changed jumps
            }
            shownCarried = carried;
            shownInserted = inserted;
        }

        // ---------------------------------------------------------------- danger
        /// <summary>0 nothing, 1 a patrol is suspicious ("?"), 2 a patrol is coming ("!").</summary>
        public void SetDanger(int level)
        {
            if (level == danger) return;
            danger = level;
            Fade(dangerGroup).Show(level > 0);
            if (level > 0)
            {
                dangerText.text = level == 1 ? "?   SEEN  -  BREAK ITS LINE OF SIGHT" : "!   IT IS COMING  -  HIDE OR DISGUISE";
                dangerText.color = level == 1 ? amber : red;
                Fade(dangerGroup).Pulse();
            }
            edgeTarget = level == 2 ? 0.85f : level == 1 ? 0.25f : 0f;
        }

        // ---------------------------------------------------------------- tokens
        public void ShowTokens(bool show) => Fade(tokenGroup).Show(show);

        public void SetTokens(float share)
        {
            share = Mathf.Clamp01(share);
            tokenFill.rectTransform.anchorMax = new Vector2(share, 1f);
            tokenFill.color = share < 0.25f ? red : amber;
            tokenValue.text = Mathf.CeilToInt(share * 100f) + "%";
        }

        // ---------------------------------------------------------------- hint, title, cards
        /// <summary>One line of teaching at the bottom of the screen, for a few seconds.</summary>
        public void Hint(string line, float seconds)
        {
            if (Fade(hintGroup).Visible && hintText.text != line) Fade(hintGroup).Pulse();
            hintText.text = line;
            hintUntil = Time.unscaledTime + seconds;
        }

        public void HideHint() => hintUntil = -1f;

        public void ShowTitle(float seconds) => titleUntil = Time.unscaledTime + seconds;

        public void ShowLoss(string title, string body)
        {
            lossTitle.text = title;
            lossBody.text = body;
            lossGroup.blocksRaycasts = true;
            Fade(lossGroup).seconds = 0.12f;
            Fade(lossGroup).Show(true);
        }

        public void ShowWin()
        {
            winGroup.blocksRaycasts = true;
            Fade(winGroup).seconds = 0.6f;
            Fade(winGroup).Show(true);
        }
    }
}
