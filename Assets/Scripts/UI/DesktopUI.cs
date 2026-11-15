using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The workstation's desktop on the 10th floor: a few of the robots' own files (which a courier
    /// unit may not open) and one folder that matters, "PASSWORDS". Inside it is Password.txt, and
    /// inside that the gate's 4-digit code: one of a handful of lazy patterns, picked when the
    /// desktop is first opened and kept, so the file can be read again.
    ///
    /// It is worked with the mouse, like a desktop: an on-screen pointer follows the mouse, and
    /// things open only when they are clicked. A click presses the icon in, the folder's flap drops
    /// open, and its window grows out of the icon. No key opens anything.
    /// The code is not sent anywhere: the player has to remember it and type it in at the gate.
    /// </summary>
    public class DesktopUI : MonoBehaviour
    {
        public CanvasGroup group;
        public GameObject folderIcon;        // on the desktop
        public RectTransform folderFlap;     // the front of the folder: it drops as the folder opens
        public GameObject folderWindow;      // lists Password.txt
        public RectTransform fileRow;        // that file's line in the folder window
        public GameObject fileWindow;        // shows the password
        public Text passwordText;
        public Text hint;
        public Text denied;                  // "ACCESS DENIED", flashed when another file is tried
        public RectTransform pointer;        // the mouse pointer drawn on the desktop (its tip is its top-left corner)

        public bool Open { get; private set; }
        public string Password { get; private set; }
        /// <summary>The player has read the password and closed the desktop.</summary>
        public event Action<string> Viewed;

        /// <summary>The sort of code a machine that has never been robbed would choose.</summary>
        public static readonly string[] Patterns =
        {
            "1122", "1212", "1221", "2211", "1234", "4321", "1357", "2468", "3344", "5566", "7788", "9900",
        };

        int step;                             // 0 desktop, 1 folder open, 2 file open
        float steppedAt, deniedUntil, pressed;
        UiFade fade;
        RectTransform rect;

        void Awake()
        {
            rect = (RectTransform)transform;
            fade = UiFade.For(group);
            fade.slide = Vector2.zero;        // a whole screen: it only fades
            fade.seconds = 0.3f;
            fade.Show(false, true);
            group.blocksRaycasts = false;
            if (denied != null) denied.enabled = false;
        }

        public void Show()
        {
            if (string.IsNullOrEmpty(Password)) Password = Patterns[UnityEngine.Random.Range(0, Patterns.Length)];
            passwordText.text = Password;
            Open = true;
            StopAllCoroutines();
            SetStep(0);
            folderWindow.SetActive(false);
            fileWindow.SetActive(false);
            if (folderFlap != null) folderFlap.localScale = Vector3.one;
            folderIcon.transform.localScale = Vector3.one;
            fade.Show(true);
            group.blocksRaycasts = true;
            FirstPersonController.CaptureCursor(false);
            Cursor.visible = false;           // the desktop draws its own pointer
            MovePointer();
        }

        /// <summary>The folder's icon was clicked: it opens.</summary>
        public void OpenFolder()
        {
            if (!Open || step != 0) return;
            SetStep(1);
            StartCoroutine(OpenFrom((RectTransform)folderIcon.transform, folderWindow, true));
        }

        /// <summary>Password.txt's row was clicked: the file opens.</summary>
        public void OpenFile()
        {
            if (!Open || step != 1) return;
            SetStep(2);
            StartCoroutine(OpenFrom(fileRow, fileWindow, false));
        }

        /// <summary>One of the robots' own files was clicked: not for a courier unit.</summary>
        public void Refuse()
        {
            if (!Open || denied == null) return;
            denied.enabled = true;
            deniedUntil = Time.unscaledTime + 1.3f;
        }

        void SetStep(int s)
        {
            step = s;
            steppedAt = Time.unscaledTime;
            hint.text = s == 0 ? "CLICK THE PASSWORDS FOLDER" : s == 1 ? "CLICK Password.txt" : "REMEMBER THE CODE, THEN CLICK CLOSE";
        }

        /// <summary>
        /// The click lands: the thing clicked is pressed in for a moment (a folder's flap drops
        /// open), then its window grows out of it to its place on the desktop.
        /// </summary>
        IEnumerator OpenFrom(RectTransform source, GameObject window, bool isFolder)
        {
            // pressed in
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.09f)
            {
                source.localScale = Vector3.one * Mathf.Lerp(1f, 0.9f, t);
                yield return null;
            }
            // released; a folder's front flap falls open
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.14f)
            {
                float e = t * t * (3f - 2f * t);
                source.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e);
                if (isFolder && folderFlap != null) folderFlap.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.55f, e), 1f);
                yield return null;
            }
            source.localScale = Vector3.one;

            // the window grows out of what was clicked
            var win = (RectTransform)window.transform;
            var canvasGroup = window.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = window.AddComponent<CanvasGroup>();
            window.SetActive(true);
            Vector3 home = win.localPosition;
            Vector3 from = win.parent.InverseTransformPoint(source.TransformPoint(source.rect.center));
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.24f)
            {
                float e = 1f - (1f - t) * (1f - t) * (1f - t);        // fast out of the icon, settling into place
                win.localPosition = Vector3.Lerp(from, home, e);
                win.localScale = Vector3.one * Mathf.Lerp(0.12f, 1f, e);
                canvasGroup.alpha = Mathf.Clamp01(t * 2.2f);
                yield return null;
            }
            win.localPosition = home;
            win.localScale = Vector3.one;
            canvasGroup.alpha = 1f;
        }

        void MovePointer()
        {
            if (pointer == null || Mouse.current == null) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Mouse.current.position.ReadValue(), null, out var local))
                pointer.localPosition = local;
            // a click dips the pointer for a moment
            if (Mouse.current.leftButton.wasPressedThisFrame) pressed = 1f;
            pressed = Mathf.MoveTowards(pressed, 0f, Time.unscaledDeltaTime * 8f);
            pointer.localScale = Vector3.one * (1f - 0.18f * pressed);
        }

        void Update()
        {
            if (!Open) return;
            MovePointer();
            if (Cursor.visible) Cursor.visible = false;
            if (denied != null && denied.enabled && Time.unscaledTime > deniedUntil) denied.enabled = false;
            var k = Keyboard.current;
            // Esc is the only key, and only to leave once the file has been read
            if (k != null && k.escapeKey.wasPressedThisFrame && step == 2 && Time.unscaledTime - steppedAt > 0.6f) Close();
        }

        /// <summary>Close the desktop (the CLOSE button once the file is open).</summary>
        public void Close()
        {
            if (!Open || step < 2) return;
            Open = false;
            StopAllCoroutines();
            fade.Show(false);
            group.blocksRaycasts = false;
            FirstPersonController.CaptureCursor(true);
            Viewed?.Invoke(Password);
        }
    }
}
