using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 notice pill (Figma "인게임3" 88:1767: pill 88:1848 #FAF0DF at 60 %, fully round, 465 x 94; coin 88:1850; words
    /// 88:1849, 38 px #7A594D): Assets/_UI수정/5_공통/Notice.prefab, listed as "Notice". The pill hangs from the top middle
    /// under the HUD's top row (where the old notice sat); GameRoot raises it into a free top band or drops it to the bottom
    /// edge while a window is open.
    /// </summary>
    public static class NoticeBuild
    {
        public const string PrefabPath = UiEditFolder.CommonDir + "/Notice.prefab";
        const float W = 464.97f, H = 94.27f;

        public static GameObject Make()
        {
            var root = Root("Notice", 1001);   // over everything, the scene fade too
            var v = root.gameObject.AddComponent<NoticeView>();
            var pill = Node(root, "Pill", 960f - W / 2f, 189.3f, W, H, T);
            var shape = Shape(pill.gameObject, new Color(Hex("#FAF0DF").r, Hex("#FAF0DF").g, Hex("#FAF0DF").b, 0.6f), Vector4.one * H * 0.5f);
            shape.raycastTarget = false;
            var ps = new Vector2(W, H);
            var coin = Picture(pill, "Coin", "Common/coin.png", 51.46f - 1.5f, (H - 60f) / 2f, 60f, 59.5f, L, ps);
            var text = Words(pill, "Words", "몽글 코인 + 50", 38f, Brown, 0f, 0f, W, H, C, HorizontalAlignmentOptions.Center, null, ps);
            var rt = text.rectTransform; rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = new Vector2(51.46f, 0f); rt.offsetMax = new Vector2(-51.46f, 0f);
            v.pill = pill; v.text = text; v.coin = coin.gameObject;
            pill.gameObject.SetActive(false);
            return root.gameObject;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Notice", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
