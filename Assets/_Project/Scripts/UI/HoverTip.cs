using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// D-034: while the pointer is over this object, the shared <see cref="TipBubble"/> shows <see cref="text"/> just above it
    /// (the tools' one-line hints on the repair shelf).
    /// </summary>
    public class HoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea] public string text;
        public TipBubble bubble;

        public void OnPointerEnter(PointerEventData e) { if (bubble != null && !string.IsNullOrEmpty(text)) bubble.Show((RectTransform)transform, text); }
        public void OnPointerExit(PointerEventData e) { if (bubble != null) bubble.Hide((RectTransform)transform); }
        void OnDisable() { if (bubble != null) bubble.Hide((RectTransform)transform); }
    }
}
