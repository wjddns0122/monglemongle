using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Furniture placing (Figma 115:1398): Assets/_UI수정/3_마을/PlaceBar.prefab, listed as "PlaceBar" in Resources/Views/ViewIndex.
    /// The pictures are the frame's vectors drawn by headless Chrome (work/figma_1127/place, compose.py) at their exact boxes:
    /// ◀ Common/back_corner.png (the unified corner button, node 99.5 x 102.87 at (50, 48)), 되돌리기 / 다시 하기 and their key caps,
    /// the bar and its four buttons; the tips are the village tiles' tip (Village/tip.png, the same vector).
    /// </summary>
    public static class PlaceBarBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/PlaceBar.prefab";

        static readonly string[] Names = { "Btn_돌리기", "Btn_보관함에 넣기", "Btn_여기에 두기", "Btn_취소" };
        static readonly string[] Arts = { "place_rotate", "place_store", "place_put", "place_cancel" };
        static readonly string[] TipWords = { "돌리기", "보관함에 넣기", "여기에 두기", "취소" };
        static readonly string[] TipKeys = { "place.rotate", "place.store", "place.put", "place.cancel" };

        // the bar and its buttons (frame px, the composed pictures' boxes)
        const float BarX = 67f, BarY = 886.5f, BarW = 570f, BarH = 157.5f;
        static readonly float[] ButtonX = { 93f, 226.5f, 359.5f, 493f };
        const float ButtonY = 906f, ButtonS = 119f;
        // 여기에 두기's tip (node 257.93, 853.77; its tail over the button), the others the same way over theirs, kept on the screen
        const float PutTipX = 257.5f, TipY = 853.5f, TipW = 234.5f, TipH = 61f, TipMinX = 20f;

        public static GameObject Make()
        {
            var root = Root("PlaceBar", 10);   // over the village HUD (9)
            var v = root.gameObject.AddComponent<PlaceBarView>();

            // top left: ◀ where the home button stands (it steps aside while placing), then 되돌리기 (Z) and 다시 하기 (Y)
            v.backButton = PictureButton(root, "Btn_나가기", "Common/back_corner.png", 48.5f, 46.5f, 104.5f, 108f, TL);
            v.undoButton = PictureButton(root, "Btn_되돌리기", "Village/undo.png", 192.5f, 58.5f, 75.5f, 75.5f, TL);
            v.redoButton = PictureButton(root, "Btn_다시 하기", "Village/redo.png", 295.5f, 58.5f, 75.5f, 75.5f, TL);
            v.undoKeys = new[] { KeyCap(root, "KeyZ", "Z", 186.5f, 194.19f, 13f), KeyCap(root, "KeyY", "Y", 290.24f, 297.93f, 14f) };

            // the bar at the bottom left (pinned there on any screen shape)
            var bar = Picture(root, "Bar", "Village/place_panel.png", BarX, BarY, BarW, BarH, BL);
            bar.raycastTarget = true;   // a click on the bar stays on the bar (not the park under it)
            var bs = new Vector2(BarW, BarH);
            var buttons = new Button[4];
            for (int i = 0; i < 4; i++)
                buttons[i] = PictureButton(bar.transform, Names[i], "Village/" + Arts[i] + ".png", ButtonX[i] - BarX, ButtonY - BarY, ButtonS, ButtonS, TL, bs);
            v.rotateButton = buttons[0]; v.storeButton = buttons[1]; v.putButton = buttons[2]; v.cancelButton = buttons[3];
            v.tips = new GameObject[4];
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Max(TipMinX, PutTipX + ButtonX[i] - ButtonX[2]);
                var tip = Picture(bar.transform, "Tip" + i, "Village/tip.png", x - BarX, TipY - BarY, TipW, TipH, TL, bs);
                // the words in the yellow part; 여기에 두기 lands where the frame has it (339.44, 866.78: 18 px)
                Words(tip.transform, "Text", TipWords[i], 18f, Brown, 35.25f, 0f, 184.3f, 49f, TL, HorizontalAlignmentOptions.Center, TipKeys[i], new Vector2(TipW, TipH));
                tip.gameObject.SetActive(false); v.tips[i] = tip.gameObject;
            }

            root.gameObject.SetActive(false);   // the village opens it (PlaceBarView.Show)
            return root.gameObject;
        }

        /// <summary>A little key cap (Figma 115:1536: 24.84 round at y 48.71) with its letter (18 px, the frame's text box).</summary>
        static GameObject KeyCap(Transform root, string name, string letter, float x, float letterX, float letterW)
        {
            var cap = Picture(root, name, "Village/key_small.png", x, 48.5f, 25.5f, 25f, TL);
            Words(cap.transform, "Letter", letter, 18f, Brown, letterX - x, 49.04f - 48.5f, letterW, 23f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(25.5f, 25f));
            return cap.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("PlaceBar", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
