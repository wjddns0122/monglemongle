using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 message box (Figma 88:1683 "몽이가 왔어요"): Assets/_UI수정/5_공통/Dialog.prefab, listed as "Dialog" in
    /// Resources/Views/ViewIndex. All of it is shapes and words (no pictures), with the SVG's own values, so it grows cleanly.
    /// </summary>
    public static class DialogBuild
    {
        public const string PrefabPath = UiEditFolder.CommonDir + "/Dialog.prefab";

        static readonly Color Sheet = Hex("#F9EDDD"), Edge = Hex("#C4DFE7"), Stitch = Hex("#C8E1EA"), Band = Hex("#A0DCF8"), Peach = Hex("#FFB58A");

        // Figma (frame px): the sheet's outer edge is the 3 px stroke's outside (node box grown by 1.5)
        const float X = 361.2f, Y = 404.1f, W = 1182.03f, H = 445.12f;
        const float BodyLinePitch = 45.6f;   // two body lines 575.17 → 620.77

        public static GameObject Make()
        {
            var root = Root("Dialog", 200);
            var view = root.gameObject.AddComponent<DialogView>();
            var size = new Vector2(W, H);

            var panel = Node(root, "Panel", X, Y, W, H, C);
            panel.anchoredPosition = new Vector2(0f, panel.anchoredPosition.y);   // centred across (Figma: 7.8 left of the middle)
            var sheet = Shape(panel.gameObject, Sheet, Vector4.one * 29.5f, 3f, Edge);
            sheet.raycastTarget = true;   // clicks on the box stay in the box
            sheet.stitch = true; sheet.stitchColor = Stitch; sheet.stitchWidth = 3f; sheet.dash = 10f; sheet.gap = 10f; sheet.roundCaps = true;
            sheet.stitchPad = new Vector4(13.47f, 13.7f, 13.46f, 13.7f); sheet.stitchRadius = 28f; sheet.stitchPeriod = 19.99f;

            // the blue band (inside the stroke, round top corners), the title in its middle
            var band = Node(panel, "Band", 1.5f, 1.47f, W - 3f, 112f, T, size);
            band.anchorMin = new Vector2(0f, 1f); band.anchorMax = new Vector2(1f, 1f); band.offsetMin = new Vector2(1.5f, band.offsetMin.y); band.offsetMax = new Vector2(-1.5f, band.offsetMax.y);
            Shape(band.gameObject, Band, new Vector4(28f, 28f, 0f, 0f));
            var title = Words(band, "Title", "몽이가 왔어요", 50f, Brown, 0f, 0f, W - 3f, 112f, C, HorizontalAlignmentOptions.Center, null, new Vector2(W - 3f, 112f));
            title.rectTransform.anchorMin = Vector2.zero; title.rectTransform.anchorMax = Vector2.one; title.rectTransform.offsetMin = new Vector2(40f, 0f); title.rectTransform.offsetMax = new Vector2(-40f, 1f);

            // the words: centred, starting 57.6 under the band
            var body = Words(panel, "Body", "몽이는 오랫동안 사랑받아 조금 지친 곰인형이에요.\n다시 포근해질 수 있도록 도와주세요.", 38f, Brown, 120f, 113.47f + 57.6f, W - 240f, 94.6f, T, HorizontalAlignmentOptions.Center, null, size);
            body.verticalAlignment = VerticalAlignmentOptions.Top; body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = LineSpacingFor(body, BodyLinePitch);

            // the buttons: one row in the middle of the foot, the second (peach) left of the main one (blue)
            var row = Node(panel, "Buttons", 0f, H - 40.95f - 86.57f, 717.39f, 86.57f, B, size);
            row.anchoredPosition = new Vector2(0f, 40.95f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 27.83f; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            var fit = row.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            view.secondButton = Pill(row, "Btn_Second", Peach, "죄송합니다");
            view.okButton = Pill(row, "Btn_Ok", Band, "살펴볼게요");

            view.panel = panel; view.title = title; view.body = body;
            root.gameObject.SetActive(false);   // the game opens it (DialogView.Open)
            return root.gameObject;
        }

        static Button Pill(Transform row, string name, Color fill, string words)
        {
            var rt = Node(row, name, 0f, 0f, 344.78f, 86.57f, TL, new Vector2(344.78f, 86.57f));
            var s = Shape(rt.gameObject, fill, Vector4.one * 43.285f); s.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = s;
            rt.gameObject.AddComponent<Mongle.UI2.PressScale>();
            // Figma: the 38 px word box sits 3.6 above the pill's middle
            var label = Words(rt, "Label", words, 38f, Brown, 0f, 0f, 344.78f, 49f, C, HorizontalAlignmentOptions.Center, null, new Vector2(344.78f, 86.57f));
            label.rectTransform.anchoredPosition = new Vector2(0f, 3.6f);
            return b;
        }

        /// <summary>The TMP line spacing that puts two lines <paramref name="pitch"/> apart (Figma's line height).</summary>
        static float LineSpacingFor(TMP_Text t, float pitch)
        {
            string keep = t.text; t.lineSpacing = 0f;
            t.text = "가\n가"; t.ForceMeshUpdate();
            float p0 = t.textInfo.lineCount > 1 ? t.textInfo.lineInfo[0].baseline - t.textInfo.lineInfo[1].baseline : pitch;
            t.text = keep;
            return (pitch - p0) / (t.fontSize * 0.01f);
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Dialog", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
