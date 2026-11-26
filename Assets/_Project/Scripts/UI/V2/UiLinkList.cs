using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// D-040: a list of a Figma screen (tool slots, notebook rows, quest cards, storage items) that shows the items of a list on the
    /// game's own screen: one copy of <see cref="template"/> per item there, its <see cref="UiLink"/>s pointing into that item.
    /// The design's other sample items (<see cref="samples"/>) are only for the editor. Optional pages (<see cref="pageSize"/> with
    /// the ◀ ▶ buttons) and a filter set by <see cref="UiTabFilter"/> tabs.
    /// </summary>
    public class UiLinkList : MonoBehaviour
    {
        [Tooltip("게임 화면의 예전 목록 (이름 경로). 바꾸지 마세요.")] public string path;
        [Tooltip("예전 목록에서 이름이 이걸로 시작하는 것이 항목 (',' 로 여러 개)")] public string prefix;
        [Tooltip("항목 하나의 모양 (게임에서는 항목 수만큼 복사돼요)")] public RectTransform template;
        [Tooltip("미리보기용 견본 (게임에서는 지워져요)")] public Transform[] samples = new Transform[0];
        [Tooltip("다음 항목이 놓이는 간격 (레이아웃이 없을 때)")] public Vector2 step;
        [Tooltip("한 쪽에 보일 항목 수 (0이면 전부)")] public int pageSize;
        public Button prev, next;
        [Tooltip("항목 수에 맞춰 스크롤 내용 높이를 늘려요")] public bool growContent;
        [Tooltip("한 쪽이 다 차지 않으면 항목들을 가운데로 모아요")] public bool centre;
        [Tooltip("격자(Grid Layout Group) 안의 목록: 항목이 많을 때 이 폭 안에 여러 칸으로 놓아요 (0이면 격자 칸을 건드리지 않아요)")] public float gridWidth;
        [Tooltip("칸이 좁아지면 숨길 장식 이름 (',' 로 여러 개, 예: Heart)")] public string narrowHide;

        [NonSerialized] public string filter = "";
        [NonSerialized] public int page;
        /// <summary>Shows no items (a folded panel, <see cref="UiFold"/>).</summary>
        [NonSerialized] public bool hidden;

        /// <summary>Whether <paramref name="t"/> is this list's template or one of its copies.</summary>
        public bool Owns(Transform t) => t == template || (t is RectTransform r && clones.Contains(r));

        Transform baseT; bool ready;
        Vector2 origin, cell0, gap0; float contentH0;
        readonly List<RectTransform> clones = new List<RectTransform>();
        readonly List<UiLink[]> cloneLinks = new List<UiLink[]>();
        readonly List<Transform> items = new List<Transform>();

        public void Init(Transform b)
        {
            baseT = b;
            if (ready || template == null) return;
            ready = true;
            foreach (var s in samples) if (s != null && s != template) { s.gameObject.SetActive(false); if (Application.isPlaying) Destroy(s.gameObject); else DestroyImmediate(s.gameObject); }
            origin = template.anchoredPosition;
            bool isGrid = template.parent.TryGetComponent<GridLayoutGroup>(out var g0);
            cell0 = isGrid ? g0.cellSize : template.rect.size; gap0 = isGrid ? g0.spacing : Vector2.zero;
            template.gameObject.SetActive(false);
            if (template.parent is RectTransform content) contentH0 = content.sizeDelta.y;
            if (prev != null) prev.onClick.AddListener(() => page--);
            if (next != null) next.onClick.AddListener(() => page++);
        }

        bool Match(string name)
        {
            if (string.IsNullOrEmpty(prefix)) return true;
            foreach (var p in prefix.Split(',')) if (p.Length > 0 && name.StartsWith(p, StringComparison.Ordinal)) return true;
            return false;
        }

        bool Pass(string name)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            foreach (var f in filter.Split(',')) if (f.Length > 0 && name.Contains(f)) return true;
            return false;
        }

        public void Sync()
        {
            if (!ready) return;
            if (hidden) { foreach (var c in clones) if (c.gameObject.activeSelf) c.gameObject.SetActive(false); return; }
            items.Clear();
            var src = UiPath.Find(baseT, path);
            if (src != null && src.gameObject.activeInHierarchy)   // a closed window has no items to show
                for (int i = 0; i < src.childCount; i++) { var c = src.GetChild(i); if (c.gameObject.activeSelf && Match(c.name) && Pass(c.name)) items.Add(c); }
            int from = 0, count = items.Count;
            if (pageSize > 0)
            {
                int pages = Mathf.Max(1, (items.Count + pageSize - 1) / pageSize);
                page = Mathf.Clamp(page, 0, pages - 1); from = page * pageSize; count = Mathf.Min(pageSize, items.Count - from);
                bool paged = items.Count > pageSize;
                if (prev != null) { if (prev.gameObject.activeSelf != paged) prev.gameObject.SetActive(paged); prev.interactable = page > 0; }
                if (next != null) { if (next.gameObject.activeSelf != paged) next.gameObject.SetActive(paged); next.interactable = page < pages - 1; }
            }
            while (clones.Count < count)
            {
                var c = Instantiate(template, template.parent, false); c.name = template.name + "_" + clones.Count;
                clones.Add(c); cloneLinks.Add(c.GetComponentsInChildren<UiLink>(true));
            }
            bool layout = template.parent.GetComponent<LayoutGroup>() != null;
            // only lists that ask for it (gridWidth): a designed grid such as the storage cards keeps its own cells
            if (gridWidth > 0f && template.parent.TryGetComponent<GridLayoutGroup>(out var grid)) FitGrid(grid, from, count);
            // a short page (the last tools) sits in the middle of the row instead of at its start
            var shift = centre && pageSize > 0 && count < pageSize ? step * ((pageSize - count) * 0.5f) : Vector2.zero;
            for (int i = 0; i < clones.Count; i++)
            {
                var c = clones[i]; bool on = i < count;
                if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
                if (!on) continue;
                if (!layout) c.anchoredPosition = origin + shift + step * i;
                var item = items[from + i];
                foreach (var l in cloneLinks[i]) { l.Bind(item); l.Sync(); }
            }
            if (growContent && !layout && template.parent is RectTransform content)
            {
                float need = -origin.y + Mathf.Abs(step.y) * count + 40f;
                content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(contentH0, need));
            }
        }

        /// <summary>
        /// A list in a grid: up to four items stay one column of full-size buttons; more (a month or a day to pick) get as many
        /// columns as the game's own grid uses (its first row), narrowed to fit <see cref="gridWidth"/>. Decorations named in
        /// <see cref="narrowHide"/> leave narrow cells.
        /// </summary>
        void FitGrid(GridLayoutGroup grid, int from, int count)
        {
            int cols = 1;
            if (count > 4)
            {
                float top = float.MinValue; int first = 0;
                for (int i = 0; i < count; i++) if (items[from + i] is RectTransform r) top = Mathf.Max(top, r.anchoredPosition.y);
                for (int i = 0; i < count; i++) if (items[from + i] is RectTransform r && Mathf.Abs(r.anchoredPosition.y - top) < 1f) first++;
                cols = Mathf.Clamp(first, 2, 8);
            }
            var cell = cell0; var gap = gap0;
            if (cols > 1)
            {
                if (cols >= 6) gap = new Vector2(Mathf.Min(gap0.x, 8f), Mathf.Min(gap0.y, 12f));   // many small cells: tighter
                float room = gridWidth > 0f ? gridWidth : cell0.x * cols;
                cell = new Vector2(Mathf.Min(cell0.x, (room - (cols - 1) * gap.x) / cols), Mathf.Min(cell0.y, cols >= 6 ? 56f : 64f));
            }
            if ((grid.spacing - gap).sqrMagnitude > 0.01f) grid.spacing = gap;
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            if (grid.constraintCount != cols) grid.constraintCount = cols;
            if ((grid.cellSize - cell).sqrMagnitude > 0.01f) grid.cellSize = cell;
            if (string.IsNullOrEmpty(narrowHide)) return;
            bool narrow = cell.x < cell0.x * 0.7f;
            foreach (var c in clones)
            {
                if (!c.gameObject.activeSelf) continue;
                foreach (var n in narrowHide.Split(','))
                {
                    var d = n.Length > 0 ? c.Find(n.Trim()) : null;
                    if (d != null && d.gameObject.activeSelf == narrow) d.gameObject.SetActive(!narrow);
                }
            }
        }
    }
}
