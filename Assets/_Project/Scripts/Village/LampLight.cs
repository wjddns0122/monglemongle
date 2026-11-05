using UnityEngine;

namespace Mongle.Village
{
    /// <summary>A street lamp's warm glow: a point light plus an emissive bulb that can be switched by clicking.</summary>
    public class LampLight : MonoBehaviour
    {
        public Light Glow; public Renderer Bulb;
        public bool On { get; private set; } = true;
        float level = 1f; MaterialPropertyBlock mpb;
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");

        void Awake() { mpb = new MaterialPropertyBlock(); Apply(1f); }
        public void Toggle() { On = !On; }

        void Update()
        {
            float goal = On ? 1f : 0f;
            if (Mathf.Abs(level - goal) < 0.002f) return;
            level = Mathf.MoveTowards(level, goal, Time.deltaTime * 4f);
            Apply(level);
        }

        void Apply(float v)
        {
            if (Glow != null) { Glow.intensity = 0.55f * v; Glow.enabled = v > 0.01f; }
            if (Bulb != null) { Bulb.GetPropertyBlock(mpb); mpb.SetColor(Emission, new Color(1.5f, 1.2f, 0.65f) * v); Bulb.SetPropertyBlock(mpb); }
        }
    }
}
