using UnityEngine;
using UnityEngine.UI;

namespace TheLastHuman
{
    /// <summary>
    /// The mission waypoint at the top of the HUD: a real 3D arrow that keeps turning in space to
    /// point at the current objective, with the objective's name and distance under it.
    ///
    /// The arrow model sits in a tiny "studio" far below the level, filmed by its own camera into a
    /// texture the HUD shows. The studio camera stands in for the player's view with the head held
    /// level, so the arrow shows the true direction: away from you when the target is ahead, toward
    /// you when it is behind, tipping up or down toward the target's height.
    /// </summary>
    public class ObjectiveArrow : MonoBehaviour
    {
        public Transform arrow;            // the 3D arrow (points along its +Z)
        public Renderer arrowRenderer;
        public Camera arrowCamera;
        public RawImage display;
        public Text label;
        public CanvasGroup group;
        public Transform viewer;           // the camera the player is looking through
        public float swing = 9f;           // how quickly the arrow comes round to a new direction
        public int resolution = 256;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Emission = Shader.PropertyToID("_EmissionColor");
        Transform target;
        string targetName;
        RenderTexture texture;
        MaterialPropertyBlock block;
        Vector3 rest;

        void OnEnable()
        {
            rest = arrow.localPosition;
            texture = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32) { name = "WaypointArrow", antiAliasing = 4 };
            arrowCamera.targetTexture = texture;
            display.texture = texture;
        }

        void OnDisable()
        {
            if (arrowCamera != null) arrowCamera.targetTexture = null;
            if (texture != null)
            {
                texture.Release();
                Destroy(texture);
            }
        }

        public void SetTarget(Transform newTarget, string name)
        {
            target = newTarget;
            targetName = name;
        }

        /// <summary>Amber for human eyes, blue through the visor.</summary>
        public void SetColour(Color colour)
        {
            if (block == null) block = new MaterialPropertyBlock();
            block.SetColor(BaseColor, colour);
            block.SetColor(Emission, colour.linear * 0.85f);
            arrowRenderer.SetPropertyBlock(block);
        }

        void LateUpdate()
        {
            bool active = target != null && viewer != null;
            group.alpha = Mathf.MoveTowards(group.alpha, active ? 1f : 0f, Time.deltaTime * 4f);
            arrowCamera.enabled = group.alpha > 0.01f;
            if (!active) return;

            var to = target.position - viewer.position;
            if (to.sqrMagnitude > 0.01f)
            {
                var relative = Quaternion.Inverse(Quaternion.Euler(0f, viewer.eulerAngles.y, 0f)) * to.normalized;
                // right next to a target at the player's feet the true direction is almost straight down,
                // which reads badly from the studio camera: keep the tilt within about 35 degrees
                var flat = new Vector3(relative.x, 0f, relative.z);
                float level = flat.magnitude;
                flat = level > 0.05f ? flat / level : Vector3.forward;
                float tilt = Mathf.Clamp(Mathf.Atan2(relative.y, level) * Mathf.Rad2Deg, -35f, 35f);
                var want = Quaternion.LookRotation(flat) * Quaternion.Euler(-tilt, 0f, 0f);
                arrow.localRotation = Quaternion.Slerp(arrow.localRotation, want, 1f - Mathf.Exp(-swing * Time.deltaTime));
            }
            arrow.localPosition = rest + Vector3.up * (0.04f * Mathf.Sin(Time.time * 2.4f));     // it floats a little

            to.y = 0f;
            label.text = targetName + "  " + Mathf.RoundToInt(to.magnitude) + " M";
        }
    }
}
