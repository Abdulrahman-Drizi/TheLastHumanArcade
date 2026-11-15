using UnityEngine;

namespace TheLastHuman
{
    /// <summary>
    /// Rows of small lights that flash together in a travelling pattern: chasers running down a
    /// street, a ripple across a ceiling, a column climbing a tower. Each light belongs to one of a
    /// few "phases"; all lights of a phase share one material, and only those few materials are
    /// animated, so hundreds of lights cost a handful of draw calls and no per-light work.
    /// </summary>
    public class PatternLights : MonoBehaviour
    {
        public Renderer[] lights;
        public int[] phaseOf;                 // which phase each light belongs to
        public int phases = 8;
        public Material template;             // an emissive material; one copy is made per phase
        public Color colour = new Color32(90, 240, 255, 255);
        public float low = 0.15f, high = 3.2f;
        public float cyclesPerSecond = 0.9f;  // how fast the pattern travels (negative runs it the other way)
        public float sharpness = 3f;          // higher = a shorter bright pulse with longer dark between

        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        Material[] copies;

        void Awake()
        {
            copies = new Material[phases];
            for (int p = 0; p < phases; p++) copies[p] = new Material(template) { name = template.name + "_Phase" + p };
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].sharedMaterial = copies[phaseOf[i] % phases];
        }

        void Update()
        {
            var linear = colour.linear;
            for (int p = 0; p < phases; p++)
            {
                float wave = 0.5f + 0.5f * Mathf.Cos((Time.time * cyclesPerSecond - (float)p / phases) * Mathf.PI * 2f);
                copies[p].SetColor(Emission, linear * Mathf.Lerp(low, high, Mathf.Pow(wave, sharpness)));
            }
        }

        void OnDestroy()
        {
            if (copies == null) return;
            foreach (var m in copies) Destroy(m);
        }
    }
}
