using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// What the player sees through the mask: a cold blue machine visor laid over the picture, with
    /// scanlines, corner brackets and one telemetry panel. Its BATTERY row is the mask's real
    /// clock, and its ROBOTS row is how the robots are treating the player; DISGUISE INTEGRITY
    /// stays full for as long as the mask is on, and only breaks if an X-ray shows the human
    /// underneath. CPU and RAM are set dressing that runs hot when things go wrong.
    /// It only displays what the HUD tells it.
    /// </summary>
    public class VisorHud : MonoBehaviour
    {
        public CanvasGroup group;
        public Volume grade;                 // the blue colour grade over the 3D picture
        [Range(0f, 1f)] public float gradeStrength = 0.6f;   // how much of that grade is applied: a tint to see through, not a filter to fight
        public RawImage scanlines;

        [Header("Telemetry rows")]
        public Image cpuFill;
        public Image ramFill, batteryFill, integrityFill;
        public Text cpuValue, ramValue, batteryValue, integrityValue;
        public GameObject robotsRow;         // off where there are no robots to watch for (the escape)
        public Text robotsValue;
        public Text header;                  // only ever shown while the disguise is broken

        public Color normal = new Color32(70, 180, 255, 255);
        public Color warn = new Color32(255, 84, 60, 255);
        public float lowBattery = 0.3f;      // below this the battery row turns red: a warning, nothing more

        bool on, compromised, stressed;
        float shown, charge = 1f, cpu = 0.42f, cpuTarget = 0.42f, ram = 0.61f, nextCpu, nextText;

        void Awake()
        {
            group.alpha = 0f;
            if (grade != null) grade.weight = 0f;
            if (robotsRow != null) robotsRow.SetActive(false);
            header.enabled = false;
        }

        /// <summary>Switch the visor on (the mask went on) or off.</summary>
        public void Show(bool show)
        {
            if (show && !on)
            {
                charge = 1f;
                compromised = false;
                nextText = 0f;
            }
            on = show;
        }

        /// <summary>How much of the mask's battery is left (1..0), and whether an X-ray has broken the disguise.</summary>
        public void SetCharge(float left, bool seenThrough)
        {
            charge = Mathf.Clamp01(left);
            compromised = seenThrough;
        }

        /// <summary>How the robots are treating the player, as the panel's last row.</summary>
        public void SetRobots(string state, bool bad)
        {
            if (robotsRow == null) return;
            if (!robotsRow.activeSelf) robotsRow.SetActive(true);
            robotsValue.text = state;
            robotsValue.color = bad ? warn : normal;
        }

        /// <summary>A robot is after the player: the readouts run hot.</summary>
        public void SetStress(bool stress) => stressed = stress;

        void Update()
        {
            shown = Mathf.MoveTowards(shown, on ? 1f : 0f, Time.deltaTime * (on ? 2.6f : 5f));
            bool booting = on && shown < 1f;
            group.alpha = shown * (booting && Random.value < 0.3f ? 0.35f : 1f);       // flickers while it starts up
            if (grade != null) grade.weight = shown * gradeStrength;
            if (shown <= 0f) return;

            var uv = scanlines.uvRect;
            uv.y = Mathf.Repeat(Time.time * 0.35f, 1f);                                 // the lines crawl slowly
            scanlines.uvRect = uv;

            bool hot = compromised || stressed;
            if (Time.time >= nextCpu)
            {
                nextCpu = Time.time + Random.Range(0.25f, 0.7f);
                cpuTarget = hot ? Random.Range(0.84f, 0.99f) : Random.Range(0.31f, 0.58f);
            }
            float ease = 1f - Mathf.Exp(-6f * Time.deltaTime);
            cpu = Mathf.Lerp(cpu, cpuTarget, ease);
            ram = Mathf.Lerp(ram, (hot ? 0.86f : 0.62f) + 0.04f * Mathf.Sin(Time.time * 0.6f), ease * 0.5f);

            // the disguise is whole until the battery is flat; only an X-ray breaks it early
            float integrity = compromised ? 0.12f + 0.16f * Random.value : 1f;
            bool low = charge < lowBattery;
            bool blink = Mathf.Repeat(Time.time, 0.4f) < 0.2f;

            Bar(cpuFill, cpu, hot);
            Bar(ramFill, ram, hot);
            Bar(batteryFill, charge, low);
            Bar(integrityFill, integrity, compromised);

            if (Time.time >= nextText)                                                  // numbers tick ten times a second
            {
                nextText = Time.time + 0.1f;
                Value(cpuValue, cpu, hot);
                Value(ramValue, ram, hot);
                Value(batteryValue, charge, low);
                Value(integrityValue, integrity, compromised);
            }
            header.enabled = compromised;
            header.color = blink ? warn : new Color(warn.r, warn.g, warn.b, 0.35f);
        }

        void Bar(Image fill, float value, bool bad)
        {
            fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fill.color = bad ? warn : normal;
        }

        void Value(Text text, float value, bool bad)
        {
            text.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
            text.color = bad ? warn : normal;
        }
    }
}
