using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// A shop (Figma 11-26 "상점" 88:1143): the stitched cream sheet on the right of the keeper, ◀ back and the shop's name at the
    /// top, the 부탁 보기 pill, the keeper's field (name and what they say, with a little next-step pill after a purchase), four
    /// item cards a page (picture, name, price, 구매) and ◀ 1/2 ▶. 부탁 보기 turns the cards into the keeper's requests, drawn like
    /// the rows of the quest window (88:2130) at 70 % and as wide as the keeper's field: 수락할게요, 찾으러 가기, 보상 받기, ♥ 완료. Prefab
    /// Assets/_UI수정/3_마을/Shop.prefab, opened with <see cref="Get"/>; the village hands it the lists and listens.
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        /// <summary>One thing for sale.</summary>
        public struct Item
        {
            public string id, name;
            public int price;
            public Sprite icon;
            /// <summary>Enough coins (otherwise the 구매 pill is greyed; pressing it still lets the keeper say so).</summary>
            public bool afford;
        }

        [Serializable]
        public class Card
        {
            public GameObject root;
            public Image icon;
            public TMP_Text name, price;
            public Button buy;
            public TMP_Text buyText;
        }

        [Header("위")]
        public TMP_Text titleText;
        public Button backButton, tabButton;
        public TMP_Text tabText;
        [Header("가게 주인")]
        public TMP_Text keeperText, lineText;
        [Tooltip("물건을 산 뒤 다음 할 일 (지금 놓기 · 입혀 보기)")] public Button lineButton;
        public TMP_Text lineButtonText;
        [Tooltip("말 상자의 너비 (버튼이 없을 때 / 있을 때)")] public float lineWidth = 950f, lineWidthWithButton = 746f;
        [Header("물건 (한 쪽에 넷)")]
        public GameObject itemsGroup;
        public Card[] cards = new Card[0];
        [Tooltip("코인이 모자랄 때 구매 버튼 색")] public Color poorTint = new Color(0.82f, 0.8f, 0.8f, 1f);
        [Header("부탁 (한 쪽에 셋)")]
        public GameObject asksGroup;
        public AskRow[] rows = new AskRow[0];
        public AskStyle style = new AskStyle();
        [Header("쪽 넘기기")]
        public Button prevButton, nextButton;
        public TMP_Text pageText;
        public TMP_Text emptyText;

        public Action<string> OnBuy, OnAccept, OnClaim, OnGo;
        public Action OnClose;

        readonly List<Item> items = new List<Item>();
        readonly List<Ask> asks = new List<Ask>();
        Action lineAction;
        bool askMode, hooked;
        int page;
        static ShopView current;

        public bool Visible => isActiveAndEnabled;
        public bool QuestTabOpen => askMode;
        public int Page => page;
        public int PageCount => Mathf.Max(1, (Count + PerPage - 1) / PerPage);
        int PerPage => Mathf.Max(1, askMode ? rows.Length : cards.Length);
        int Count => askMode ? asks.Count : items.Count;

        /// <summary>The shop window (the prefab listed as "Shop"), made once per scene.</summary>
        public static ShopView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Shop");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<ShopView>(); current.name = "Shop";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        void Awake() => Hook();
        void OnDestroy() { if (current == this) current = null; }

        void Hook()
        {
            if (hooked) return; hooked = true;
            if (backButton != null) backButton.onClick.AddListener(() => OnClose?.Invoke());
            if (tabButton != null) tabButton.onClick.AddListener(() => ShowAsks(!askMode));
            if (lineButton != null) lineButton.onClick.AddListener(() => lineAction?.Invoke());
            if (prevButton != null) prevButton.onClick.AddListener(() => Turn(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Turn(1));
            for (int i = 0; i < cards.Length; i++)
            {
                int k = i;
                if (cards[i].buy != null) cards[i].buy.onClick.AddListener(() => { int n = page * cards.Length + k; if (n < items.Count) OnBuy?.Invoke(items[n].id); });
            }
            for (int i = 0; i < rows.Length; i++)
            {
                int k = i;
                if (rows[i].button != null) rows[i].button.onClick.AddListener(() => Press(page * rows.Length + k));
            }
        }

        void Press(int n)
        {
            if (n >= asks.Count) return;
            var a = asks[n];
            if (a.onPress != null) { a.onPress(); return; }
            switch (a.state)
            {
                case AskState.Open: OnAccept?.Invoke(a.id); break;
                case AskState.Doing: OnGo?.Invoke(a.id); break;
                case AskState.Ready: OnClaim?.Invoke(a.id); break;
            }
        }

        // ---------------- what it shows ----------------
        /// <summary>Opens on a shop: its name, the keeper and their line, the things for sale and the requests (on the requests
        /// at once when <paramref name="openAsks"/>, e.g. a reward is waiting).</summary>
        public void Show(string title, string keeper, string line, IList<Item> forSale, IList<Ask> requests, bool openAsks = false)
        {
            Hook();
            gameObject.SetActive(true);
            titleText.text = title; keeperText.text = keeper;
            SetLine(line);
            items.Clear(); if (forSale != null) items.AddRange(forSale);
            asks.Clear(); if (requests != null) asks.AddRange(requests);
            askMode = openAsks && asks.Count > 0; page = 0;
            Draw();
        }

        /// <summary>The cards again (coins or the box changed): the page stays.</summary>
        public void SetItems(IList<Item> forSale) { items.Clear(); if (forSale != null) items.AddRange(forSale); Draw(); }

        /// <summary>The requests again (one was accepted or paid out): the page stays.</summary>
        public void SetAsks(IList<Ask> requests) { asks.Clear(); if (requests != null) asks.AddRange(requests); if (asks.Count == 0) askMode = false; Draw(); }

        /// <summary>The keeper's line; with <paramref name="action"/> the pill at the end of the field does the next step.</summary>
        public void SetLine(string text, string action = null, Action onAction = null)
        {
            lineText.text = text ?? "";
            bool on = !string.IsNullOrEmpty(action) && onAction != null;
            lineAction = on ? onAction : null;
            if (lineButton != null) { lineButton.gameObject.SetActive(on); if (on) lineButtonText.text = action; }
            var rt = lineText.rectTransform; rt.sizeDelta = new Vector2(on ? lineWidthWithButton : lineWidth, rt.sizeDelta.y);
        }

        public string LineShown => lineText != null ? lineText.text : "";
        public bool LineActionShown => lineButton != null && lineButton.gameObject.activeSelf;
        public bool QaLineAction() { if (!LineActionShown) return false; lineButton.onClick.Invoke(); return true; }

        public void Hide() => gameObject.SetActive(false);

        /// <summary>The things for sale (false) or the requests (true).</summary>
        public void ShowAsks(bool on)
        {
            on = on && asks.Count > 0;
            if (on == askMode) return;
            askMode = on; page = 0;
            Draw();
        }

        public void Turn(int dir)
        {
            int to = Mathf.Clamp(page + dir, 0, PageCount - 1);
            if (to == page) return;
            page = to; Draw();
        }

        void Draw()
        {
            page = Mathf.Clamp(page, 0, PageCount - 1);
            if (itemsGroup != null) itemsGroup.SetActive(!askMode);
            if (asksGroup != null) asksGroup.SetActive(askMode);
            if (askMode) DrawAsks(); else DrawItems();
            // the tab: only when the keeper has requests; (!) while one waits for an answer or a reward
            if (tabButton != null)
            {
                tabButton.gameObject.SetActive(asks.Count > 0);
                bool news = asks.Any(a => a.state == AskState.Open || a.state == AskState.Ready);
                tabText.text = Loc.Get(askMode ? "shop.items" : news ? "shop.asks.new" : "shop.asks");
            }
            bool pages = PageCount > 1;
            if (prevButton != null) { prevButton.gameObject.SetActive(pages); prevButton.interactable = page > 0; }
            if (nextButton != null) { nextButton.gameObject.SetActive(pages); nextButton.interactable = page < PageCount - 1; }
            if (pageText != null) { pageText.gameObject.SetActive(pages); pageText.text = (page + 1) + "/" + PageCount; }
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(Count == 0);
                emptyText.text = Count == 0 && asks.Count == 0 && askMode ? Loc.Get("shop.no_asks") : "";
            }
        }

        void DrawItems()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i]; int n = page * cards.Length + i;
                bool on = n < items.Count; c.root.SetActive(on);
                if (!on) continue;
                var it = items[n];
                c.root.name = "Card_" + it.id;
                c.icon.sprite = it.icon; c.icon.enabled = it.icon != null;
                c.name.text = it.name; c.price.text = it.price.ToString();
                c.buy.name = "Buy_" + it.id;
                c.buy.targetGraphic.color = it.afford ? Color.white : poorTint;
                c.buyText.text = Loc.Get("shop.buy");
            }
        }

        void DrawAsks()
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var r = rows[i]; int n = page * rows.Length + i;
                bool on = n < asks.Count; r.root.SetActive(on);
                if (on) r.Draw(asks[n], style);
            }
        }

        // ---------------- test hooks ----------------
        /// <summary>Presses a shown button by its name ("Buy_Fur_Armchair", "Quest_Accept_Q_Tulip"); false when there is none.</summary>
        public bool QaClick(string buttonName)
        {
            if (!Visible) return false;
            var b = GetComponentsInChildren<Button>(false).FirstOrDefault(x => x.name == buttonName);
            if (b == null || !b.interactable) return false;
            b.onClick.Invoke(); return true;
        }

        public IEnumerable<string> ShownCards => cards.Where(c => c.root.activeSelf).Select(c => c.root.name);
        public IEnumerable<string> ShownRows => rows.Where(r => r.root.activeSelf).Select(r => r.root.name);
    }
}
