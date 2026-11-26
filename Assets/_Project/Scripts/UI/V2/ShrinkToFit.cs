using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// One-line label that keeps its design size but shrinks (down to <see cref="minScale"/>) when the text is wider than its
    /// box — longer English words, bigger numbers (D-038). LocText calls <see cref="Fit"/> after every language change.
    /// </summary>
    [RequireComponent(typeof(Text))]
    [ExecuteAlways]
    public class ShrinkToFit : MonoBehaviour
    {
        public int baseSize;
        [Range(0.3f, 1f)] public float minScale = 0.55f;
        Text text; string lastText; float lastWidth = -1;

        void OnEnable() { Fit(); }
        void LateUpdate() { if (text == null || text.text != lastText || !Mathf.Approximately(text.rectTransform.rect.width, lastWidth)) Fit(); }

        public void Fit()
        {
            if (text == null) text = GetComponent<Text>();
            if (text == null) return;
            if (baseSize <= 0) baseSize = text.fontSize;
            lastText = text.text; lastWidth = text.rectTransform.rect.width;
            text.fontSize = baseSize;
            if (lastWidth <= 1f) return;
            float pref = text.preferredWidth;
            if (pref > lastWidth) text.fontSize = Mathf.Max(Mathf.RoundToInt(baseSize * minScale), Mathf.FloorToInt(baseSize * lastWidth / pref));
        }
    }
}
