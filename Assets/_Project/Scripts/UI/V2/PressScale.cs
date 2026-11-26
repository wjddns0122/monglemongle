using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// Press feedback for v2 buttons: a quick squash, no colour tint by default (the art has its own colours). A key can hold it
    /// down too (<see cref="Hold"/>: the claw machine's key caps go down while their arrow key is pressed); with
    /// <see cref="pressedTint"/> under 1 the picture also darkens a little while down.
    /// </summary>
    public class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public float pressed = 0.94f;
        [Tooltip("눌린 동안 그림 밝기 (1이면 그대로)")] [Range(0.5f, 1f)] public float pressedTint = 1f;
        Vector3 baseScale = Vector3.one; float target = 1f;
        bool pointer, held, awake;
        Graphic graphic; Color baseColor = Color.white;

        void Awake() { Init(); }

        void Init()
        {
            if (awake) return; awake = true;
            baseScale = transform.localScale;
            graphic = GetComponent<Graphic>(); if (graphic != null) baseColor = graphic.color;
        }

        public void OnPointerDown(PointerEventData e) { pointer = true; Refresh(); }
        public void OnPointerUp(PointerEventData e) { pointer = false; Refresh(); }
        public void OnPointerExit(PointerEventData e) { pointer = false; Refresh(); }
        void OnDisable() { pointer = false; held = false; Refresh(); if (awake) transform.localScale = baseScale; }

        /// <summary>Held down by something other than the pointer (a key).</summary>
        public void Hold(bool on) { if (on == held) return; held = on; Refresh(); }

        /// <summary>Down now (pointer or key).</summary>
        public bool Down => pointer || held;

        void Refresh()
        {
            Init();
            target = Down ? pressed : 1f;
            if (graphic != null && pressedTint < 0.999f)
            {
                float k = Down ? pressedTint : 1f;
                graphic.color = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k, baseColor.a);
            }
        }

        void Update()
        {
            float s = transform.localScale.x / Mathf.Max(0.0001f, baseScale.x);
            if (!Mathf.Approximately(s, target)) transform.localScale = baseScale * Mathf.MoveTowards(s, target, Time.unscaledDeltaTime * 3f);
        }
    }
}
