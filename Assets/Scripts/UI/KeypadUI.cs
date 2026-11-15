using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The electric gate's keypad, brought up on screen: four digits, typed by the player on the
    /// number keys (or clicked on its buttons). Backspace takes one back, Esc steps away from it.
    /// The fourth digit submits the code; whoever listens to Submitted answers with Accept or Deny.
    /// </summary>
    public class KeypadUI : MonoBehaviour
    {
        public CanvasGroup group;
        public Text[] slots;                 // the four digits
        public Text status;
        public Text worldDisplay;            // the little screen on the keypad itself, kept in step
        public FirstPersonController player;

        public bool Open { get; private set; }
        public event Action<string> Submitted;

        string typed = "";
        bool busy;
        float openedAt;
        static readonly Color Amber = new Color32(255, 168, 40, 255), Bad = new Color32(255, 84, 60, 255), Good = new Color32(120, 255, 235, 255);
        UiFade fade, panelFade;

        void Awake()
        {
            // the dim veil fades; the keypad itself slides up into place
            fade = UiFade.For(group);
            fade.slide = Vector2.zero;
            fade.Show(false, true);
            group.blocksRaycasts = false;
            var panel = transform.Find("Panel");
            if (panel != null)
            {
                var panelGroup = panel.GetComponent<CanvasGroup>();
                if (panelGroup == null) panelGroup = panel.gameObject.AddComponent<CanvasGroup>();
                panelFade = UiFade.For(panelGroup);
                panelFade.slide = new Vector2(0f, -40f);
            }
        }

        public void Show()
        {
            if (Open) return;
            Open = true;
            busy = false;
            openedAt = Time.unscaledTime;
            typed = "";
            status.text = "ENTER THE 4-DIGIT ACCESS CODE";
            status.color = Amber;
            Refresh();
            fade.Show(true);
            if (panelFade != null)
            {
                panelFade.Show(false, true);
                panelFade.Show(true);
            }
            group.blocksRaycasts = true;
            player.ControlEnabled = false;
            FirstPersonController.CaptureCursor(false);
        }

        /// <summary>Step away from the keypad (Esc, or the X button).</summary>
        public void Close() => Close(true);

        /// <summary>True while it is up, and for the frame it closes on (so the same Esc does not also open the pause menu).</summary>
        public bool Busy => Open || Time.frameCount == closedFrame;
        int closedFrame = -1;

        void Close(bool giveControlBack)
        {
            if (!Open) return;
            Open = false;
            closedFrame = Time.frameCount;
            fade.Show(false);
            group.blocksRaycasts = false;
            if (giveControlBack) player.ControlEnabled = true;      // (not when the gate's own scene has taken over)
            FirstPersonController.CaptureCursor(true);
        }

        /// <summary>A digit button (0 to 9).</summary>
        public void Press(int digit)
        {
            if (!Open || busy || typed.Length >= slots.Length) return;
            typed += (char)('0' + digit);
            Refresh();
            if (typed.Length == slots.Length) StartCoroutine(Submit());
        }

        // the buttons call these (a button's event cannot carry the digit itself)
        public void Press0() => Press(0);
        public void Press1() => Press(1);
        public void Press2() => Press(2);
        public void Press3() => Press(3);
        public void Press4() => Press(4);
        public void Press5() => Press(5);
        public void Press6() => Press(6);
        public void Press7() => Press(7);
        public void Press8() => Press(8);
        public void Press9() => Press(9);

        public void Backspace()
        {
            if (!Open || busy || typed.Length == 0) return;
            typed = typed.Substring(0, typed.Length - 1);
            Refresh();
        }

        IEnumerator Submit()
        {
            busy = true;
            status.text = "CHECKING...";
            yield return new WaitForSecondsRealtime(0.45f);
            Submitted?.Invoke(typed);
        }

        /// <summary>The code was right: show it, then get out of the way.</summary>
        public void Accept()
        {
            status.text = "ACCESS GRANTED";
            status.color = Good;
            foreach (var slot in slots) slot.color = Good;
            if (worldDisplay != null) { worldDisplay.text = "OPEN"; worldDisplay.color = Good; }
            StartCoroutine(CloseSoon());
        }

        IEnumerator CloseSoon()
        {
            yield return new WaitForSecondsRealtime(0.7f);
            Close(false);
        }

        /// <summary>The code was wrong: say so and clear it for another try.</summary>
        public void Deny()
        {
            StartCoroutine(Denied());
        }

        IEnumerator Denied()
        {
            status.text = "ACCESS DENIED";
            status.color = Bad;
            foreach (var slot in slots) slot.color = Bad;
            if (worldDisplay != null) worldDisplay.color = Bad;
            yield return new WaitForSecondsRealtime(0.9f);
            typed = "";
            status.text = "ENTER THE 4-DIGIT ACCESS CODE";
            status.color = Amber;
            busy = false;
            Refresh();
        }

        void Refresh()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].text = i < typed.Length ? typed[i].ToString() : "_";
                slots[i].color = Amber;
            }
            if (worldDisplay != null)
            {
                worldDisplay.text = typed.PadRight(slots.Length, '-');
                worldDisplay.color = Amber;
            }
        }

        void Update()
        {
            if (!Open || Time.unscaledTime - openedAt < 0.2f) return;
            var k = Keyboard.current;
            if (k == null) return;
            if (k.escapeKey.wasPressedThisFrame && !busy) { Close(); return; }
            if (k.backspaceKey.wasPressedThisFrame || k.deleteKey.wasPressedThisFrame) Backspace();
            for (int d = 0; d <= 9; d++)
                if (k[DigitKey(d)].wasPressedThisFrame || k[NumpadKey(d)].wasPressedThisFrame) Press(d);
        }

        static Key DigitKey(int d) => d == 0 ? Key.Digit0 : Key.Digit1 + (d - 1);
        static Key NumpadKey(int d) => Key.Numpad0 + d;
    }
}
