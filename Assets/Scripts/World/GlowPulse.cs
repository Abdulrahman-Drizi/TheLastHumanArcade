using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Makes the marker strips of a hiding spot breathe slowly: a quiet hint, not a beacon.
    /// Only the material slots using the hide-glow material are touched.
    /// </summary>
    public class GlowPulse : MonoBehaviour
    {
        public string materialName = "Env_HideGlow";
        public Color color = new Color32(120, 255, 235, 255);     // the palette's mint: safe
        public float low = 0.4f, high = 1.0f, period = 2.4f;
        public Light glowLight;

        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Renderer[] renderers;
        int[] slots;
        float lightBase;

        void Start()
        {
            block = new MaterialPropertyBlock();
            var all = GetComponentsInChildren<Renderer>();
            var found = new System.Collections.Generic.List<Renderer>();
            var index = new System.Collections.Generic.List<int>();
            foreach (var r in all)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].name != materialName) continue;
                    found.Add(r);
                    index.Add(i);
                }
            }
            renderers = found.ToArray();
            slots = index.ToArray();
            if (glowLight != null) lightBase = glowLight.intensity;
        }

        void Update()
        {
            if (block == null) Start();         // (scripts were reloaded under a running game)
            float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 2f / period);
            block.SetColor(Emission, color.linear * Mathf.Lerp(low, high, k));
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(block, slots[i]);
            if (glowLight != null) glowLight.intensity = lightBase * Mathf.Lerp(0.7f, 1f, k);
        }
    }
}
