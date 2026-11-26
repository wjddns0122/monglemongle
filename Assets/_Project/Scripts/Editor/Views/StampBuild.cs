using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 "수선 완료!" stamp card (Figma 88:1315): Assets/_UI수정/2_수선점/Stamp.prefab, listed as "Stamp". A fixed-size
    /// card, so every piece is the Figma vector itself (Art/UI_1126/Stamp): the grid sheet, the slots, the paw stamp, the button
    /// pin with its star and the pink button; the words are TextMeshPro.
    /// </summary>
    public static class StampBuild
    {
        public const string PrefabPath = UiEditFolder.RepairDir + "/Stamp.prefab";

        // Figma (frame px): the card's picture box, and the slots (4 x 2)
        const float CX = 474f, CY = 224.5f, CW = 1000.5f, CH = 650.5f;
        static readonly float[] SlotX = { 593.04f, 795.65f, 998.21f, 1200.77f }, SlotY = { 358.35f, 555.23f };

        public static GameObject Make()
        {
            var root = Root("Stamp", 20);
            var v = root.gameObject.AddComponent<StampView>();
            var size = new Vector2(CW, CH);
            var card = Picture(root, "Card", "Stamp/stamp_panel.png", CX, CY, CW, CH, C);
            card.raycastTarget = true;
            var ct = card.rectTransform;
            // the pin sticks out over the top-left corner
            Picture(ct, "Pin", "Stamp/stamp_pin.png", 419f - CX, 159.5f - CY, 165f, 265f, TL, size);
            Words(ct, "Title", "수선 완료!", 61.1f, Brown, 829.25f - CX - 100f, 261.87f - CY, 469f, 79f, TL, HorizontalAlignmentOptions.Center, "repair.stamp.title", size);
            v.marks = new GameObject[8];
            for (int i = 0; i < 8; i++)
            {
                float x = SlotX[i % 4] - CX, y = SlotY[i / 4] - CY;
                Picture(ct, "Slot" + i, "Stamp/stamp_slot.png", x - 0.04f, y - 0.35f, 155f, 155f, TL, size);
                // the paw print in its brown ring, as Figma draws it over the first slot
                var m = Picture(ct, "Mark" + i, "Stamp/stamp_paw.png", x - 4.54f, y - 2.85f, 163.5f, 163f, TL, size);
                m.gameObject.SetActive(i == 0);
                v.marks[i] = m.gameObject;
            }
            var b = PictureButton(ct, "Btn_Stamp", "Stamp/stamp_button.png", 812.5f - CX, 741.5f - CY, 323.5f, 100f, TL, size);
            // Figma: the 43 px words sit 3.5 above the pill's middle
            var label = Words(b.transform, "Label", "도장 찍기!", 43f, Brown, 0f, 0f, 323.5f, 56f, C, HorizontalAlignmentOptions.Center, "repair.stamp.button", new Vector2(323.5f, 100f));
            label.rectTransform.anchoredPosition = new Vector2(0f, 3.5f);
            v.card = ct; v.stampButton = b;
            root.gameObject.SetActive(false);   // the repair shop opens it (StampView.Open)
            return root.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Stamp", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
