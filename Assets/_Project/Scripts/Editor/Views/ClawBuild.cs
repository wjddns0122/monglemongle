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
    /// The 11-26 claw machine screen (Figma "인형뽑기" 88:1866): Assets/_UI수정/3_마을/Claw.prefab, listed as "Claw". Fixed-size
    /// Figma pictures (Art/UI_1126/Claw: ◀, the hearts' pill, a heart, the four key caps, 시작하기) placed at their Figma spots —
    /// ◀ pinned to the top left, the hearts to the top middle, the keys / tip / button to the bottom middle; the tip, the wait
    /// for the next heart and the prize are cream pills (Figma's tip pill #FFEFDF) drawn to fit their words.
    /// </summary>
    public static class ClawBuild
    {
        public const string PrefabPath = UiEditFolder.VillageDir + "/Claw.prefab";
        static readonly Vector2 Frame = new Vector2(1920f, 1080f);
        static readonly Color CreamFill = Hex("#FFEFDF");

        public static GameObject Make()
        {
            var root = Root("Claw", 10);
            var v = root.gameObject.AddComponent<ClawView>();

            // top: ◀ (그만하기), the five hearts
            v.backButton = PictureButton(root, "Btn_나가기", "Claw/back.png", 40.5f, 30f, 94.5f, 98f, TL, Frame);
            var pill = Picture(root, "Hearts", "Claw/pill.png", 715.5f, 44f, 513.5f, 81f, T, Frame);
            var ps = new Vector2(513.5f, 81f);
            float[] hx = { 766.1f, 850.38f, 932.86f, 1017.14f, 1101.05f };
            v.hearts = new Image[hx.Length];
            for (int i = 0; i < hx.Length; i++) v.hearts[i] = Picture(pill.transform, "Heart" + i, "Claw/heart.png", hx[i] - 0.1f - 715.5f, 53f - 44f, 69.5f, 62.5f, TL, ps);
            v.timer = Cream(root, "Timer", 972.25f, 139f, 300f, 40f, T, 22f, out v.timerText);
            v.timer.gameObject.SetActive(false);

            // middle: what came out
            v.result = Cream(root, "Result", 972.25f, 250f, 600f, 132f, T, 44f, out v.resultText);
            v.resultText.textWrappingMode = TextWrappingModes.Normal;
            v.result.gameObject.SetActive(false);

            // bottom: the key caps (left, right, up, down), the tip, 시작하기
            var keys = new (string name, string art, float x, float y, float w, float h)[]
            {
                ("Btn_왼쪽", "Claw/key_left.png", 581.5f, 905.5f, 97f, 96.5f), ("Btn_오른쪽", "Claw/key_right.png", 810f, 905.5f, 96.5f, 96.5f),
                ("Btn_위", "Claw/key_up.png", 696f, 797.5f, 96.5f, 97f), ("Btn_아래", "Claw/key_down.png", 696f, 905.5f, 96.5f, 96.5f),
            };
            v.arrows = new HoldButton[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                var b = PictureButton(root, k.name, k.art, k.x, k.y, k.w, k.h, B, Frame);
                v.arrows[i] = b.gameObject.AddComponent<HoldButton>();
                var press = b.GetComponent<PressScale>(); press.pressed = 0.88f; press.pressedTint = 0.82f;   // a key cap goes well down
            }
            v.hint = Cream(root, "Hint", 1150.01f, 798.82f, 394.17f, 44.69f, B, 20f, out v.hintText);
            v.hintText.text = "방향키 이동 · SPACE 내리기";
            v.startButton = PictureButton(root, "Btn_시작", "Claw/start.png", 936.5f, 872f, 427f, 132.5f, B, Frame);
            { var press = v.startButton.GetComponent<PressScale>(); press.pressed = 0.93f; press.pressedTint = 0.88f; }
            v.startText = Words(v.startButton.transform, "Label", "시작하기", 66.23f, Brown, 0f, -3f, 427f, 132.5f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(427f, 132.5f));
            v.startText.enableAutoSizing = true; v.startText.fontSizeMin = 36f; v.startText.fontSizeMax = 66.23f;

            root.gameObject.SetActive(false);   // the village opens it (ClawView.Get)
            return root.gameObject;
        }

        /// <summary>A cream pill (Figma's tip pill, #FFEFDF, fully round) centred on frame x <paramref name="cx"/>, its words in it;
        /// the view widens it to fit what it says.</summary>
        static RectTransform Cream(Transform root, string name, float cx, float y, float w, float h, Vector2 anchor, float size, out TMP_Text text)
        {
            var n = Node(root, name, cx - w / 2f, y, w, h, anchor, Frame);
            var p = n.anchoredPosition; n.pivot = new Vector2(0.5f, n.pivot.y); n.anchoredPosition = new Vector2(p.x + (0.5f - anchor.x) * w, p.y);   // grows both ways
            Shape(n.gameObject, CreamFill, Vector4.one * h * 0.5f).raycastTarget = false;
            var t = Words(n, "Words", "", size, Brown, 0f, 0f, w, h, C, HorizontalAlignmentOptions.Center, null, new Vector2(w, h));
            var rt = t.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(12f, 0f); rt.offsetMax = new Vector2(-12f, 0f);
            text = t;
            return n;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Claw", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
