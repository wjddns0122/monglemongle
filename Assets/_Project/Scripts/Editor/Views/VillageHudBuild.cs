using Mongle.UI2;
using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 village HUD (Figma "대화1" 88:231 for the buttons, "마을 초대" 88:510 for the hint pill, the tips and the blue
    /// edges, "탈것" 88:2367 for the ride bar): Assets/_UI수정/3_마을/VillageHud.prefab, placed in the Village scene. The fixed
    /// pieces are the Figma vectors (Art/UI_1126/Village, drawn by headless Chrome: the tile icons have turned parts and masks);
    /// the hint pill and the ride button are SoftRects with the SVG values, the words TextMeshPro.
    /// </summary>
    public static class VillageHudBuild
    {
        /// <summary>Where the left column's pictures visibly start (frame px).</summary>
        public const float LeftEdge = 48.5f;
        public const string PrefabPath = UiEditFolder.VillageDir + "/VillageHud.prefab";
        static readonly Color Cream = Hex("#FAF0DF"), Sky = Hex("#A0DCF8"), Minus = Hex("#7D5D51"), Badge = Hex("#FF8FA8");

        public static GameObject Make()
        {
            var root = Root("VillageHud", 9);   // under the village's panels (shop, storage, quest board: canvas order 10)
            var v = root.gameObject.AddComponent<VillageHudView>();

            // the top row: home, 퀘스트 (and what is under way beneath it), coins, gear
            var top = Group(root, "Top");
            // the left column — home, 퀘스트, the zoom pair — shares one left edge: the home button's visible edge, x 48.5
            // (user 2026-11-26: "딱 일자로"; the pictures' own clear margins are 1 px for home and zoom, 0 for the pill)
            v.homeButton = PictureButton(top, "Btn_Home", "Village/home.png", LeftEdge - 1f, 53f, 104f, 104f, TL);
            v.questButton = PictureButton(top, "Btn_Quest", "Village/quest_pill.png", LeftEdge, 190.5f, 187f, 94.5f, TL);
            var ql = Words(v.questButton.transform, "Label", "퀘스트", 38f, Brown, 0f, 0f, 187f, 50f, C, HorizontalAlignmentOptions.Center, "village.quest", new Vector2(187f, 94.5f));
            ql.rectTransform.anchoredPosition = new Vector2(-2.2f, 5f);   // Figma: the word sits a little above the pill's middle
            var qb = Node(v.questButton.transform, "NewBadge", 150f, -14f, 84f, 40f, TL, new Vector2(187f, 94.5f));
            Shape(qb.gameObject, Badge, Vector4.one * 20f);
            v.questBadgeText = Words(qb, "Text", "새 1", 22f, Color.white, 0f, 0f, 84f, 40f, C, HorizontalAlignmentOptions.Center, null, new Vector2(84f, 40f));
            qb.gameObject.SetActive(false); v.questBadge = qb.gameObject;
            var tr = Node(top, "Tracker", LeftEdge, 300f, 420f, 100f, TL);
            Shape(tr.gameObject, Cream, Vector4.one * 30f).color = new Color(Cream.r, Cream.g, Cream.b, 0.6f);
            var tt = Words(tr, "Text", "", 28f, Brown, 0f, 0f, 420f, 100f, C, HorizontalAlignmentOptions.Left, null, new Vector2(420f, 100f));
            Stretch(tt.rectTransform, 30f, 15f); tt.lineSpacing = 8f;
            tr.gameObject.SetActive(false); v.tracker = tr; v.trackerText = tt;
            v.coins = Picture(top, "Coins", "Village/coin_pill.png", 1455.5f, 56f, 289.5f, 98f, TR).rectTransform;
            Picture(v.coins, "Coin", "Village/coin_icon.png", 1481f - 1455.5f, 74f - 56f, 59.5f, 59.5f, TL, new Vector2(289.5f, 98f));
            v.coinText = Words(v.coins, "Amount", "0", 40f, Brown, 1548f - 1455.5f, 62f - 56f, 175f, 80f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(289.5f, 98f));
            v.gearButton = PictureButton(top, "Btn_Settings", "Village/gear.png", 1770f, 51.5f, 107f, 107f, TR);
            v.top = top.gameObject;

            // the zoom pair at the left edge
            var zoom = Group(root, "Zoom");
            v.zoomInButton = PictureButton(zoom, "Btn_ZoomIn", "Village/zoom_plus.png", LeftEdge - 1f, 481f, 107f, 107f, L);
            v.zoomOutButton = PictureButton(zoom, "Btn_ZoomOut", "Village/zoom_minus.png", LeftEdge - 1f, 600.5f, 107f, 107f, L);
            Words(v.zoomOutButton.transform, "Minus", "-", 60f, Minus, 86.8f - 50f, 613.3f - 600.5f, 35f, 78f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(107f, 107f));
            v.zoom = zoom.gameObject;

            // the tiles down the right edge: P 가구 옮기기, E 앨범, T 보관함
            var bar = Group(root, "Toolbar");
            (string name, string art, float x, float y, float s, float nx, float ny, string key, float ky)[] tiles =
            {
                ("Btn_가구 옮기기", "tile_furniture", 1762.5f, 409.5f, 115f, 1762.8f, 409.9f, "P", 401f),
                ("Btn_앨범", "tile_album", 1763f, 532.5f, 115f, 1763.4f, 532.8f, "E", 526f),
                ("Btn_보관함", "tile_storage", 1763f, 661f, 114.5f, 1763.3f, 661.3f, "T", 655.5f),
            };
            string[] tipWords = { "가구를 옮길 수 있어요", "앨범에서 사진을 확인\n할 수 있어요", "보관함에서 소지한\n물건을 확인해요" };
            string[] tipKeys = { "village.tip.furniture", "village.tip.album", "village.tip.storage" };
            var buttons = new Button[3]; v.glows = new GameObject[3]; v.tips = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var t = tiles[i];
                buttons[i] = PictureButton(bar, t.name, "Village/" + t.art + ".png", t.x, t.y, t.s, t.s, R);
                // the blue edge lies over the tile's own edge (Figma 88:526: a 5 px line centred on it), inside the button so it squashes with it
                var glow = Picture(buttons[i].transform, "Glow", "Village/tile_glow.png", t.nx - 2.9f - t.x, t.ny - 2.9f - t.y, 120f, 120f, TL, new Vector2(t.s, t.s));
                glow.gameObject.SetActive(false); v.glows[i] = glow.gameObject;
                var key = Picture(bar, "Key" + t.key, i == 0 ? "Village/key_p.png" : "Village/key.png", 1754f, t.ky, 28f, 28f, R);
                Words(key.transform, "Letter", t.key, 18f, Brown, 0f, 0f, 28f, 28f, C, HorizontalAlignmentOptions.Center, null, new Vector2(28f, 28f)).rectTransform.anchoredPosition = new Vector2(0f, 1f);
            }
            for (int i = 0; i < 3; i++)
            {
                var t = tiles[i];
                var tip = Picture(bar, "Tip" + i, "Village/tip.png", t.nx - 122.4f, t.ny - 66.4f, 234.5f, 61f, R);
                var tw = Words(tip.transform, "Text", tipWords[i], 18f, Brown, 46f, 2f, 184f, 44f, TL, HorizontalAlignmentOptions.Left, tipKeys[i], new Vector2(234.5f, 61f));
                tw.lineSpacing = -6f;
                tip.gameObject.SetActive(false); v.tips[i] = tip.gameObject;
            }
            v.furnitureButton = buttons[0]; v.albumButton = buttons[1]; v.storageButton = buttons[2];
            var sb = Node(bar, "StorageBadge", 1818f, 649f, 84f, 40f, R);
            Shape(sb.gameObject, Badge, Vector4.one * 20f);
            v.storageBadgeText = Words(sb, "Text", "새 1", 22f, Color.white, 0f, 0f, 84f, 40f, C, HorizontalAlignmentOptions.Center, null, new Vector2(84f, 40f));
            sb.gameObject.SetActive(false); v.storageBadge = sb.gameObject;
            v.toolbar = bar.gameObject;

            // the hint pill at the top (Figma 88:637: the half-clear cream, its words 38 px, two lines at most)
            var hint = Node(root, "Hint", 443.5f, 60.3f, 986.7f, 122f, T);
            Shape(hint.gameObject, new Color(Cream.r, Cream.g, Cream.b, 0.6f), Vector4.one * 59.68f);
            var ht = Words(hint, "Text", "", 38f, Brown, 0f, 0f, 986.7f, 122f, C, HorizontalAlignmentOptions.Center, null, new Vector2(986.7f, 122f));
            Stretch(ht.rectTransform, 62f, 12f); ht.textWrappingMode = TextWrappingModes.Normal; ht.lineSpacing = -4f;
            hint.gameObject.SetActive(false); v.hint = hint; v.hintText = ht;

            // the ride bar at the bottom (Figma 88:2368): its words, the blue "밖에서 보기"
            var ride = Picture(root, "RideBar", "Village/ride_bar.png", 471f, 874f, 1054.5f, 140.5f, B);
            ride.raycastTarget = true;
            var rs = new Vector2(1054.5f, 140.5f);
            var rt = Words(ride.transform, "Text", "", 38f, Brown, 562.1f - 471f, 917.4f - 874f, 620f, 49f, TL, HorizontalAlignmentOptions.Left, null, rs);
            rt.enableAutoSizing = true; rt.fontSizeMin = 24f; rt.fontSizeMax = 38f;
            var see = Node(ride.transform, "Btn_밖에서 보기", 1208.2f - 471f, 901f - 874f, 267.6f, 86.6f, TL, rs);
            var seeShape = Shape(see.gameObject, Sky, Vector4.one * 28f); seeShape.raycastTarget = true;
            var sbtn = see.gameObject.AddComponent<Button>(); sbtn.transition = Selectable.Transition.None; sbtn.targetGraphic = seeShape;
            see.gameObject.AddComponent<PressScale>();
            var sl = Words(see, "Label", "밖에서 보기", 38f, Brown, 0f, 0f, 267.6f, 50f, C, HorizontalAlignmentOptions.Center, "village.toast.watch", new Vector2(267.6f, 86.6f));
            sl.rectTransform.anchoredPosition = new Vector2(0f, 2.4f);   // Figma: 2.4 above the pill's middle
            ride.gameObject.SetActive(false); v.rideBar = ride.gameObject; v.rideText = rt; v.rideButton = sbtn;
            return root.gameObject;
        }

        static RectTransform Group(Transform root, string name)
        {
            var rt = Node(root, name, 0f, 0f, 1920f, 1080f, C);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        static void Stretch(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, padY); rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("VillageHud", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static void AddTo(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "VillageHud";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
