using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// The speech plate of the head bubbles (Figma shop_plate_left, tail at the bottom left) used as a tooltip: the tail points at the
    /// top of the hovered object, and on the right half of the screen the plate is mirrored so it opens towards the middle.
    /// </summary>
    public class TipBubble : MonoBehaviour
    {
        Image plate; Text label; RectTransform owner;

        public static TipBubble Create(Transform canvas)
        {
            var img = UiKit.SpriteImage(canvas, "TipBubble", "shop_plate_left", Vector2.zero, new Vector2(0.03f, 0f), Vector2.zero, new Vector2(260, 84));
            UiKit.Slice(img); img.raycastTarget = false;   // never under the pointer itself, or the hover would flicker
            var t = UiKit.Label(img.transform, "", 26, UiKit.FigBrown, new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(240, 60));
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.raycastTarget = false;
            UiKit.AutoFit(img, t, 180f, 64f, 640f);
            var b = img.gameObject.AddComponent<TipBubble>(); b.plate = img; b.label = t;
            img.gameObject.SetActive(false);
            return b;
        }

        public void Show(RectTransform target, string text)
        {
            owner = target; label.text = text;
            var rt = plate.rectTransform; rt.SetAsLastSibling();
            // the tail tip just above the middle of the target's top edge, clear of the shelf's edge
            var r = target.rect; rt.position = target.TransformPoint(new Vector3(r.center.x, r.yMax + 18f, 0f));
            var canvasRt = (RectTransform)rt.parent;
            bool right = canvasRt.InverseTransformPoint(rt.position).x > 0f;
            rt.localScale = new Vector3(right ? -1f : 1f, 1f, 1f); label.rectTransform.localScale = rt.localScale;   // the words stay readable
            gameObject.SetActive(true);
            if (TryGetComponent<FitToText>(out var fit)) fit.Apply();   // (?. would miss Unity's fake null)
        }

        public void Hide(RectTransform target) { if (owner == target) { owner = null; gameObject.SetActive(false); } }
    }
}
