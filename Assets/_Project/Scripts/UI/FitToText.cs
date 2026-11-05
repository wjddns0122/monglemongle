using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// Grows a 9-sliced background to the text it holds (D-021): width = text width + padding, clamped to [minW, maxW];
    /// with minH set the height follows the text too. Re-measures whenever the text changes.
    /// </summary>
    [ExecuteAlways]
    public class FitToText : MonoBehaviour
    {
        public Text text;
        public float minW, padX, maxW = 1860f, minH, padY;
        string last; int lastSize, baseSize;

        void LateUpdate()
        {
            if (text != null && (text.text != last || text.fontSize != lastSize)) Apply();
        }

        public void Apply()
        {
            if (text == null) return;
            if (baseSize == 0) baseSize = text.fontSize;
            last = text.text; lastSize = text.fontSize;
            var rt = (RectTransform)transform;
            // measure the text on one line at its own size (best fit would report the shrunk size)
            text.resizeTextForBestFit = false; var hov = text.horizontalOverflow; text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float need = text.preferredWidth + padX;
            float w = Mathf.Clamp(need, minW, maxW);
            bool child = text.transform.parent == transform && minH <= 0f;
            if (child)
            {
                // the label follows the plate; when even the widest plate is too narrow the text shrinks on one line
                var tr = text.rectTransform; tr.sizeDelta = new Vector2(w - padX * 0.5f, Mathf.Max(tr.sizeDelta.y, baseSize * 1.4f));
                if (need > maxW)
                {
                    text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
                    tr.sizeDelta = new Vector2(w - padX * 0.5f, baseSize * 1.4f);
                    text.resizeTextForBestFit = true; text.resizeTextMaxSize = baseSize; text.resizeTextMinSize = Mathf.Max(12, baseSize / 2);
                }
            }
            else text.horizontalOverflow = hov;
            var size = new Vector2(w, rt.sizeDelta.y);
            if (minH > 0f)
            {
                var tr = text.rectTransform; tr.sizeDelta = new Vector2(w - padX, tr.sizeDelta.y);
                size.y = Mathf.Max(minH, text.preferredHeight + padY);
            }
            rt.sizeDelta = size;
        }
    }
}
