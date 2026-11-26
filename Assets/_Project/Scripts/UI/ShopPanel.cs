using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Data;
using Mongle.Village;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>
    /// Item cards in one panel (Figma D-020: 다람 가구점 / 보관함). Shop mode: keeper line + buy/place buttons. Storage mode: owned-but-unplaced
    /// items with a place button. Pure view: it reports clicks through callbacks and reads WorldState; it holds no rules.
    /// Everything is laid out on the 1920x1080 artboard (FigmaLayout), hanging from the screen centre.
    /// </summary>
    public class ShopPanel
    {
        public Action<FurnitureDefinition> OnBuy, OnPlace;
        /// <summary>An owned accessory: go and put it on a friend (null = no such button).</summary>
        public Action<FurnitureDefinition> OnDress;
        public Action<QuestDefinition> OnAcceptQuest, OnClaimQuest;
        public Action OnClose;
        /// <summary>Called with the panel after every rebuild (D-035: the screen's prefab is laid over it).</summary>
        public Action<Transform> Skin;
        public bool Visible => root != null && root.activeSelf;
        public bool StorageMode { get; private set; }

        readonly GameObject root, shopGroup, storeGroup;
        readonly Text title, keeper, line, pageLabel, emptyLabel, tabLabel, lineActionLabel;
        readonly Button closeShop, closeStore, prev, next, tabButton, lineAction;
        Action lineActionHandler;
        bool questMode; IList<QuestDefinition> quests = new List<QuestDefinition>();
        readonly RectTransform cards;
        readonly Image storeView; readonly ScrollRect storeScroll;
        // 보관함: the scrolling card area on the sheet (artboard), right of it the Figma scroll track (x 1794)
        static readonly Rect StoreView = new Rect(624f, 262f, 1152f, 668f);
        const float StoreRowPitch = CardH + 26f, StorePad = 12f;
        WorldState world; List<FurnitureDefinition> items = new List<FurnitureDefinition>();
        int page;
        const int PerPage = 4;
        static readonly Color Gold = new Color(1f, 0.84f, 0.38f, 1f);
        static readonly Color Soft = new Color(0.5f, 0.38f, 0.28f, 1f);
        // the card grid (shop: under the keeper's field; storage: inside the 보관함 sheet), card pitch from the Figma
        const float CardW = 249.5f, CardH = 397f, Pitch = 287f;

        public bool QuestTabOpen => questMode;
        public IEnumerable<string> VisibleCardNames => cards.Cast<Transform>().Select(c => c.name);
        public RectTransform CardsRoot => cards;

        public ShopPanel(Transform canvas, string name = "ShopPanel")
        {
            var rt = UiKit.FigRoot(canvas, name); root = rt.gameObject;
            // ---- shop sheet ----
            shopGroup = UiKit.FigRoot(rt, "Shop").gameObject;
            UiKit.Fig(shopGroup.transform, "shop_panel", UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, true);
            closeShop = UiKit.FigButton(shopGroup.transform, "shop_back", () => OnClose?.Invoke(), UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_back");
            UiKit.Fig(shopGroup.transform, "shop_back_arrow", UiKit.Edge.Middle, UiKit.Edge.Middle);
            UiKit.HiddenLabel(closeShop, "나가기");
            title = UiKit.FigLabel(shopGroup.transform, "", 50, UiKit.FigBrown, 1128f, 252f, 640f, 90f, TextAnchor.MiddleLeft); title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            UiKit.Fig(shopGroup.transform, "shop_field", UiKit.Edge.Middle, UiKit.Edge.Middle);
            keeper = UiKit.FigLabel(shopGroup.transform, "", 38, new Color32(0xff, 0xb5, 0x8a, 255), 780f, 394f, 150f, 70f);
            line = UiKit.FigLabel(shopGroup.transform, "", 30, UiKit.FigBrown, 1330f, 394f, 880f, 110f, TextAnchor.MiddleLeft);
            // the line stays inside the keeper's field: it wraps to two lines and shrinks if it still does not fit (D-024)
            line.horizontalOverflow = HorizontalWrapMode.Wrap; line.verticalOverflow = VerticalWrapMode.Truncate;
            line.resizeTextForBestFit = true; line.resizeTextMinSize = 18; line.resizeTextMaxSize = 30; line.lineSpacing = 1.05f;
            // the keeper's next-step button after a purchase ("지금 놓기", "입혀 보기"), on the right of the line
            lineAction = UiKit.FigButton(shopGroup.transform, "hud_dlgbtn", () => lineActionHandler?.Invoke(), UiKit.Edge.Middle, UiKit.Edge.Middle, 770f, 405f - 540f + 134f, "LineAction");
            Shrink(lineAction, 232f, 66f, 1806f, 394f); lineActionLabel = UiKit.FigLabel(lineAction.transform, "", 28, UiKit.FigBrown, 960f, 540f, 220f, 60f); lineActionLabel.rectTransform.anchoredPosition = Vector2.zero;
            PinRight(lineAction); UiKit.Slice(lineAction.GetComponent<Image>(), true); UiKit.AutoFit(lineAction.GetComponent<Image>(), lineActionLabel, 232f, 60f, 420f);
            lineAction.gameObject.SetActive(false);
            tabButton = UiKit.FigButton(shopGroup.transform, "ride_btn_see", () => { questMode = !questMode; Rebuild(); }, UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_tab");
            Shrink(tabButton, 260f, 74f, 1780f, 252f); tabLabel = UiKit.FigLabel(tabButton.transform, "", 30, UiKit.FigBrown, 960f, 540f, 250f, 60f); tabLabel.rectTransform.anchoredPosition = Vector2.zero;
            PinRight(tabButton); UiKit.Slice(tabButton.GetComponent<Image>(), true); UiKit.AutoFit(tabButton.GetComponent<Image>(), tabLabel, 260f, 60f, 420f);
            prev = UiKit.FigButton(shopGroup.transform, "shop_pager_prev", () => SetPage(page - 1), UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_prev");
            next = UiKit.FigButton(shopGroup.transform, "shop_pager_next", () => SetPage(page + 1), UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_next");
            pageLabel = UiKit.FigLabel(shopGroup.transform, "", 36, UiKit.FigBrown, 1238f, 937f, 120f, 60f);
            // own names: these move between the two sheets, and the prefab finds them by name (D-035)
            title.name = "Title"; keeper.name = "Keeper"; line.name = "Line"; pageLabel.name = "Page"; tabLabel.name = "TabLabel"; lineActionLabel.name = "ActionLabel";
            // ---- storage sheet (보관함) ----
            storeGroup = UiKit.FigRoot(rt, "Storage").gameObject;
            // D-025: the Figma sheet would run under the top bar and the gear; it is drawn at 88 % and centred below them
            var sg = (RectTransform)storeGroup.transform; sg.localScale = Vector3.one * 0.88f; sg.anchoredPosition = new Vector2(-219f, -52f);
            UiKit.Fig(storeGroup.transform, "inv_panel", UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, true);
            UiKit.Fig(storeGroup.transform, "inv_tab", UiKit.Edge.Middle, UiKit.Edge.Middle);
            UiKit.FigLabel(storeGroup.transform, "보관함", 61, UiKit.FigBrown2, 784f, 100f, 300f, 90f);
            // the cards scroll inside the sheet (no pager under it); the Figma track and thumb become the scroll bar
            storeView = UiKit.FigRect(storeGroup.transform, "Viewport", StoreView, UiKit.Edge.Middle, UiKit.Edge.Middle);
            storeView.color = new Color(1f, 1f, 1f, 0f); storeView.raycastTarget = true;   // catches the wheel and drags between cards
            storeView.gameObject.AddComponent<RectMask2D>();
            storeScroll = storeView.gameObject.AddComponent<ScrollRect>();
            storeScroll.viewport = storeView.rectTransform; storeScroll.horizontal = false; storeScroll.vertical = true;
            storeScroll.movementType = ScrollRect.MovementType.Clamped; storeScroll.scrollSensitivity = 60f; storeScroll.inertia = true; storeScroll.decelerationRate = 0.12f;
            var track = UiKit.Fig(storeGroup.transform, "inv_scroll_track", UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, true, "ScrollBar");
            var area = UiKit.Rect(track.transform, "SlidingArea", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = UiKit.Rect(area, "Handle", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hi = handle.gameObject.AddComponent<Image>(); hi.sprite = UiKit.UiSprite("inv_scroll_thumb"); hi.type = Image.Type.Sliced; hi.pixelsPerUnitMultiplier = 2f;
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.handleRect = handle; bar.targetGraphic = hi; bar.direction = Scrollbar.Direction.BottomToTop;
            storeScroll.verticalScrollbar = bar; storeScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            closeStore = UiKit.FigButton(storeGroup.transform, "inv_close", () => OnClose?.Invoke(), UiKit.Edge.Middle, UiKit.Edge.Middle, 0, 0, "Btn_close");
            UiKit.HiddenLabel(closeStore, "나가기");
            // ---- shared ----
            cards = UiKit.Rect(rt, "Cards", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cards.sizeDelta = new Vector2(4 * Pitch, CardH);
            emptyLabel = UiKit.FigLabel(rt, "", 40, UiKit.FigBrown, 1230f, 640f, 1000f, 200f); emptyLabel.name = "Empty";
            root.SetActive(false);
        }

        /// <summary>Resizes a sprite button to a smaller pill and puts it at an artboard centre point (children keep centred labels).</summary>
        static void Shrink(Button b, float w, float h, float artCx, float artCy)
        {
            var r = (RectTransform)b.transform; r.sizeDelta = new Vector2(w, h); r.anchoredPosition = new Vector2(artCx - 960f, -(artCy - 540f));
        }

        /// <summary>Keeps a button's right edge where it is, so a longer label grows it to the left.</summary>
        static void PinRight(Button b)
        {
            var r = (RectTransform)b.transform; var p = r.anchoredPosition; r.pivot = new Vector2(1f, 0.5f); r.anchoredPosition = p;
        }

        public static void CoinIcon(Transform parent, Vector2 pos, float size, float anchorY = 0.5f)
        {
            var c = UiKit.Panel(parent, "Coin", Gold, new Vector2(0f, anchorY), pos, new Vector2(size, size), false);
            UiKit.Label(c.transform, "몽", (int)(size * 0.55f), new Color(0.62f, 0.4f, 0.1f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
        }

        public void ShowShop(ShopDefinition shop, WorldState w, string keeperLine, IList<QuestDefinition> shopQuests = null, bool openQuests = false)
        {
            StorageMode = false; world = w; quests = shopQuests ?? new List<QuestDefinition>(); questMode = openQuests && quests.Count > 0;
            title.text = shop.displayName; keeper.text = shop.keeperName; SetLine(keeperLine);
            items = shop.items.Where(i => i != null).ToList(); page = 0; emptyLabel.text = "";
            root.SetActive(true); Rebuild();
        }

        public void ShowStorage(WorldState w, IEnumerable<FurnitureDefinition> catalogItems)
        {
            StorageMode = true; world = w; questMode = false; quests = new List<QuestDefinition>();
            title.text = "보관함"; keeper.text = "내 물건"; SetLine("산 물건은 모두 여기 있어요. 가구는 '마을에 놓기', 악세서리는 '꾸며주기'로 친구에게 입혀요.");
            // sorted by kind: furniture, then plants, then accessories (the 전체 / 가구 / 악세서리 tabs read the same way)
            items = catalogItems.Where(i => i != null && w.Owned(i.id) > 0).OrderBy(i => (int)i.category).ToList(); page = 0;
            root.SetActive(true); Rebuild();
            Canvas.ForceUpdateCanvases(); storeScroll.verticalNormalizedPosition = 1f;   // opens at the top
        }

        /// <summary>The keeper's line; with <paramref name="action"/> a button beside it does the next step (e.g. place what was just bought).</summary>
        public void SetLine(string text, string action = null, Action onAction = null)
        {
            line.text = text;
            bool on = !string.IsNullOrEmpty(action) && onAction != null;
            lineActionHandler = on ? onAction : null;
            lineAction.gameObject.SetActive(on && !StorageMode);
            if (on) { UiKit.SetButtonText(lineAction, action); lineAction.GetComponent<FitToText>().Apply(); }
            // room for the button: a narrower, slightly smaller line
            // the field runs 700..1810 (artboard); the name takes its left end, the button (when shown) its right end
            float right = on ? 1806f - Mathf.Max(232f, ((RectTransform)lineAction.transform).sizeDelta.x) - 24f : 1790f, left = 862f;
            var rt = line.rectTransform; rt.sizeDelta = new Vector2(right - left, 104f);
            rt.anchoredPosition = new Vector2((left + right) / 2f - 960f, -(394f - 540f));
            line.resizeTextMaxSize = on ? 28 : 30;
        }
        public string LineShown => line.text;
        public bool LineActionShown => lineAction.gameObject.activeSelf;
        public bool QaLineAction() { if (!LineActionShown) return false; lineAction.onClick.Invoke(); return true; }
        public void Hide() { root.SetActive(false); }

        /// <summary>Re-draw after coins/inventory changed (keeps the page).</summary>
        public void Refresh()
        {
            if (!Visible) return;
            if (StorageMode) items = items.Where(i => world.Owned(i.id) > 0).ToList();
            Rebuild();
        }

        void SetPage(int p) { page = Mathf.Clamp(p, 0, Mathf.Max(0, (items.Count - 1) / PerPage)); Rebuild(); }

        void Rebuild() { RebuildCards(); Skin?.Invoke(root.transform); }

        void RebuildCards()
        {
            shopGroup.SetActive(!StorageMode); storeGroup.SetActive(StorageMode);
            // the keeper's field (name + line) only belongs to the shop; the 보관함 sheet shows its hint under the tab instead
            keeper.gameObject.SetActive(!StorageMode);
            if (StorageMode)
            {
                // the hint line sits on the sheet (the shop's keeper field is part of the hidden shop group, so draw the text here)
                line.transform.SetParent(storeGroup.transform, false);
                // one line between the tab and the cards (it shrinks rather than wrapping a lone word onto a second line)
                line.rectTransform.anchoredPosition = new Vector2(1209f - 960f, -(212f - 540f)); line.rectTransform.sizeDelta = new Vector2(1180f, 44f); line.alignment = TextAnchor.MiddleCenter; line.resizeTextMaxSize = 28;
                emptyLabel.transform.SetParent(storeGroup.transform, false);   // scaled with the sheet
                // the cards are the scroll content: full width of the viewport, hanging from its top
                cards.SetParent(storeView.transform, false);
                cards.anchorMin = new Vector2(0f, 1f); cards.anchorMax = new Vector2(1f, 1f); cards.pivot = new Vector2(0.5f, 1f); cards.anchoredPosition = Vector2.zero;
                storeScroll.content = cards;
                prev.gameObject.SetActive(false); next.gameObject.SetActive(false); pageLabel.gameObject.SetActive(false);
            }
            else
            {
                line.transform.SetParent(shopGroup.transform, false); line.alignment = TextAnchor.MiddleLeft;
                storeScroll.content = null;
                cards.SetParent(root.transform, false); emptyLabel.transform.SetParent(root.transform, false);
                cards.anchorMin = cards.anchorMax = cards.pivot = new Vector2(0.5f, 0.5f); cards.sizeDelta = new Vector2(4 * Pitch, CardH);
                pageLabel.transform.SetParent(shopGroup.transform, false); pageLabel.rectTransform.anchoredPosition = new Vector2(1238f - 960f, -(937f - 540f));
                prev.transform.SetParent(shopGroup.transform, false); next.transform.SetParent(shopGroup.transform, false);
                var pr = FigmaLayout.Rects["shop_pager_prev"]; var nr = FigmaLayout.Rects["shop_pager_next"];
                ((RectTransform)prev.transform).sizeDelta = pr.size; ((RectTransform)next.transform).sizeDelta = nr.size;
                ((RectTransform)prev.transform).anchoredPosition = new Vector2(pr.center.x - 960f, -(pr.center.y - 540f)); ((RectTransform)next.transform).anchoredPosition = new Vector2(nr.center.x - 960f, -(nr.center.y - 540f));
                cards.anchoredPosition = new Vector2(1246.3f - 960f, -(678f - 540f));
            }
            for (int i = cards.childCount - 1; i >= 0; i--)
            {
                var c = cards.GetChild(i).gameObject; c.transform.SetParent(null);   // gone from the list at once (the prefab is laid over it next)
                if (Application.isPlaying) UnityEngine.Object.Destroy(c); else UnityEngine.Object.DestroyImmediate(c);
            }
            bool hasQuests = !StorageMode && quests.Count > 0;
            tabButton.gameObject.SetActive(hasQuests);
            if (hasQuests)
            {
                bool attention = quests.Any(q => world.StatusOf(q) == QuestStatus.Available || world.StatusOf(q) == QuestStatus.ReadyToClaim);
                UiKit.SetButtonText(tabButton, questMode ? "물건 보기" : (attention ? "부탁 보기 (!)" : "부탁 보기"));
            }
            if (hasQuests && questMode)
            {
                prev.gameObject.SetActive(false); next.gameObject.SetActive(false); pageLabel.gameObject.SetActive(false);
                emptyLabel.text = "";
                BuildQuestRows();
                return;
            }
            if (StorageMode) { BuildStorageGrid(); return; }
            int pages = Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PerPage));
            page = Mathf.Clamp(page, 0, pages - 1);
            prev.gameObject.SetActive(pages > 1); next.gameObject.SetActive(pages > 1); pageLabel.gameObject.SetActive(pages > 1);
            pageLabel.text = (page + 1) + "/" + pages;
            emptyLabel.rectTransform.anchoredPosition = new Vector2((StorageMode ? 1209f : 1230f) - 960f, -((StorageMode ? 540f : 640f) - 540f));
            emptyLabel.text = items.Count == 0 ? (StorageMode ? "보관함이 비었어요.\n가게에서 마음에 드는 물건을 사 봐요!" : "") : "";
            var slice = items.Skip(page * PerPage).Take(PerPage).ToList();
            float total = (slice.Count - 1) * Pitch;
            for (int i = 0; i < slice.Count; i++)
                BuildCard(slice[i], new Vector2(-total / 2f + i * Pitch, 0));
        }

        /// <summary>보관함: every stored item in rows of four, scrolling when there are more than fit; a short list sits in the middle.</summary>
        void BuildStorageGrid()
        {
            emptyLabel.rectTransform.anchoredPosition = new Vector2(StoreView.center.x - 960f, -(StoreView.center.y - 540f));
            emptyLabel.text = items.Count == 0 ? "보관함이 비었어요.\n가게에서 마음에 드는 물건을 사 봐요!" : "";
            int rows = Mathf.CeilToInt(items.Count / (float)PerPage);
            float need = rows == 0 ? 0f : 2f * StorePad + (rows - 1) * StoreRowPitch + CardH;
            float h = Mathf.Max(StoreView.height, need), top = (h - need) / 2f + StorePad;
            cards.sizeDelta = new Vector2(0f, h);
            for (int i = 0; i < items.Count; i++)
            {
                int r = i / PerPage, c = i % PerPage, inRow = Mathf.Min(PerPage, items.Count - r * PerPage);
                BuildCard(items[i], new Vector2(-(inRow - 1) * Pitch / 2f + c * Pitch, h / 2f - (top + r * StoreRowPitch + CardH / 2f)));
            }
            storeScroll.verticalScrollbar.gameObject.SetActive(need > StoreView.height + 1f);
        }

        void BuildQuestRows()
        {
            var shown = quests.Where(q => world.StatusOf(q) != QuestStatus.Locked).ToList();
            if (shown.Count == 0) { emptyLabel.text = "지금은 부탁이 없어요.\n다른 부탁을 끝내면 새 부탁이 생겨요."; emptyLabel.rectTransform.anchoredPosition = new Vector2(1230f - 960f, -(640f - 540f)); return; }
            // D-024: rows of 1140x150 under the keeper's field (field ends at 465; rows from 488, 160 apart; the sheet ends at 1003)
            const float RowW = 1140f, RowH = 150f, Top = 488f, Pitch = 160f;
            cards.anchoredPosition = new Vector2(1250f - 960f, 0f);
            for (int i = 0; i < shown.Count && i < 3; i++)
            {
                var q = shown[i]; var st = world.StatusOf(q);
                string tone = st == QuestStatus.Available ? "pink" : st == QuestStatus.Active ? "yellow" : st == QuestStatus.ReadyToClaim ? "mint" : "gray";
                var rowRt = UiKit.Rect(cards, "Quest_" + q.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                rowRt.sizeDelta = new Vector2(RowW, RowH); rowRt.anchoredPosition = new Vector2(0f, -(Top + RowH / 2f + i * Pitch - 540f));
                var row = rowRt.gameObject.AddComponent<Image>(); row.sprite = UiKit.UiSprite("q_row_" + tone); row.raycastTarget = false; UiKit.Slice(row);
                // left: icon, title, line (up to x 640); middle: reward pill (660..790) and progress under it; right: the button
                var icon = UiKit.SpriteImage(row.transform, "Icon", "q_ic_" + tone, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(96f, 96f));
                UiKit.TitleLabel(icon.transform, string.IsNullOrEmpty(q.title) ? "!" : q.title.Substring(0, 1), 38, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(90, 80));
                var title = UiKit.TitleLabel(row.transform, q.title, 34, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(140f, -42f), new Vector2(500f, 48f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
                title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate; title.resizeTextForBestFit = true; title.resizeTextMinSize = 22; title.resizeTextMaxSize = 34;
                var detail = UiKit.TitleLabel(row.transform, st == QuestStatus.Available ? q.request : st == QuestStatus.Done ? q.doneLine : q.acceptLine, 22, UiKit.FigBrown2, new Vector2(0f, 1f), new Vector2(140f, -100f), new Vector2(500f, 56f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
                detail.horizontalOverflow = HorizontalWrapMode.Wrap; detail.verticalOverflow = VerticalWrapMode.Truncate; detail.resizeTextForBestFit = true; detail.resizeTextMinSize = 16; detail.resizeTextMaxSize = 22;
                if (st != QuestStatus.Done)
                {
                    var pill = UiKit.SpriteImage(row.transform, "CoinPill", "q_coinpill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(660f, 14f), new Vector2(130f, 60f));
                    UiKit.TitleLabel(pill.transform, "+" + q.rewardCoins, 26, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(24f, 0f), new Vector2(80, 50));
                }
                string status = st == QuestStatus.Active ? "진행 " + world.ProgressOf(q) + "/" + q.targetCount : st == QuestStatus.ReadyToClaim ? "완료!" : st == QuestStatus.Done ? "끝냈어요" : "";
                var stl = UiKit.TitleLabel(row.transform, status, 24, UiKit.FigBrown2, new Vector2(0f, 0.5f), new Vector2(725f, -40f), new Vector2(160f, 36f));
                stl.resizeTextForBestFit = true; stl.resizeTextMinSize = 16; stl.resizeTextMaxSize = 24;
                if (st == QuestStatus.Done)
                {
                    UiKit.SpriteImage(row.transform, "Heart", "q_heart", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-210f, 0f), new Vector2(45.5f, 41f));
                    UiKit.TitleLabel(row.transform, "완료", 36, UiKit.FigBrown2, new Vector2(1f, 0.5f), new Vector2(-135f, 0f), new Vector2(110, 56));
                }
                else if (st == QuestStatus.Available || st == QuestStatus.ReadyToClaim)
                {
                    var b = UiKit.SpriteButton(row.transform, (st == QuestStatus.Available ? "Quest_Accept_" : "Quest_Claim_") + q.id, st == QuestStatus.Available ? "q_btn_pink" : "q_btn_yellow", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(236f, 76f),
                        () => { if (st == QuestStatus.Available) OnAcceptQuest?.Invoke(q); else OnClaimQuest?.Invoke(q); });
                    var bl = UiKit.TitleLabel(b.transform, st == QuestStatus.Available ? "수락할게요" : "보상 받기", 30, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(214, 44));
                    bl.horizontalOverflow = HorizontalWrapMode.Wrap; bl.verticalOverflow = VerticalWrapMode.Truncate; bl.resizeTextForBestFit = true; bl.resizeTextMinSize = 18; bl.resizeTextMaxSize = 30;
                }
            }
        }

        void BuildCard(FurnitureDefinition def, Vector2 pos)
        {
            int owned = world.Owned(def.id);
            var cardRt = UiKit.Rect(cards, "Card_" + def.id, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            cardRt.sizeDelta = new Vector2(CardW, CardH); cardRt.anchoredPosition = pos;
            var card = cardRt.gameObject.AddComponent<Image>(); card.sprite = UiKit.UiSprite("shop_card"); card.raycastTarget = false;
            // everything below is placed from the card's top-left corner (Figma card rect 691,480)
            var bg = UiKit.SpriteImage(card.transform, "IconBg", "shop_card_img", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(25f, -26.8f), new Vector2(195.5f, 155.5f));
            var icon = IconBaker.Get(def);
            if (icon != null)
            {
                var img = UiKit.Fixed(bg.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150, 150)).gameObject.AddComponent<Image>();
                img.sprite = icon; img.preserveAspect = true; img.raycastTarget = false;
            }
            var nameLabel = UiKit.TitleLabel(card.transform, def.displayName, 30, UiKit.FigBrown, new Vector2(0f, 1f), new Vector2(CardW / 2f, -203f), new Vector2(236, 44));
            nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap; nameLabel.resizeTextForBestFit = true; nameLabel.resizeTextMinSize = 20; nameLabel.resizeTextMaxSize = 30; // "하트 관람차 모형" fits on one line
            UiKit.Label(card.transform, def.description, 18, Soft, new Vector2(0f, 1f), new Vector2(CardW / 2f, -250f), new Vector2(228, 44), TextAnchor.UpperCenter, FontStyle.Normal);
            if (!StorageMode)
            {
                UiKit.SpriteImage(card.transform, "Coin", "shop_coin", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(17f, -278f), new Vector2(46f, 46f));
                UiKit.TitleLabel(card.transform, def.price.ToString(), 28, UiKit.FigBrown, new Vector2(0f, 1f), new Vector2(112f, -301f), new Vector2(90, 44), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
                if (owned > 0) UiKit.Label(card.transform, "가진 수 " + owned, 18, Soft, new Vector2(1f, 1f), new Vector2(-62f, -301f), new Vector2(110, 30), TextAnchor.MiddleRight, FontStyle.Normal);
                bool can = world.Coins >= def.price;
                bool place = owned > 0 && def.Placeable; // accessories are worn by friends, never placed
                bool dress = owned > 0 && def.IsAccessory && OnDress != null;
                bool two = place || dress;
                var buy = CardButton(card.transform, can ? "사기" : "코인이 모자라요", can ? UiKit.FigBrown : UiKit.FigBrown * 0.7f, two ? 95f : 195.5f, 55.5f, two ? 25f : 25f, () => OnBuy?.Invoke(def), can ? 28 : 18);
                buy.name = "Buy_" + def.id; buy.GetComponent<Image>().color = can ? Color.white : new Color(0.8f, 0.8f, 0.8f, 1f);
                if (place)
                {
                    var put = CardButton(card.transform, "놓기", UiKit.FigBrown, 95f, 55.5f, 125.5f, () => OnPlace?.Invoke(def), 28);
                    put.name = "Place_" + def.id; put.GetComponent<Image>().color = new Color(0.8f, 1f, 0.9f, 1f);
                }
                else if (dress)
                {
                    var wear = CardButton(card.transform, "입히기", UiKit.FigBrown, 95f, 55.5f, 125.5f, () => OnDress?.Invoke(def), 24);
                    wear.name = "Dress_" + def.id; wear.GetComponent<Image>().color = new Color(0.8f, 1f, 0.9f, 1f);
                }
            }
            else
            {
                UiKit.TitleLabel(card.transform, (def.IsAccessory ? "악세서리 " : "") + owned + "개", 28, UiKit.FigBrown, new Vector2(0f, 1f), new Vector2(CardW / 2f, -301f), new Vector2(230, 44));
                if (def.IsAccessory)
                {
                    // worn, not placed: straight to the dress-up
                    var wear = CardButton(card.transform, "꾸며주기", UiKit.FigBrown, 195.5f, 55.5f, 25f, () => OnDress?.Invoke(def), 28);
                    wear.name = "Dress_" + def.id;
                }
                else
                {
                    var put = CardButton(card.transform, "마을에 놓기", UiKit.FigBrown, 195.5f, 55.5f, 25f, () => OnPlace?.Invoke(def), 28);
                    put.name = "Place_" + def.id;
                }
            }
        }

        /// <summary>The pink pill of the Figma card (buy button), hanging from the card's top-left at x, 325 px down.</summary>
        static Button CardButton(Transform card, string text, Color textColor, float w, float h, float x, Action onClick, int fontSize)
        {
            var b = UiKit.SpriteButton(card, "Btn", "shop_buy", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -332f), new Vector2(w, h), onClick);
            var t = UiKit.TitleLabel(b.transform, text, fontSize, textColor, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w - 12f, fontSize * 1.4f));
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 14; t.resizeTextMaxSize = fontSize;
            UiKit.Slice(b.GetComponent<Image>(), true);
            return b;
        }

        // ---- test hooks ----
        public bool QaClick(string buttonName)
        {
            var t = cards.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == buttonName);
            if (t == null || !t.interactable) return false;
            t.onClick.Invoke(); return true;
        }
    }
}
