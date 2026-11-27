using Mongle.UI2;
using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Dressing up (Figma 115:1573 "인게임3 사본"): Assets/_UI수정/2_수선점/Customize.prefab, listed as "Customize" in
    /// Resources/Views/ViewIndex. The panel, the cards (plain / worn) and the arrows are the frame's vectors drawn by headless
    /// Chrome (work/figma_1127/custom, compose.py) at their exact boxes; the pills (top line, 꾸며주기, 이대로 확정, the count
    /// badges) are SoftRects with the SVG values so they grow with their words. The frame has the panel and its arrows 32 px
    /// right of the middle: here they stand in the middle (user 2026-11-27: "center로").
    /// </summary>
    public static class CustomizeBuild
    {
        public const string PrefabPath = UiEditFolder.RepairDir + "/Customize.prefab";

        static readonly Color Pill = Hex("#F9EDDD"), Pink = Hex("#FFA9C2"), Badge = Hex("#A0DCF8"), Label = Hex("#816253");

        // the panel's picture box in the frame, and how far the group moves to stand in the middle (node 506.61 + 971.14 / 2 = 992.18)
        const float PanelX = 507.5f, PanelY = 727.5f, PanelW = 971.5f, PanelH = 292f, Shift = 960f - 992.18f;
        // the cards: nodes 155.42 (y 828.51), evenly 170.03 apart, the row (835.56) in the panel's middle (user 2026-11-27: "Center로
        // 고정"; the frame starts it 1 px further left); the pictures 1.4 / 1.0 out of their nodes
        const float CardPitch = 170.035f, RowW = 4f * CardPitch + 155.42f, RowX = 992.18f - RowW / 2f;
        const float CardX = RowX - 1.4f, CardY = 827.5f, CardS = 160f;
        // the head row spans the cards: 꾸며주기 starts with the first card, 이대로 확정 ends with the last (the frame had 63.6 / 29
        // of room on the left / right, so the panel's insides leaned right)
        const float LabelX = RowX, HintX = LabelX + 132.66f + 10.44f + 4.5f, OkX = RowX + RowW - 236.95f;

        public static GameObject Make()
        {
            var root = Root("Customize", 11);   // over the repair HUD (10)
            var v = root.gameObject.AddComponent<CustomizeView>();

            // the line at the top: the cream pill 639 x 97 at y 49.25 (middle 97.75), 38 px words 62 in from its ends
            var title = Node(root, "Title", 640.5f, 49.25f, 639f, 97f, T);
            title.pivot = new Vector2(0.5f, 0.5f); title.anchoredPosition = new Vector2(0f, -97.75f);
            Shape(title.gameObject, Pill, Vector4.one * 48.5f);
            var tt = Words(title, "Text", "마음에 드는 것을 골라 꾸며줘요", 38f, Brown, 0f, 0f, 639f, 97f, C, HorizontalAlignmentOptions.Center, null, new Vector2(639f, 97f));
            tt.rectTransform.anchorMin = Vector2.zero; tt.rectTransform.anchorMax = Vector2.one; tt.rectTransform.offsetMin = tt.rectTransform.offsetMax = Vector2.zero;
            v.title = title; v.titleText = tt;

            // the panel at the bottom (pinned to the foot, 60.57 above it), its children in the panel picture's pixels
            var panel = Picture(root, "Panel", "Repair/cust_panel.png", PanelX + Shift, PanelY, PanelW, PanelH, B);
            panel.raycastTarget = true;   // clicks on the panel stay on it
            var ps = new Vector2(PanelW, PanelH);
            var p = panel.rectTransform;
            v.panel = p;

            // 꾸며주기 on its white label (115:1632, 30 px), the help line (115:1617, 18 px)
            var lab = Node(p, "Label", LabelX - PanelX, 757.86f - PanelY, 132.66f, 60.3f, TL, ps);
            Shape(lab.gameObject, Color.white, Vector4.one * 16.6f);
            Words(lab, "Text", "꾸며주기", 30f, Label, 0f, 0f, 132.66f, 60.3f, C, HorizontalAlignmentOptions.Center, "custom.title", new Vector2(132.66f, 60.3f));
            v.hintText = Words(p, "Hint", "누르면 입히고, 다시 누르면 벗어요.", 18f, Brown, HintX - PanelX, 774.85f - PanelY, 300f, 23f, TL, HorizontalAlignmentOptions.Left, "custom.hint", ps);

            // 이대로 확정 (115:1602: the pink pill 236.95 x 59.49, 38 px)
            var ok = Node(p, "Btn_이대로 확정", OkX - PanelX, 756f - PanelY, 236.95f, 59.49f, TL, ps);
            var okShape = Shape(ok.gameObject, Pink, Vector4.one * 29.745f); okShape.raycastTarget = true;
            var okBtn = ok.gameObject.AddComponent<Button>(); okBtn.transition = Selectable.Transition.None; okBtn.targetGraphic = okShape;
            ok.gameObject.AddComponent<PressScale>();
            Words(ok, "Label", "이대로 확정", 38f, Brown, 0f, 0f, 236.95f, 59.49f, C, HorizontalAlignmentOptions.Center, "custom.confirm", new Vector2(236.95f, 59.49f));
            v.confirmButton = okBtn;

            // five cards
            v.plateOff = Art("Repair/cust_card.png"); v.plateOn = Art("Repair/cust_card_on.png");
            v.cards = new CustomizeView.Card[5]; v.cardPitch = CardPitch;
            var cs = new Vector2(CardS, CardS);
            for (int i = 0; i < 5; i++)
            {
                var b = PictureButton(p, "Card" + i, "Repair/cust_card.png", CardX + i * CardPitch - PanelX, CardY - PanelY, CardS, CardS, TL, ps);
                var c = new CustomizeView.Card { button = b, plate = (Image)b.targetGraphic };
                // the picture in the card's middle, the name on its foot (115:1634: 18 px at y 949.62), over the visible card (155.42 - shadow)
                c.icon = Picture(b.transform, "Icon", "Repair/cust_card.png", 27.6f, 14f, 100f, 100f, TL, cs);
                c.icon.sprite = null; c.icon.enabled = false;
                c.label = Words(b.transform, "Name", "빨간 리본", 18f, Label, 2.6f, 949.62f - CardY, 150f, 23f, TL, HorizontalAlignmentOptions.Center, null, cs);
                // the count badge (115:1628: 38.47 x 19.86 at its top right), growing to the left with more digits
                var badge = Node(b.transform, "Badge", 680.97f - 573.38f + 1.4f, 839.61f - 828.51f + 1.0f, 38.47f, 19.86f, TL, cs);
                badge.pivot = new Vector2(1f, 1f); badge.anchoredPosition += new Vector2(38.47f, 0f);
                Shape(badge.gameObject, Badge, Vector4.one * 9.93f);
                c.count = Words(badge, "Count", "x1", 18f, Brown, 0f, 0f, 38.47f, 19.86f, C, HorizontalAlignmentOptions.Center, null, new Vector2(38.47f, 19.86f));
                c.count.rectTransform.anchorMin = Vector2.zero; c.count.rectTransform.anchorMax = Vector2.one; c.count.rectTransform.offsetMin = c.count.rectTransform.offsetMax = Vector2.zero;
                c.badge = badge;
                v.cards[i] = c;
            }
            // nothing to wear yet (a friend whose player bought nothing): the words where the cards would be
            v.emptyText = Words(p, "Empty", "", 30f, Brown, 60f, 100f, PanelW - 120f, 160f, TL, HorizontalAlignmentOptions.Center, null, ps);
            v.emptyText.textWrappingMode = TextWrappingModes.Normal; v.emptyText.gameObject.SetActive(false);

            // ◀ ▶ (the frame has them 7 apart in height: both at their middle, 832.5)
            v.prevButton = PictureButton(p, "Btn_이전", "Repair/cust_arrow_l.png", 384f - PanelX, 832.5f - PanelY, 107f, 107f, TL, ps);
            v.nextButton = PictureButton(p, "Btn_다음", "Repair/cust_arrow_r.png", 1494.5f - PanelX, 832.5f - PanelY, 107.5f, 107f, TL, ps);

            root.gameObject.SetActive(false);   // the repair shop opens it after the stamp
            return root.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Customize", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
