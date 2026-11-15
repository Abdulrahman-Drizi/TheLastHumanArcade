using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// A Human Detector gate. It lets masked units through with a green blink. Anyone who walks
    /// through it without the mask on sets off a facility-wide alert: every security robot nearby
    /// runs straight to the gate, and the beam and lamp flash red while the alert lasts.
    /// </summary>
    public class HumanDetector : MonoBehaviour
    {
        public Renderer beam;
        public Light lamp;
        public Vector3 zoneSize = new Vector3(4f, 3f, 1.4f);      // the gateway, centred on this object
        public float alertRadius = 45f;
        public float alertSeconds = 6f;

        public Transform player;
        public PlayerIdentity identity;
        public DetectionSystem detection;
        public HudController hud;

        public Color idle = new Color32(90, 240, 255, 255);
        public Color passed = new Color32(120, 255, 235, 255);
        public Color alert = new Color32(255, 60, 50, 255);

        static readonly int Tint = Shader.PropertyToID("_Color");
        MaterialPropertyBlock block;
        float alertUntil = -1f, passedUntil = -1f;
        bool wasInside;

        void Awake() => block = new MaterialPropertyBlock();

        void Update()
        {
            var local = transform.InverseTransformPoint(player.position);
            bool inside = Mathf.Abs(local.x) < zoneSize.x / 2f && Mathf.Abs(local.z) < zoneSize.z / 2f && local.y > -0.5f && local.y < zoneSize.y;
            if (inside && !identity.IsRobot && Time.time > alertUntil && !detection.Paused)
            {
                alertUntil = Time.time + alertSeconds;
                detection.Alarm(player.position, alertRadius);
                hud.ShowWarning("HUMAN DETECTED  -  FACILITY ALERT", 4f);
                hud.PlayGlitch(0.5f, 0.6f);
            }
            else if (inside && !wasInside && identity.IsRobot) passedUntil = Time.time + 0.6f;
            wasInside = inside;

            bool alarmed = Time.time < alertUntil;
            bool blink = Mathf.Repeat(Time.time, 0.24f) < 0.12f;
            var colour = alarmed ? alert : Time.time < passedUntil ? passed : idle;
            colour.a = alarmed ? (blink ? 0.55f : 0.2f) : Time.time < passedUntil ? 0.4f : 0.16f + 0.05f * Mathf.Sin(Time.time * 2f);
            block.SetColor(Tint, colour);
            beam.SetPropertyBlock(block);
            lamp.color = alarmed ? alert : Time.time < passedUntil ? passed : idle;
            lamp.intensity = alarmed ? (blink ? 7f : 2f) : 2.4f;
        }
    }
}
