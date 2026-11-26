using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>Press feedback for v2 buttons: a quick squash, no colour tint (the art has its own colours).</summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressed = 0.94f;
        Vector3 baseScale = Vector3.one; float target = 1f;
        void Awake() { baseScale = transform.localScale; }
        public void OnPointerDown(PointerEventData e) { target = pressed; }
        public void OnPointerUp(PointerEventData e) { target = 1f; }
        public void OnPointerExit(PointerEventData e) { target = 1f; }
        void Update()
        {
            float s = transform.localScale.x / Mathf.Max(0.0001f, baseScale.x);
            if (!Mathf.Approximately(s, target)) transform.localScale = baseScale * Mathf.MoveTowards(s, target, Time.unscaledDeltaTime * 3f);
        }
    }
}
