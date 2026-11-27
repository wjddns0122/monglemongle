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
    /// The village HUD (Figma 통일본 115:1255 "마을 초대" for the places of the buttons, the coins, the tiles, the tips and the hint
    /// pill; "대화1" 88:231 / 88:510 for their pictures, "탈것" 88:2367 for the ride bar): Assets/_UI수정/3_마을/VillageHud.prefab,
    /// placed in the Village scene. The corner buttons stand where every screen has them: home 99.52 at (50, 48), the gear at
    /// (1791, 48). The fixed
    /// pieces are the Figma vectors (Art/UI_1126/Village, drawn by headless Chrome: the tile icons have turned parts and masks);
    /// the hint pill and the ride button are SoftRects with the SVG values, the words TextMeshPro.
    /// </summary>
    public static class VillageHudBuild
    {
        /// <summary>Where the left column's pictures visibly start (frame px): the home button's edge in 115:1255.</summary>
        public const float LeftEdge = 50f;
        public const string PrefabPath = UiEditFolder.VillageDir + "/VillageHud.prefab";
        static readonly Color Cream = Hex("#FAF0DF"), Sky = Hex("#A0DCF8"), Minus = Hex("#7D5D51"), Badge = Hex("#FF8FA8"), NewDot = Hex("#FD95A9");

        public static GameObject Make()
        {
            var root = Root("VillageHud", 9);   // under the village's panels (shop, storage, quest board: canvas order 10)
            var v = root.gameObject.AddComponent<VillageHudView>();

            // the top row: home, 퀘스트 (and what is under way beneath it), coins, gear
            var top = Group(root, "Top");
            // the left column — home, 퀘스트, the zoom pair — shares one left edge: the home button's visible edge, x 50
            // (user 2026-11-26: "딱 일자로", kept over 115:1255's 50 / 43.68 / 59.48; the pictures' own clear margins are 1 px for
            // home and zoom, 0 for the pill). Heights are 115:1255's.
            v.homeButton = PictureButton(top, "Btn_Home", "Village/home.png", LeftEdge - 1f, 47f, 104f, 104f, TL);
            // 퀘스트 where 115:1255 has it (user 2026-11-27: "퀘스트 버튼은 이 디자인 보고 참고"): node 186.8 x 94.27 at (43.68, 189.27)
            // the pill itself is 115:1386's: the half-clear cream (#FAF0DF at 60 %) with fully round ends (r 47.135); the word 38 px
            // in its Figma box (80.32, 206.99, 109 x 49) — the 11-26 picture had smaller corners
            var qs = new Vector2(186.8f, 94.27f);
            var qn = Node(top, "Btn_Quest", 43.68f, 189.27f, qs.x, qs.y, TL);
            var qshape = Shape(qn.gameObject, new Color(Cream.r, Cream.g, Cream.b, 0.6f), Vector4.one * 47.135f); qshape.raycastTarget = true;
            var qbtn = qn.gameObject.AddComponent<Button>(); qbtn.transition = Selectable.Transition.None; qbtn.targetGraphic = qshape;
            qn.gameObject.AddComponent<PressScale>();
            v.questButton = qbtn;
            Words(qn, "Label", "퀘스트", 38f, Brown, 80.32f - 43.68f, 206.99f - 189.27f, 109f, 49f, TL, HorizontalAlignmentOptions.Center, "village.quest", qs);
            // something new (Figma 115:1398's dot on the 앨범 tile, user 2026-11-27: "새1 말고 … 빨간색 뜨는거 … 다 그렇게"): on the
            // pill's round top right
            v.questBadge = Dot(v.questButton.transform, 161.05f, 2.0f, qs); v.questBadgeText = null;
            var tr = Node(top, "Tracker", LeftEdge, 300f, 420f, 100f, TL);
            Shape(tr.gameObject, Cream, Vector4.one * 30f).color = new Color(Cream.r, Cream.g, Cream.b, 0.6f);
            var tt = Words(tr, "Text", "", 28f, Brown, 0f, 0f, 420f, 100f, C, HorizontalAlignmentOptions.Left, null, new Vector2(420f, 100f));
            Stretch(tt.rectTransform, 30f, 15f); tt.lineSpacing = 8f;
            tr.gameObject.SetActive(false); v.tracker = tr; v.trackerText = tt;
            // the coin pill: 115:1255 moved the whole group 10.7 right and 1 up (the pill node 1467.2, 56.02)
            v.coins = Picture(top, "Coins", "Village/coin_pill.png", 1466.2f, 55f, 289.5f, 98f, TR).rectTransform;
            Picture(v.coins, "Coin", "Village/coin_icon.png", 1481f - 1455.5f, 74f - 56f, 59.5f, 59.5f, TL, new Vector2(289.5f, 98f));
            v.coinText = Words(v.coins, "Amount", "0", 40f, Brown, 1548f - 1455.5f, 62f - 56f, 175f, 80f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(289.5f, 98f));
            // the gear: node 99.52 at (1791, 48), the picture 2.5 bigger round it
            v.gearButton = PictureButton(top, "Btn_Settings", "Village/gear.png", 1788.5f, 45.5f, 107f, 107f, TR);
            v.top = top.gameObject;

            // the zoom pair at the left edge
            var zoom = Group(root, "Zoom");
            // 115:1255: the nodes at y 468.06 / 591, the pictures 2.5 bigger round them; the "-" (35x78, 60 px) at (93, 606)
            v.zoomInButton = PictureButton(zoom, "Btn_ZoomIn", "Village/zoom_plus.png", LeftEdge - 1f, 465.56f, 107f, 107f, L);
            v.zoomOutButton = PictureButton(zoom, "Btn_ZoomOut", "Village/zoom_minus.png", LeftEdge - 1f, 588.5f, 107f, 107f, L);
            Words(v.zoomOutButton.transform, "Minus", "-", 60f, Minus, 93f - 56.5f, 606f - 588.5f, 35f, 78f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(107f, 107f));
            v.zoom = zoom.gameObject;

            // the tiles down the right edge: P 가구 옮기기, E 앨범, T 보관함 (115:1255: nodes 114.45 at x 1770.38, y 409.91 … 661.12;
            // evenly 125.6 apart (user 2026-11-27: "간격이 또 다르잖아" — the frame had 121.7 / 129.5); each picture sits 0.3-0.4
            // up-left of its node; every key mark 8.91 over its tile, every tip 66.26 over it (the first tile's, in the frame)
            var bar = Group(root, "Toolbar");
            // (user 2026-11-27 again: "여기 갭을 30px 정도"): 30 between the tiles, the middle one where it was (535.515)
            const float TileSize = 114.45f, TileGap = 30f, TilePitch = TileSize + TileGap, TileTop = 535.515f - TilePitch, KeyUp = 8.91f, TipUp = 66.26f;
            (string name, string art, float x, float y, float s, float nx, float ny, string key, float ky)[] tiles =
            {
                ("Btn_가구 옮기기", "tile_furniture", 1770.08f, TileTop - 0.4f, 115f, 1770.38f, TileTop, "P", TileTop - KeyUp),
                ("Btn_앨범", "tile_album", 1769.98f, TileTop + TilePitch - 0.3f, 115f, 1770.38f, TileTop + TilePitch, "E", TileTop + TilePitch - KeyUp),
                ("Btn_보관함", "tile_storage", 1770.08f, TileTop + 2f * TilePitch - 0.3f, 114.5f, 1770.38f, TileTop + 2f * TilePitch, "T", TileTop + 2f * TilePitch - KeyUp),
            };
            // the tips' pills (115:1255: 234.46 x 47.98, the tail under them over the tile's middle), their words at x 1695.63
            (float x, float y)[] tipAt = { (1648.03f, TileTop - TipUp), (1648.03f, TileTop + TilePitch - TipUp), (1648.03f, TileTop + 2f * TilePitch - TipUp) };
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
                var key = Picture(bar, "Key" + t.key, i == 0 ? "Village/key_p.png" : "Village/key.png", 1754f + 7.6f, t.ky, 28f, 28f, R);
                Words(key.transform, "Letter", t.key, 18f, Brown, 0f, 0f, 28f, 28f, C, HorizontalAlignmentOptions.Center, null, new Vector2(28f, 28f)).rectTransform.anchoredPosition = new Vector2(0f, 1f);
            }
            for (int i = 0; i < 3; i++)
            {
                var t = tiles[i];
                var tip = Picture(bar, "Tip" + i, "Village/tip.png", tipAt[i].x, tipAt[i].y, 234.5f, 61f, R);
                // the words' middle 22.1 under the pill's top (one line 354.27 + 11.5; two lines 474.73 … 519.33)
                var tw = Words(tip.transform, "Text", tipWords[i], 18f, Brown, 1695.63f - tipAt[i].x, 0.1f, 184f, 44f, TL, HorizontalAlignmentOptions.Left, tipKeys[i], new Vector2(234.5f, 61f));
                tw.lineSpacing = -6f;
                tip.gameObject.SetActive(false); v.tips[i] = tip.gameObject;
            }
            v.furnitureButton = buttons[0]; v.albumButton = buttons[1]; v.storageButton = buttons[2];
            // the dots on the tiles' top right corner (115:1398: node 20.5 at 98.51, -3.02 from the 앨범 tile's node; 3 px white edge)
            v.albumBadge = Dot(buttons[1].transform, 97.31f, -4.12f, new Vector2(115f, 115f));
            v.storageBadge = Dot(buttons[2].transform, 97.31f, -4.12f, new Vector2(114.5f, 114.5f)); v.storageBadgeText = null;
            v.toolbar = bar.gameObject;

            // the hint pill at the top (115:1255: the half-clear cream 992 x 142.6 at y 26.45, round ends; its words 38 px, two
            // lines 45.6 apart, 62 in from the sides)
            var hint = Node(root, "Hint", 464f, 26.45f, 992f, 142.6f, T);
            // its middle stays at y 97.75 for one line or two (115:1255: 26.45 + 142.6 / 2; 115:1398: 49.25 + 97 / 2)
            hint.pivot = new Vector2(0.5f, 0.5f); hint.anchoredPosition = new Vector2(0f, -97.75f);
            Shape(hint.gameObject, new Color(Cream.r, Cream.g, Cream.b, 0.6f), Vector4.one * 71.3f);
            var ht = Words(hint, "Text", "", 38f, Brown, 0f, 0f, 992f, 142.6f, C, HorizontalAlignmentOptions.Center, null, new Vector2(992f, 142.6f));
            Stretch(ht.rectTransform, 62f, 24f); ht.textWrappingMode = TextWrappingModes.Normal; ht.lineSpacing = LineSpacingFor(ht, 45.6f);
            hint.gameObject.SetActive(false); v.hint = hint; v.hintText = ht;

            // the star hunt (user 2026-11-27: "별찾기 … 코인 보는 쪽 Row줄이랑 같이 … 미션 설명은 그 밑에"): the hint's half-clear cream
            // pill in the top row, its middle level with the coin pill's (y 104), 38 px; what to do in a smaller pill under it
            var hunt = Node(root, "Hunt", 760f, 55.5f, 400f, 97f, T);
            hunt.pivot = new Vector2(0.5f, 0.5f); hunt.anchoredPosition = new Vector2(0f, -104f);
            Shape(hunt.gameObject, new Color(Cream.r, Cream.g, Cream.b, 0.6f), Vector4.one * 48.5f);
            var hut = Words(hunt, "Text", "★ 별 찾기 0/5   2:00", 38f, Brown, 0f, 0f, 400f, 97f, C, HorizontalAlignmentOptions.Center, null, new Vector2(400f, 97f));
            Stretch(hut.rectTransform, 0f, 0f);
            hunt.gameObject.SetActive(false);
            var info = Node(root, "HuntInfo", 760f, 164.5f, 400f, 56f, T);
            Shape(info.gameObject, new Color(Cream.r, Cream.g, Cream.b, 0.6f), Vector4.one * 28f);
            var hit = Words(info, "Text", "", 26f, Brown, 0f, 0f, 400f, 56f, C, HorizontalAlignmentOptions.Center, null, new Vector2(400f, 56f));
            Stretch(hit.rectTransform, 0f, 0f);
            info.gameObject.SetActive(false);
            v.hunt = hunt; v.huntText = hut; v.huntInfo = info; v.huntInfoText = hit;

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

        /// <summary>The "something new" dot (Figma 115:1546: #FD95A9, a 3 px white edge, 23.5 with the edge), its pivot in its middle
        /// so its little pulse grows from there.</summary>
        static GameObject Dot(Transform parent, float x, float y, Vector2 parentSize)
        {
            var d = Node(parent, "NewDot", x, y, 23.5f, 23.5f, TL, parentSize);
            d.pivot = new Vector2(0.5f, 0.5f); d.anchoredPosition += new Vector2(11.75f, -11.75f);
            Shape(d.gameObject, NewDot, Vector4.one * 11.75f, 3f, Color.white);
            d.gameObject.SetActive(false);
            return d.gameObject;
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
