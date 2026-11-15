using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// Streaks of light that rush outward from the middle of the screen while the parkour motor is
    /// moving fast: thin at a run, long and bright in a slide. They are a handful of UI bars, each
    /// re-thrown from near the centre whenever it reaches the edge.
    /// </summary>
    public class SpeedLines : MonoBehaviour
    {
        public ParkourMotor motor;              // none (in the stealth levels): the lines never show
        public CanvasGroup group;
        public RectTransform[] lines;
        public float inner = 250f, outer = 780f;       // how far from the middle a streak starts and ends (canvas units)

        float[] travel, speed, angle;

        void Awake()
        {
            group.alpha = 0f;
            travel = new float[lines.Length];
            speed = new float[lines.Length];
            angle = new float[lines.Length];
            for (int i = 0; i < lines.Length; i++) Throw(i, Random.value);
        }

        void Throw(int i, float start)
        {
            travel[i] = start;
            speed[i] = Random.Range(1.6f, 3.2f);
            angle[i] = Random.Range(0f, 360f);
        }

        void Update()
        {
            float rush = motor != null && motor.isActiveAndEnabled ? motor.Rush : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, Mathf.Clamp01((rush - 0.2f) / 0.45f), Time.deltaTime * 4f);
            if (group.alpha <= 0f) return;
            for (int i = 0; i < lines.Length; i++)
            {
                travel[i] += Time.deltaTime * speed[i] * (0.8f + 1.2f * rush);
                if (travel[i] >= 1f) Throw(i, 0f);
                float a = angle[i] * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float e = travel[i] * travel[i];                                   // they speed up as they leave
                lines[i].anchoredPosition = dir * Mathf.Lerp(inner, outer, e);
                lines[i].localRotation = Quaternion.Euler(0f, 0f, angle[i]);
                lines[i].sizeDelta = new Vector2(Mathf.Lerp(60f, 380f, e) * (0.5f + rush), Mathf.Lerp(2f, 5f, rush));
            }
        }
    }
}
