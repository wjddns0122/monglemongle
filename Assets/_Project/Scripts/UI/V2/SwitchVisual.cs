using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>The pill on/off switch of the settings (track + sliding knob), a Toggle underneath.</summary>
    [RequireComponent(typeof(Toggle))]
    public class SwitchVisual : MonoBehaviour
    {
        public SoftRect track, knob;
        public Color on = new Color(1f, 0.53f, 0.67f), off = new Color(0.98f, 0.92f, 0.86f);
        public float travel = 30f;
        Toggle t; float k;
        void Awake() { t = GetComponent<Toggle>(); k = t.isOn ? 1 : 0; Apply(); }
        void Update()
        {
            if (t == null) return;
            float want = t.isOn ? 1 : 0;
            if (!Mathf.Approximately(k, want)) { k = Mathf.MoveTowards(k, want, Time.unscaledDeltaTime * 8f); Apply(); }
        }
        /// <summary>Jump to the toggle's state without the slide (after binding values).</summary>
        public void Snap() { if (t == null) t = GetComponent<Toggle>(); k = t.isOn ? 1 : 0; Apply(); }

        public void Apply()
        {
            if (track != null) track.color = Color.Lerp(off, on, k);
            if (knob != null) knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, k), 0);
        }
    }
}
