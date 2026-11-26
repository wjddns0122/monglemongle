using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// Mouse over a box (a tool slot): only its border turns pink, the fill stays as drawn. Leaves the box as it was when the
    /// mouse goes away or the box is hidden. Nothing happens on a button that cannot be pressed.
    /// </summary>
    [RequireComponent(typeof(SoftRect))]
    public class UiHoverBorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("마우스를 올렸을 때 테두리 색")] public Color color = new Color(1f, 0.561f, 0.671f, 1f);   // Figma pink #FF8FAB
        [Tooltip("마우스를 올렸을 때 테두리 두께")] public float width = 5f;

        SoftRect box; Selectable button; float baseWidth; Color baseColor; bool over;

        void Awake() { Ready(); }

        void Ready()
        {
            if (box != null) return;
            box = GetComponent<SoftRect>(); button = GetComponent<Selectable>();
            baseWidth = box.borderWidth; baseColor = box.borderColor;
        }

        public void OnPointerEnter(PointerEventData e) { Ready(); if (button == null || button.IsInteractable()) Show(true); }
        public void OnPointerExit(PointerEventData e) { Show(false); }
        void OnDisable() { Show(false); }

        /// <summary>Shows or takes away the hover border (the mouse does this; tools and tests may too).</summary>
        public void Show(bool on)
        {
            Ready();
            if (box == null || over == on) return;
            over = on;
            box.borderWidth = on ? width : baseWidth;
            box.borderColor = on ? color : baseColor;
            box.SetAll();
        }
    }
}
