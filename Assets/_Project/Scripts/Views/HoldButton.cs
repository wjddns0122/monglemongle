using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>A button that says whether it is held down (the key caps: the claw moves while one is held).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData e) { var b = GetComponent<Button>(); if (b == null || b.interactable) Held = true; }
        public void OnPointerUp(PointerEventData e) { Held = false; }
        public void OnPointerExit(PointerEventData e) { Held = false; }
        void OnDisable() { Held = false; }
        /// <summary>Tests: hold or let go.</summary>
        public void QaHold(bool on) { Held = on; }
    }
}
