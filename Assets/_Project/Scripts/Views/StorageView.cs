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
    /// The 보관함 (Figma 11-26 "보관함" 88:825, empty 88:650): a ring binder. The left page holds everything bought and not yet
    /// used as square cards four to a row (×N in the blue pill, a blue edge on the chosen one), scrolling; above it the 전체 /
    /// 가구 / 악세서리 tabs, a search field and the 기본 정렬 ▼ order. The right page shows the chosen thing big with its name,
    /// what it is and 사용하기 (furniture is placed in the village, an accessory goes on a friend). Prefab
    /// Assets/_UI수정/3_마을/StorageBox.prefab, opened with <see cref="Get"/>; the village hands it the list and listens to
    /// <see cref="OnUse"/>.
    /// </summary>
    public class StorageView : MonoBehaviour
    {
        public enum Kind { Furniture, Accessory }

        /// <summary>One thing in the box.</summary>
        public struct Item
        {
            public string id, name, description;
            public Sprite icon;
            public int count;
            public Kind kind;
        }

        [Serializable]
        public class Tab
        {
            public Button button;
            public Image plate;
            public TMP_Text label;
        }

        [Header("위")]
        [Tooltip("전체 · 가구 · 악세서리")] public Tab[] tabs = new Tab[0];
        public Sprite tabOn, tabOff;
        public TMP_InputField search;
        [Tooltip("기본 정렬 · 이름 순 · 많은 순")] public TMP_Dropdown sort;
        public Button closeButton;
        [Header("왼쪽 장 (물건 칸)")]
        public ScrollRect scroll;
        public RectTransform content;
        [Tooltip("칸 하나 (복사해서 써요): Plate, Chosen, Icon, Badge/Count")] public GameObject cardTemplate;
        public int columns = 4;
        [Tooltip("칸 사이 간격 (왼쪽 위에서 다음 칸 왼쪽 위까지)")] public Vector2 step = new Vector2(267.42f, 267.5f);
        [Tooltip("첫 칸의 자리 (내용 왼쪽 위에서)")] public Vector2 first = new Vector2(7.46f, 5.82f);
        public TMP_Text emptyText;
        [Header("오른쪽 장 (고른 물건)")]
        public GameObject detail;
        public Image picture;
        public TMP_Text nameText, descriptionText;
        public Button useButton;
        public TMP_Text useText;

        public Action<string> OnUse;
        public Action OnClose;

        readonly List<Item> items = new List<Item>();
        readonly List<Item> shown = new List<Item>();
        readonly List<GameObject> cards = new List<GameObject>();
        int tab, order; string query = "", chosen;
        bool hooked;
        static StorageView current;

        public bool Visible => isActiveAndEnabled;
        public int TabIndex => tab;
        public string Chosen => chosen;
        public IEnumerable<string> ShownIds => shown.Select(i => i.id);

        /// <summary>The box (the prefab listed as "Storage"), made once per scene.</summary>
        public static StorageView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Storage");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<StorageView>(); current.name = "Storage";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        void Awake() => Hook();
        void OnDestroy() { if (current == this) current = null; }

        void Hook()
        {
            if (hooked) return; hooked = true;
            for (int i = 0; i < tabs.Length; i++) { int k = i; if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => SetTab(k)); }
            if (closeButton != null) closeButton.onClick.AddListener(() => OnClose?.Invoke());
            if (useButton != null) useButton.onClick.AddListener(() => { if (chosen != null) OnUse?.Invoke(chosen); });
            if (search != null) search.onValueChanged.AddListener(SetSearch);
            if (sort != null)
            {
                sort.ClearOptions();
                sort.AddOptions(new List<string> { Loc.Get("storage.sort"), Loc.Get("storage.sort.name"), Loc.Get("storage.sort.count") });
                sort.SetValueWithoutNotify(order);
                sort.onValueChanged.AddListener(SetOrder);
            }
        }

        // ---------------- what it shows ----------------
        /// <summary>Opens on everything in the box, on 전체 with the first thing chosen and the list at the top.</summary>
        public void Show(IList<Item> list)
        {
            Hook();
            gameObject.SetActive(true);
            tab = 0; query = ""; chosen = null;
            if (search != null) search.SetTextWithoutNotify("");
            SetItems(list);
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>The box again (something was used up): the tab, the search and the chosen thing stay where they can.</summary>
        public void SetItems(IList<Item> list)
        {
            items.Clear(); if (list != null) items.AddRange(list);
            Draw();
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetTab(int i) { i = Mathf.Clamp(i, 0, Mathf.Max(0, tabs.Length - 1)); if (i == tab) return; tab = i; Draw(); }
        public void SetSearch(string text) { text = (text ?? "").Trim(); if (text == query) return; query = text; Draw(); }
        public void SetOrder(int i) { if (i == order) return; order = i; if (sort != null) sort.SetValueWithoutNotify(i); Draw(); }

        /// <summary>Chooses a thing (its card gets the blue edge, the right page shows it).</summary>
        public void Select(string id)
        {
            if (!shown.Any(i => i.id == id)) return;
            chosen = id; Marks(); Detail();
        }

        void Draw()
        {
            IEnumerable<Item> q = items.Where(i => i.count > 0);
            if (tab == 1) q = q.Where(i => i.kind == Kind.Furniture);
            else if (tab == 2) q = q.Where(i => i.kind == Kind.Accessory);
            if (query.Length > 0) q = q.Where(i => (i.name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            if (order == 1) q = q.OrderBy(i => i.name, StringComparer.Ordinal);
            else if (order == 2) q = q.OrderByDescending(i => i.count);
            shown.Clear(); shown.AddRange(q);
            if (chosen == null || !shown.Any(i => i.id == chosen)) chosen = shown.Count > 0 ? shown[0].id : null;

            for (int i = 0; i < Mathf.Max(shown.Count, cards.Count); i++)
            {
                if (i >= shown.Count) { cards[i].SetActive(false); continue; }
                if (i >= cards.Count)
                {
                    var c = Instantiate(cardTemplate, content, false);
                    int k = i; var b = c.GetComponent<Button>(); if (b != null) b.onClick.AddListener(() => { if (k < shown.Count) Select(shown[k].id); });
                    cards.Add(c);
                }
                Fill(cards[i], shown[i], i);
            }
            int rows = (shown.Count + columns - 1) / columns;
            var t = (RectTransform)cardTemplate.transform;
            float need = rows == 0 ? 0f : 2f * first.y + (rows - 1) * step.y + t.sizeDelta.y;
            float view = scroll != null ? ((RectTransform)scroll.viewport.transform).rect.height : need;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(need, view));
            if (scroll != null && scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(need > view + 1f);

            for (int i = 0; i < tabs.Length; i++) if (tabs[i].plate != null) tabs[i].plate.sprite = i == tab ? tabOn : tabOff;
            if (emptyText != null)
            {
                emptyText.gameObject.SetActive(shown.Count == 0);
                emptyText.text = Loc.Get(items.Any(i => i.count > 0) ? "storage.none" : "storage.empty");
            }
            Marks(); Detail();
        }

        void Fill(GameObject c, Item it, int i)
        {
            c.SetActive(true); c.name = "Card_" + it.id;
            var rt = (RectTransform)c.transform;
            rt.anchoredPosition = new Vector2(first.x + (i % columns) * step.x, -(first.y + (i / columns) * step.y));
            var icon = c.transform.Find("Icon").GetComponent<Image>(); icon.sprite = it.icon; icon.enabled = it.icon != null;
            c.transform.Find("Badge/Count").GetComponent<TMP_Text>().text = "x " + it.count;
        }

        void Marks()
        {
            for (int i = 0; i < cards.Count && i < shown.Count; i++) cards[i].transform.Find("Chosen").gameObject.SetActive(shown[i].id == chosen);
        }

        void Detail()
        {
            var it = shown.FirstOrDefault(i => i.id == chosen);
            bool on = chosen != null;
            if (detail != null) detail.SetActive(on);
            if (!on) return;
            picture.sprite = it.icon; picture.enabled = it.icon != null;
            nameText.text = it.name; descriptionText.text = it.description ?? "";
            if (useText != null) useText.text = Loc.Get("storage.use");
        }

        // ---------------- test hooks ----------------
        /// <summary>Chooses the thing and presses 사용하기 (false when it is not in the box or not shown).</summary>
        public bool QaUse(string id)
        {
            if (!Visible) return false;
            if (!shown.Any(i => i.id == id)) { tab = 0; query = ""; if (search != null) search.SetTextWithoutNotify(""); Draw(); }
            if (!shown.Any(i => i.id == id)) return false;
            Select(id); useButton.onClick.Invoke(); return true;
        }

        public int CountOf(string id) => items.Where(i => i.id == id).Select(i => i.count).FirstOrDefault();
    }
}
