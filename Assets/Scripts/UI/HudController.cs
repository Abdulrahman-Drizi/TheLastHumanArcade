using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// Everything drawn over the game: the instruction box (top left), the objective arrow, the E
    /// prompt, warnings, form / robot / mask readouts, the stance plate, the hold-to-remove bar, the
    /// fade, the glitch overlay and the end-of-round panel. It has two looks: warm and plain for
    /// human eyes, cold blue with telemetry through the mask's visor (see <see cref="VisorHud"/>).
    /// It only displays what it is told; it holds no game rules.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        [Header("Instruction box (top left)")]
        public CanvasGroup commsGroup;
        public Text commsHeader;
        public Text commsBody;
        public Image commsAccent;
        public float typeSpeed = 34f;           // characters a second: the mission line is typed at a pace that can be read
        public float speechSpeed = 30f;         // and so is speech

        [Header("Interaction prompt")]
        public CanvasGroup promptGroup;
        public Text promptText;

        [Header("Warning")]
        public CanvasGroup warningGroup;
        public Text warningText;

        [Header("Status (bottom left): how the robots treat the player. Human eyes only; the visor has its own row")]
        public CanvasGroup watchGroup;
        public Text watchText;
        public Text captureHint;

        [Header("Key hints and dialogue")]
        public CanvasGroup driveKeys;           // W S A D key caps, shown for a moment on getting into the car
        public GameObject maskKey;              // the Q key cap beside the mask clock, shown when the mask can go on
        public CanvasGroup sayGroup;
        public Text saySpeaker;
        public Text sayLine;

        [Header("Boss fight: hearts (top right)")]
        public CanvasGroup heartsGroup;
        public Image[] hearts;

        [Header("Notice (a good-news banner, top centre)")]
        public CanvasGroup noticeGroup;
        public Text noticeText;
        public Text[] completeTexts;            // title, body, note of the end card

        [Header("Held action (hold E): a labelled bar")]
        public CanvasGroup actionGroup;
        public Text actionText;
        public Image actionFill;

        [Header("Caption: one line under the picture during a cinematic")]
        public CanvasGroup captionGroup;
        public Text captionText;

        [Header("Deletion wave (the escape): how far behind it is")]
        public CanvasGroup waveGroup;
        public Text waveText;
        public Image waveFill;

        [Header("Mask clock and hold-to-remove bar")]
        public CanvasGroup maskGroup;
        public Text maskLabel;
        public Image maskFill;
        public CanvasGroup holdGroup;
        public Image holdFill;

        [Header("Objective arrow (top centre)")]
        public ObjectiveArrow objective;

        [Header("Whole gameplay HUD (hidden during story cards and the intro flight)")]
        public CanvasGroup gameplayGroup;
        public RectTransform letterboxTop;
        public RectTransform letterboxBottom;
        public float letterboxHeight = 110f;

        [Header("Stance (bottom right)")]
        public Image stancePanel;
        public Text stanceText;
        public GameObject standIcon, crouchIcon, tuckIcon;
        public Graphic[] stanceGraphics;        // everything on the stance plate that takes the stance colour

        [Header("Masked: the machine visor")]
        public VisorHud visor;
        public Graphic[] accentGraphics;        // amber in human vision, blue through the visor
        public Graphic[] textGraphics;          // cream in human vision, ice blue through the visor
        public Color visorAccent = new Color32(70, 180, 255, 255);
        public Color visorText = new Color32(205, 232, 255, 255);
        // through the visor every plate takes the look of the telemetry panel: navy, thin blue frame, small blue title
        public Image[] plates;
        public Color humanPlate = new Color32(8, 24, 26, 205);
        public Color visorPlate = new Color32(6, 20, 44, 215);
        public GameObject[] visorOnly;          // the blue frames and hairlines
        public GameObject[] humanOnly;          // the warm look's own trim

        [Header("Overlays")]
        public Image fade;
        public RectTransform glitchRoot;
        public GameObject completePanel;

        public Color cream = new Color32(244, 236, 214, 255);
        public Color amber = new Color32(255, 168, 40, 255);
        public Color alert = new Color32(255, 84, 60, 255);
        public Color teal = new Color32(90, 200, 190, 255);
        public Color hideGreen = new Color32(120, 255, 235, 255);      // the palette's mint: safe

        Coroutine typing, warning, glitch;
        Image[] glitchBars;
        string currentPrompt;
        float gameplayTarget = 1f, letterboxTarget, letterbox;
        bool visorOn, stanceShown, stanceAllowed = true;
        float driveKeysUntil = -1f, noticeUntil = -1f;
        bool captionShown;
        Stance stance;

        Color Accent => visorOn ? visorAccent : amber;
        Color Body => visorOn ? visorText : cream;
        Color Calm => visorOn ? visorAccent : teal;

        // every panel comes and goes with a short slide and fade (see UiFade), never a pop
        readonly Dictionary<CanvasGroup, UiFade> fades = new Dictionary<CanvasGroup, UiFade>();
        CanvasGroup stanceGroup;

        UiFade Fader(CanvasGroup group)
        {
            if (!fades.TryGetValue(group, out var fade))
            {
                fade = UiFade.For(group);
                fades[group] = fade;
            }
            return fade;
        }

        void Show(CanvasGroup group, bool on, bool instant = false) => Fader(group).Show(on, instant);

        void Awake()
        {
            glitchBars = glitchRoot.GetComponentsInChildren<Image>(true);
            glitchRoot.gameObject.SetActive(false);
            completePanel.SetActive(false);
            maskKey.SetActive(false);
            stanceGroup = stancePanel.GetComponent<CanvasGroup>();
            if (stanceGroup == null) stanceGroup = stancePanel.gameObject.AddComponent<CanvasGroup>();
            stanceGroup.blocksRaycasts = false;
            foreach (var group in new[] { warningGroup, sayGroup, heartsGroup, noticeGroup, actionGroup, captionGroup, waveGroup,
                                          driveKeys, stanceGroup, promptGroup, maskGroup, holdGroup })
                Show(group, false, true);
            foreach (var go in visorOnly) go.SetActive(false);
        }

        void Update()
        {
            gameplayGroup.alpha = Mathf.MoveTowards(gameplayGroup.alpha, gameplayTarget, Time.deltaTime * 2.5f);
            letterbox = Mathf.MoveTowards(letterbox, letterboxTarget, Time.deltaTime * 1.6f);
            float h = letterboxHeight * letterbox * letterbox * (3f - 2f * letterbox);
            letterboxTop.sizeDelta = new Vector2(0f, h);
            letterboxBottom.sizeDelta = new Vector2(0f, h);
            Show(driveKeys, Time.time < driveKeysUntil);
            Show(noticeGroup, Time.unscaledTime < noticeUntil);
            Show(captionGroup, captionShown);
        }

        /// <summary>One line of text under the picture (cinematics); null takes it away.</summary>
        public void SetCaption(string line)
        {
            bool had = captionShown;
            captionShown = !string.IsNullOrEmpty(line);
            if (!captionShown) return;
            if (had && captionText.text != line) Fader(captionGroup).Pulse();
            captionText.text = line;
        }

        /// <summary>Progress of a held action (0..1) under its label; a negative value hides the bar.</summary>
        public void SetAction(string label, float progress)
        {
            Show(actionGroup, progress >= 0f);
            if (progress < 0f) return;
            actionText.text = label;
            actionFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        /// <summary>The deletion wave is this many metres behind (negative hides the readout).</summary>
        public void SetWave(float metres)
        {
            Show(waveGroup, metres >= 0f);
            if (metres < 0f) return;
            bool close = metres < 25f;
            waveText.text = "DELETION WAVE   " + Mathf.RoundToInt(metres) + " M BEHIND";
            waveText.color = close ? alert : Body;
            waveFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(1f - metres / 80f), 1f);    // the bar fills as it closes in
            waveFill.color = close ? alert : Accent;
        }

        /// <summary>Show or hide the gameplay HUD (instructions, minimap, status, prompts).</summary>
        public void SetGameplayVisible(bool visible, bool instant = false)
        {
            gameplayTarget = visible ? 1f : 0f;
            if (instant) gameplayGroup.alpha = gameplayTarget;
        }

        /// <summary>Point the top arrow at the current mission target (null hides it).</summary>
        public void SetObjective(Transform target, string name) => objective.SetTarget(target, name);

        /// <summary>Black bars top and bottom for the intro flight.</summary>
        public void SetLetterbox(bool on, bool instant = false)
        {
            letterboxTarget = on ? 1f : 0f;
            if (instant) letterbox = letterboxTarget;
        }

        // ---------------------------------------------------------------- instruction box
        /// <summary>The instruction currently shown in the top-left box (the pause menu repeats it).</summary>
        public string MissionText { get; private set; } = "";

        public void SetComms(string header, string body, bool retype = true)
        {
            bool changed = body != MissionText;
            MissionText = body;
            commsHeader.text = header;
            if (typing != null) StopCoroutine(typing);
            if (retype)
            {
                if (changed && commsGroup != null) Fader(commsGroup).Pulse();
                typing = StartCoroutine(Type(body));
            }
            else commsBody.text = body;
        }

        IEnumerator Type(string body)
        {
            commsAccent.color = Accent;
            for (float shown = 0f; shown < body.Length; shown += typeSpeed * Time.deltaTime)
            {
                commsBody.text = body.Substring(0, Mathf.Min(body.Length, Mathf.CeilToInt(shown)));
                yield return null;
            }
            commsBody.text = body;
            typing = null;
        }

        // ---------------------------------------------------------------- prompt and warning
        public void SetPrompt(string action)
        {
            if (action == currentPrompt) return;
            bool swap = currentPrompt != null && action != null;
            currentPrompt = action;
            if (action != null) promptText.text = action.ToUpperInvariant();     // the E key cap sits beside it
            Show(promptGroup, action != null);
            if (swap) Fader(promptGroup).Pulse();
        }

        public void ShowWarning(string message, float seconds = 2.6f)
        {
            if (warning != null) StopCoroutine(warning);
            warning = StartCoroutine(Warn(message, seconds));
        }

        /// <summary>Take the warning down at once (e.g. the mask came off, so its warning no longer applies).</summary>
        public void HideWarning()
        {
            if (warning != null) StopCoroutine(warning);
            warning = null;
            Show(warningGroup, false);
        }

        IEnumerator Warn(string message, float seconds)
        {
            bool swap = Fader(warningGroup).Visible && warningText.text != message;
            warningText.text = message;
            Show(warningGroup, true);
            if (swap) Fader(warningGroup).Pulse();
            yield return new WaitForSeconds(seconds);
            Show(warningGroup, false);
            warning = null;
        }

        // ---------------------------------------------------------------- status
        /// <summary>
        /// Human eyes or the mask's visor. Human vision is warm and plain. Through the visor the
        /// whole HUD takes the look of the telemetry panel: blue on navy plates with thin frames,
        /// and the mask's battery and the robots' attention become rows of that panel.
        /// </summary>
        public void SetForm(PlayerForm form)
        {
            bool was = visorOn;
            visorOn = form == PlayerForm.Robot;
            foreach (var g in accentGraphics) Tint(g, Accent);
            foreach (var g in textGraphics) Tint(g, Body);
            foreach (var plate in plates) plate.color = visorOn ? visorPlate : humanPlate;
            foreach (var go in visorOnly) go.SetActive(visorOn);
            foreach (var go in humanOnly) go.SetActive(!visorOn);
            objective.SetColour(Accent);
            if (visor != null) visor.Show(visorOn);
            Show(watchGroup, !visorOn);
            if (visorOn) Show(maskGroup, false);
            if (was != visorOn && commsGroup != null) Fader(commsGroup).Pulse();
            stanceShown = false;        // repaint the stance plate in the new colours
            SetStance(stance);
        }

        static void Tint(Graphic g, Color c)
        {
            c.a = g.color.a;
            g.color = c;
        }

        /// <summary>One line saying how the robots currently treat the player.</summary>
        public void SetRobotStatus(bool chased, bool watched, bool hidden, bool masked)
        {
            string say;
            bool bad = false;
            if (chased) { say = "CHASING YOU"; bad = true; }
            else if (hidden) say = "CANNOT SEE YOU";
            else if (masked) say = "IGNORING YOU";
            else if (watched) { say = "WATCHING"; bad = true; }
            else say = "NOT LOOKING";
            string line = "ROBOTS  " + say + (hidden && !chased ? " (HIDDEN)" : "");
            if (watchText.text != line)
            {
                watchText.text = line;
                if (!visorOn) Fader(watchGroup).Pulse();
            }
            watchText.color = bad ? alert : Calm;
            if (visor != null)
            {
                visor.SetRobots(say, bad);
                visor.SetStress(chased || (watched && !masked && !hidden));
            }
        }

        /// <summary>The stance plate is never shown while the player is not on foot (driving).</summary>
        public void ShowStance(bool show)
        {
            stanceAllowed = show;
            RefreshStancePlate();
        }

        /// <summary>The plate (with its key caps) only appears while the player is actually down; standing shows nothing.</summary>
        void RefreshStancePlate() => Show(stanceGroup, stanceAllowed && stance != Stance.Standing);

        /// <summary>The stance plate: a pictogram and one word, recoloured so a crouch cannot be missed.</summary>
        public void SetStance(Stance newStance)
        {
            if (stanceShown && newStance == stance) return;
            stance = newStance;
            stanceShown = true;
            RefreshStancePlate();
            standIcon.SetActive(stance == Stance.Standing);
            crouchIcon.SetActive(stance == Stance.Crouching);
            tuckIcon.SetActive(stance == Stance.Tucked);
            stanceText.text = stance == Stance.Standing ? "STANDING" : stance == Stance.Crouching ? "CROUCHING" : "LYING LOW";
            var colour = stance == Stance.Standing ? Body : stance == Stance.Crouching ? amber : hideGreen;
            foreach (var g in stanceGraphics) Tint(g, colour);
            // the plate itself fills in when the player is down, so the change reads at a glance
            var plate = stance == Stance.Standing ? new Color(0.03f, 0.09f, 0.1f, 0.8f) : colour * 0.28f;
            plate.a = stance == Stance.Standing ? 0.8f : 0.92f;
            stancePanel.color = plate;
        }

        /// <summary>Show the driving keys (bottom right) for a few seconds, then let them fade.</summary>
        public void ShowDriveKeys(float seconds) => driveKeysUntil = Time.time + seconds;

        // ---------------------------------------------------------------- dialogue
        /// <summary>A line of speech at the bottom of the screen: typed out, held, then cleared.</summary>
        public IEnumerator Say(string speaker, string line, float holdSeconds)
        {
            bool swap = Fader(sayGroup).Visible && saySpeaker.text != speaker;
            saySpeaker.text = speaker;
            sayLine.text = "";
            Show(sayGroup, true);
            if (swap) Fader(sayGroup).Pulse();      // a new voice: the box steps back and comes in again
            for (float shown = 0f; shown < line.Length; shown += speechSpeed * Time.deltaTime)
            {
                sayLine.text = line.Substring(0, Mathf.Min(line.Length, Mathf.CeilToInt(shown)));
                yield return null;
            }
            sayLine.text = line;
            yield return new WaitForSeconds(holdSeconds);
            Show(sayGroup, false);
        }

        public void HideSay() => Show(sayGroup, false);

        // ---------------------------------------------------------------- hearts and notices
        public void ShowHearts(bool show) => Show(heartsGroup, show);

        /// <summary>How many of the hearts are still full; the rest are shown empty.</summary>
        public void SetHearts(int full)
        {
            for (int i = 0; i < hearts.Length; i++)
                hearts[i].color = i < full ? alert : new Color(0.25f, 0.28f, 0.32f, 0.7f);
        }

        /// <summary>A banner of good news (an unlock), shown for a few seconds.</summary>
        public void ShowNotice(string message, float seconds = 4f)
        {
            if (Fader(noticeGroup).Visible && noticeText.text != message) Fader(noticeGroup).Pulse();
            noticeText.text = message;
            noticeUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Rewrite the end card (each level and the finale word it differently).</summary>
        public void SetCompleteText(string title, string body, string note)
        {
            completeTexts[0].text = title;
            completeTexts[1].text = body;
            completeTexts[2].text = note;
        }

        public void SetCaptureHint(bool show)
        {
            if (captureHint.gameObject.activeSelf != show) captureHint.gameObject.SetActive(show);
        }

        // ---------------------------------------------------------------- mask
        /// <summary>
        /// The mask clock: a label and a bar from 0 to 1. 'urgent' turns it red (the battery is low).
        /// While the mask is worn the bar is its battery, shown as a row of the visor's telemetry
        /// instead; 'exposed' says an X-ray has shown the human under it.
        /// </summary>
        public void SetMask(string label, float fill, bool urgent, bool showKey = false, bool exposed = false)
        {
            if (maskKey.activeSelf != showKey) maskKey.SetActive(showKey);
            Show(maskGroup, !visorOn);
            maskLabel.text = label;
            maskLabel.color = urgent ? alert : Accent;
            maskFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(fill), 1f);
            maskFill.color = urgent ? alert : Accent;
            if (visorOn && visor != null) visor.SetCharge(fill, exposed);
        }

        public void HideMask() => Show(maskGroup, false);

        /// <summary>Progress of holding E to take the mask off (0..1); a negative value hides the bar.</summary>
        public void SetHold(float progress)
        {
            Show(holdGroup, progress >= 0f);
            if (progress >= 0f) holdFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        }

        // ---------------------------------------------------------------- overlays
        public IEnumerator Fade(float from, float to, float seconds)
        {
            fade.raycastTarget = false;
            for (float t = 0; t < 1f; t += Time.deltaTime / seconds)
            {
                SetFade(Mathf.Lerp(from, to, t));
                yield return null;
            }
            SetFade(to);
        }

        void SetFade(float a)
        {
            var c = fade.color;
            c.a = a;
            fade.color = c;
        }

        /// <summary>Sliding bars and dropout blocks across the screen for a moment.</summary>
        public void PlayGlitch(float seconds, float strength = 1f)
        {
            if (glitch != null) StopCoroutine(glitch);
            glitch = StartCoroutine(Glitch(seconds, strength));
        }

        IEnumerator Glitch(float seconds, float amount)
        {
            glitchRoot.gameObject.SetActive(true);
            var size = ((RectTransform)glitchRoot.parent).rect.size;
            Color[] tints = { new Color(1f, 0.33f, 0.2f), new Color(0.2f, 0.9f, 0.9f), new Color(0.02f, 0.03f, 0.04f), amber };
            for (float t = 0; t < seconds; t += 0.045f)
            {
                float strength = Mathf.Sin(Mathf.Clamp01(t / seconds) * Mathf.PI) * amount;   // ramps up, then clears
                if (amount < 1f) strength = Mathf.Max(strength, amount * 0.5f);              // a sustained warning never fully clears
                for (int i = 0; i < glitchBars.Length; i++)
                {
                    var bar = glitchBars[i];
                    bool on = Random.value < 0.25f * amount + 0.65f * strength;
                    bar.enabled = on;
                    if (!on) continue;
                    bool block = i % 3 == 0;
                    var rt = bar.rectTransform;
                    rt.sizeDelta = block
                        ? new Vector2(24f * Random.Range(2, 9), 24f * Random.Range(1, 4))
                        : new Vector2(size.x * Random.Range(0.2f, 1f), Random.Range(2f, 6f + 34f * strength));
                    rt.anchoredPosition = new Vector2(Random.Range(-0.5f, 0.5f) * size.x, Random.Range(-0.5f, 0.5f) * size.y);
                    var c = tints[Random.Range(0, tints.Length)];
                    c.a = block ? Random.Range(0.55f, 0.95f) : Random.Range(0.25f, 0.6f);
                    bar.color = c;
                }
                yield return new WaitForSeconds(0.045f);
            }
            glitchRoot.gameObject.SetActive(false);
            glitch = null;
        }

        public void ShowComplete()
        {
            completePanel.SetActive(true);
            Show(promptGroup, false, true);
            Show(maskGroup, false, true);
            Show(holdGroup, false, true);
        }
    }
}
