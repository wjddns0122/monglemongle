using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The 퀘스트 window (Figma 11-26 "퀘스트" 88:2130): the stitched cream sheet with the 퀘스트 title tab, the 진행중 N / 완료 N /
    /// 내 이야기 tabs behind it, ✕, and the requests as rows (<see cref="AskRow"/>: the round icon, title, "giver : what they
    /// asked", the coin pill and the button; a finished one cream with ♥ 완료) in a scrolling list with the bar on the right.
    /// Prefab Assets/_UI수정/3_마을/QuestWindow.prefab, opened with <see cref="Get"/>; the village hands it the rows of the tab
    /// shown and listens to <see cref="OnTab"/>.
    /// </summary>
    public class QuestView : MonoBehaviour
    {
        [Serializable]
        public class Tab
        {
            public Button button;
            public Image plate;
            public TMP_Text label;
        }

        [Header("위")]
        [Tooltip("진행중 · 완료 · 내 이야기")] public Tab[] tabs = new Tab[0];
        public Sprite tabOn, tabOff;
        public Button closeButton;
        [Header("줄")]
        public ScrollRect scroll;
        public RectTransform content;
        [Tooltip("줄 하나 (복사해서 써요)")] public GameObject rowTemplate;
        [Tooltip("줄 사이 간격 (위에서 다음 줄 위까지)")] public float step = 200.53f;
        [Tooltip("첫 줄의 자리 (내용 왼쪽 위에서)")] public Vector2 first = new Vector2(6.42f, 6.22f);
        public AskStyle style = new AskStyle();
        [Tooltip("내 이야기에서 말해 준 것의 그림 (♥)")] public Sprite heart;
        public TMP_Text emptyText;

        public Action<int> OnTab;
        public Action OnClose;

        readonly List<Ask> asks = new List<Ask>();
        readonly List<AskRow> rows = new List<AskRow>();
        int tab; bool hooked;
        static QuestView current;

        public bool Visible => isActiveAndEnabled;
        public int TabIndex => tab;
        public IEnumerable<string> ShownIds => asks.Select(a => a.id);

        /// <summary>The window (the prefab listed as "Quest"), made once per scene.</summary>
        public static QuestView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Quest");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<QuestView>(); current.name = "Quest";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        void Awake() => Hook();
        void OnDestroy() { if (current == this) current = null; }

        void Hook()
        {
            if (hooked) return; hooked = true;
            for (int i = 0; i < tabs.Length; i++) { int k = i; if (tabs[i].button != null) tabs[i].button.onClick.AddListener(() => { if (k != tab) OnTab?.Invoke(k); }); }
            if (closeButton != null) closeButton.onClick.AddListener(() => OnClose?.Invoke());
        }

        public void Show() { Hook(); gameObject.SetActive(true); if (scroll != null) scroll.verticalNormalizedPosition = 1f; }
        public void Hide() => gameObject.SetActive(false);

        /// <summary>The rows of <paramref name="tabIndex"/>, the tab labels ("진행중 4", "완료 1", "내 이야기") and the words for an
        /// empty list. A new tab starts at the top; the same tab keeps its place (a live timer redraws it).</summary>
        public void Render(int tabIndex, IList<Ask> list, string empty, IList<string> tabLabels)
        {
            Hook();
            bool newTab = tabIndex != tab; tab = tabIndex;
            asks.Clear(); if (list != null) asks.AddRange(list);
            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i].plate != null) tabs[i].plate.sprite = i == tab ? tabOn : tabOff;
                if (tabLabels != null && i < tabLabels.Count && tabs[i].label != null) tabs[i].label.text = tabLabels[i];
            }
            for (int i = 0; i < Mathf.Max(asks.Count, rows.Count); i++)
            {
                if (i >= asks.Count) { rows[i].root.SetActive(false); continue; }
                if (i >= rows.Count)
                {
                    var go = Instantiate(rowTemplate, content, false);
                    var r = AskRow.Find(go); int k = i;
                    if (r.button != null) r.button.onClick.AddListener(() => Press(k));
                    rows.Add(r);
                }
                var row = rows[i]; row.root.SetActive(true);
                ((RectTransform)row.root.transform).anchoredPosition = new Vector2(first.x, -(first.y + i * step));
                row.Draw(asks[i], style);
            }
            var t = (RectTransform)rowTemplate.transform;
            float need = asks.Count == 0 ? 0f : 2f * first.y + (asks.Count - 1) * step + t.sizeDelta.y;
            float view = scroll != null ? ((RectTransform)scroll.viewport.transform).rect.height : need;
            content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(need, view));
            if (scroll != null && scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(need > view + 1f);
            if (newTab && scroll != null) scroll.verticalNormalizedPosition = 1f;
            if (emptyText != null) { emptyText.text = empty ?? ""; emptyText.gameObject.SetActive(asks.Count == 0 && !string.IsNullOrEmpty(empty)); }
        }

        void Press(int i)
        {
            if (i < asks.Count) asks[i].onPress?.Invoke();
        }

        // ---------------- test hooks ----------------
        /// <summary>Presses the button of the row <paramref name="id"/> (false when it has none).</summary>
        public bool QaPress(string id)
        {
            int i = asks.FindIndex(a => a.id == id);
            if (i < 0 || !Visible || i >= rows.Count || !rows[i].button.gameObject.activeSelf || !rows[i].button.interactable) return false;
            rows[i].button.onClick.Invoke(); return true;
        }

        public AskRow RowOf(string id) { int i = asks.FindIndex(a => a.id == id); return i >= 0 && i < rows.Count ? rows[i] : null; }
    }
}
