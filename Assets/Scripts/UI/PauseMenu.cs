using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>What the pause menu needs from whichever level is running.</summary>
    public interface ILevelFlow
    {
        /// <summary>False during cutscenes, story cards and terminals: Esc belongs to them then.</summary>
        bool CanPause { get; }
        void RestartCheckpoint();
        void LoadMenu();
    }

    /// <summary>
    /// The pause menu (Esc): PAUSED, with RESUME, RESTART CHECKPOINT, SETTINGS and MAIN MENU.
    /// The game stops while it is open; the menu fades in, and its two pages slide over one another.
    /// The crosshair marker only appears beside a line the mouse is actually over, or once Up/Down
    /// (or W/S) have been used to move it; Enter or E picks, a click picks, Esc resumes.
    /// SETTINGS holds the look sensitivity (Left/Right to change), remembered between sessions.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public CanvasGroup group;
        public GameObject mainPage, settingsPage;
        public Text[] items;                  // RESUME, RESTART CHECKPOINT, SETTINGS, MAIN MENU
        public RectTransform marker;          // the crosshair beside the line being pointed at
        public Text sensitivityValue;
        public RectTransform sensitivityFill;
        public MonoBehaviour flow;            // the level's flow (an ILevelFlow)
        public FirstPersonController player;
        public HudController hud;

        public Color selected = new Color32(244, 236, 214, 255);
        public Color idle = new Color32(170, 190, 200, 255);

        const string SensitivityKey = "LookSensitivity";
        const float MinSensitivity = 0.03f, MaxSensitivity = 0.21f;
        const int Levels = 10;
        public bool Open { get; private set; }
        ILevelFlow level;
        int index = -1;                       // the marked line; -1 while nothing is pointed at
        bool controlBefore, cursorBefore, inSettings;
        UiFade fade, mainFade, settingsFade;
        CanvasGroup mainGroup, settingsGroup;

        void Awake()
        {
            level = flow as ILevelFlow;
            fade = UiFade.For(group);
            fade.slide = Vector2.zero;        // the veil only fades; the pages do the sliding
            mainGroup = PageGroup(mainPage);
            settingsGroup = PageGroup(settingsPage);
            mainFade = UiFade.For(mainGroup);
            settingsFade = UiFade.For(settingsGroup);
            mainFade.slide = new Vector2(-44f, 0f);
            settingsFade.slide = new Vector2(44f, 0f);
            fade.Show(false, true);
            group.blocksRaycasts = false;
            if (PlayerPrefs.HasKey(SensitivityKey)) player.mouseSensitivity = PlayerPrefs.GetFloat(SensitivityKey);
        }

        static CanvasGroup PageGroup(GameObject page)
        {
            page.SetActive(true);
            var g = page.GetComponent<CanvasGroup>();
            return g != null ? g : page.AddComponent<CanvasGroup>();
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;
            if (!Open)
            {
                if (k.escapeKey.wasPressedThisFrame && level != null && level.CanPause) Show();
                return;
            }

            if (inSettings)
            {
                if (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) { ShowSettings(false); return; }
                float step = (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame ? 1f : 0f)
                           - (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame ? 1f : 0f);
                if (step != 0f) ChangeSensitivity(Mathf.RoundToInt(step));
                return;
            }

            if (k.escapeKey.wasPressedThisFrame) { Resume(); return; }
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) Select(index < 0 ? 0 : (index + 1) % items.Length);
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) Select(index < 0 ? items.Length - 1 : (index + items.Length - 1) % items.Length);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)
                Pick(index < 0 ? 0 : index);
        }

        void Show()
        {
            Open = true;
            Time.timeScale = 0f;
            controlBefore = player.ControlEnabled;
            cursorBefore = player.CursorCaptured;
            player.ControlEnabled = false;
            FirstPersonController.CaptureCursor(false);
            ShowSettings(false, true);
            Select(-1);
            hud.SetGameplayVisible(false, true);      // the menu stands alone over the frozen game
            fade.Show(true);
            mainFade.Show(false, true);               // the choices slide in from the left as the veil comes up
            mainFade.Show(true);
            group.blocksRaycasts = true;
        }

        public void Resume()
        {
            if (!Open) return;
            Open = false;
            Time.timeScale = 1f;
            fade.Show(false);
            group.blocksRaycasts = false;
            hud.SetGameplayVisible(true, true);
            player.ControlEnabled = controlBefore;
            FirstPersonController.CaptureCursor(cursorBefore || controlBefore);
        }

        /// <summary>Mark a line (the keyboard, or the mouse passing over it); -1 marks none and hides the crosshair.</summary>
        public void Select(int i)
        {
            index = i;
            for (int n = 0; n < items.Length; n++) items[n].color = n == i ? selected : idle;
            marker.gameObject.SetActive(i >= 0);
            if (i < 0) return;
            marker.SetParent(items[i].transform, false);
            marker.anchoredPosition = new Vector2(-52f, 0f);
        }

        /// <summary>The mouse has left a line: if that was the marked one, nothing is marked any more.</summary>
        public void Leave(int i)
        {
            if (index == i) Select(-1);
        }

        /// <summary>Run an item (Enter, E, or a click).</summary>
        public void Pick(int i)
        {
            switch (i)
            {
                case 0: Resume(); break;
                case 1: Resume(); level.RestartCheckpoint(); break;
                case 2: ShowSettings(true); break;
                case 3: Time.timeScale = 1f; Open = false; level.LoadMenu(); break;
            }
        }

        void ShowSettings(bool show, bool instant = false)
        {
            inSettings = show;
            settingsGroup.blocksRaycasts = show;
            mainGroup.blocksRaycasts = !show;
            settingsFade.Show(show, instant);
            mainFade.Show(!show, instant);
            if (show) ChangeSensitivity(0);
        }

        /// <summary>
        /// Called by the settings page's - and + buttons (and Left/Right): one press is exactly one
        /// step on the 1 to 10 scale shown beside the bar.
        /// </summary>
        public void ChangeSensitivity(int steps)
        {
            float share = Mathf.InverseLerp(MinSensitivity, MaxSensitivity, player.mouseSensitivity);
            int level = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, Levels, share)) + steps, 1, Levels);
            share = (level - 1) / (float)(Levels - 1);
            player.mouseSensitivity = Mathf.Lerp(MinSensitivity, MaxSensitivity, share);
            PlayerPrefs.SetFloat(SensitivityKey, player.mouseSensitivity);
            sensitivityFill.anchorMax = new Vector2(share, 1f);
            sensitivityValue.text = level.ToString();
        }

        public void LessSensitive() => ChangeSensitivity(-1);
        public void MoreSensitive() => ChangeSensitivity(1);
        public void CloseSettings() => ShowSettings(false);

        void OnDestroy()
        {
            if (Open) Time.timeScale = 1f;       // never leave the game frozen behind a scene change
        }
    }
}
