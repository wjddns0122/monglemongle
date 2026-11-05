using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Carousel: the deck, roof and horses turn slowly (the stairs stay put) and each horse bobs on its pole.
    /// It can be stopped and started again (the player clicks it, D-006); speed eases in both directions.
    /// </summary>
    public class CarouselMotion : MonoBehaviour
    {
        public float degreesPerSecond = 8f;
        public float bobHeight = 0.07f;   // in the model's own units (metres in the Blender file)
        public float bobSpeed = 1.5f;
        public float easeSeconds = 1.6f;
        /// <summary>Ground footprint radius as a fraction of the carousel height (root scale = height), stairs included.</summary>
        public float footprintRadius = 0.66f;
        public float FootprintWorldRadius => footprintRadius * transform.lossyScale.y;

        public bool Running { get; private set; } = true;
        /// <summary>0 = standing still, 1 = full speed.</summary>
        public float Speed01 { get; private set; } = 1f;
        public Transform[] Horses { get { Build(); return horses; } }
        public Transform Spin { get { Build(); return spin; } }

        Transform spin; Transform[] horses; Vector3[] basePos; float bobPhase;

        void Awake() { Build(); }

        void Build()
        {
            if (spin != null) return;
            var model = transform.Find("Model"); if (model == null) model = transform;
            spin = new GameObject("Spin").transform; spin.SetParent(model, false);
            var movers = new System.Collections.Generic.List<Transform>();
            foreach (Transform c in model) if (c.name.StartsWith("CR_Body") || c.name.StartsWith("CR_Horse")) movers.Add(c);
            var hs = new System.Collections.Generic.List<Transform>();
            foreach (var m in movers) { m.SetParent(spin, true); if (m.name.StartsWith("CR_Horse")) hs.Add(m); }
            horses = hs.ToArray(); basePos = new Vector3[horses.Length];
            for (int i = 0; i < horses.Length; i++) basePos[i] = horses[i].localPosition;
        }

        public void SetRunning(bool on) => Running = on;

        void Update()
        {
            Speed01 = Mathf.MoveTowards(Speed01, Running ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, easeSeconds));
            float k = Mathf.SmoothStep(0f, 1f, Speed01);
            spin.Rotate(Vector3.up, degreesPerSecond * k * Time.deltaTime, Space.Self); // positive = the way the horses face
            bobPhase += Time.deltaTime * bobSpeed * k;
            for (int i = 0; i < horses.Length; i++)
                horses[i].localPosition = basePos[i] + Vector3.up * (Mathf.Sin(bobPhase + i * 2.1f) * bobHeight);
        }
    }
}
