using System;
using System.Collections.Generic;
using Mongle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// D-040: a Figma screen (Assets/_UI수정) shown in the game in front of the screen the code builds. The code's screen keeps
    /// all the game logic; the parts this screen replaces (<see cref="hideOld"/>) are made invisible and unclickable there, and the
    /// <see cref="UiLink"/>s here press its buttons and show its words. Parts Figma has no design for (the shop, dress-up, claw
    /// machine …) stay as the code draws them. The gear buttons open the real settings window (<see cref="OpenSettings"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public class UiFront : MonoBehaviour
    {
        [Tooltip("이 화면이 덮는 예전 화면의 부분 (게임에서 안 보이게 돼요). 이름 경로 — 바꾸지 마세요.")]
        public string[] hideOld = new string[0];
        [Tooltip("이 예전 창이 열려 있으면 이 화면을 맨 위로 올려요 (창 화면)")]
        public string onTopWhen;
        [Tooltip("창이 열려 있는 동안 뒤 화면을 어둡게 덮어요 (뒤의 버튼은 눌리지 않아요)")]
        public bool dimBehind;
        [Tooltip("덮는 색")]
        public Color dimColor = new Color(0.16f, 0.1f, 0.07f, 0.42f);
        GameObject dim;

        [NonSerialized] public Transform old;
        UiLink[] links = new UiLink[0]; UiLinkList[] lists = new UiLinkList[0]; UiTabFilter[] tabs = new UiTabFilter[0];
        readonly Dictionary<string, Transform> hidden = new Dictionary<string, Transform>();
        float nextFind; Transform topWhen;

        /// <summary>Which Figma screens go over which of the code's screens (their UiSkin names).</summary>
        static readonly Dictionary<string, string[]> Fronts = new Dictionary<string, string[]>
        {
            ["RepairScreen"] = new[] { "RepairHud", "Intake" },
            ["VillageScreen"] = new[] { "VillageHud", "Quest", "Storage", "RideInvite", "Intake" },
            ["PhotoScreen"] = new[] { "Photo" },
        };

        /// <summary>Tests / tools: no Figma screens.</summary>
        public static bool Off;

        /// <summary>Puts the Figma screens of the code's screen <paramref name="skin"/> over it (in the game only).</summary>
        public static void AttachFor(Transform oldCanvas, string skin)
        {
            if (!Application.isPlaying || Off || oldCanvas == null || skin == null || !Fronts.TryGetValue(skin, out var names)) return;
            // the last one ends up first among the canvas' children: the HUD below its dialogs
            for (int i = names.Length - 1; i >= 0; i--) Attach(oldCanvas, names[i]);
        }

        /// <summary>Shows the Figma screen <paramref name="name"/> in front of the code's screen under <paramref name="oldCanvas"/>.</summary>
        public static UiFront Attach(Transform oldCanvas, string name)
        {
            if (Off || oldCanvas == null) return null;
            var existing = oldCanvas.Find("UI2_" + name);
            if (existing != null) return existing.GetComponent<UiFront>();
            var prefab = UiSkin.FindFront(name); if (prefab == null) return null;
            if (oldCanvas.GetComponent<CanvasScaler>() != null && oldCanvas.GetComponent<AdaptiveScaler>() == null) oldCanvas.gameObject.AddComponent<AdaptiveScaler>();
            var go = Instantiate(prefab, oldCanvas, false); go.name = "UI2_" + name;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.localScale = Vector3.one;
            go.transform.SetAsFirstSibling();   // the code's own windows (shop, dress-up …) stay on top
            var f = go.GetComponent<UiFront>(); if (f == null) f = go.AddComponent<UiFront>();
            f.Init(oldCanvas);
            return f;
        }

        public void Init(Transform oldRoot)
        {
            old = oldRoot;
            lists = GetComponentsInChildren<UiLinkList>(true);
            tabs = GetComponentsInChildren<UiTabFilter>(true);
            var all = GetComponentsInChildren<UiLink>(true); var mine = new List<UiLink>();
            foreach (var l in all)
            {
                bool inTemplate = false;
                foreach (var li in lists) if (li.template != null && l.transform.IsChildOf(li.template)) { inTemplate = true; break; }
                if (!inTemplate) mine.Add(l);
            }
            links = mine.ToArray();
            foreach (var li in lists) li.Init(old);
            foreach (var l in links) l.Bind(old);
            FindRows();
            Sync();
        }

        void LateUpdate() { Sync(); }

        public void Sync()
        {
            if (old == null) return;
            HideOld();
            if (!string.IsNullOrEmpty(onTopWhen))
            {
                if (topWhen == null) topWhen = UiPath.Find(old, onTopWhen);
                bool open = topWhen != null && topWhen.gameObject.activeInHierarchy;
                // a Figma window that is open sits above the code's own HUD bits (quest tracker, badges) like the window it replaces did
                if (open && transform.GetSiblingIndex() != transform.parent.childCount - 1) transform.SetAsLastSibling();
                Dim(open);
            }
            foreach (var li in lists) li.Sync();
            foreach (var tab in tabs) tab.Sync();
            foreach (var l in links) l.Sync();
            CloseUpRows();
        }

        // ---------------- rows of buttons ----------------

        /// <summary>Buttons side by side (one line, one height) of which the game may hide some.</summary>
        sealed class Row { public RectTransform[] items; public float[] centre; public float mid, gap; public int mask = -1; }
        readonly List<Row> rows = new List<Row>();

        /// <summary>Finds the rows: siblings on one line with the same height and anchors, at least one of them shown only when the
        /// game shows its twin (a dialog's "Sorry" next to its "OK"). Their design places are kept for when all are shown.</summary>
        void FindRows()
        {
            rows.Clear();
            var parents = new HashSet<Transform>();
            foreach (var l in links) if (l != null && Hides(l) && l.transform.parent != null) parents.Add(l.transform.parent);
            foreach (var p in parents)
            {
                if (p.GetComponent<LayoutGroup>() != null) continue;   // a row / column group closes up on its own
                var kids = new List<RectTransform>();
                for (int i = 0; i < p.childCount; i++) if (p.GetChild(i) is RectTransform k) kids.Add(k);
                var used = new HashSet<RectTransform>();
                foreach (var a in kids)
                {
                    if (used.Contains(a)) continue;
                    var row = new List<RectTransform> { a };
                    foreach (var b in kids) if (b != a && !used.Contains(b) && SameLine(a, b)) row.Add(b);
                    if (row.Count < 2) continue;
                    bool hides = false;
                    foreach (var x in row) if (x.TryGetComponent<UiLink>(out var xl) && Hides(xl)) hides = true;
                    if (!hides) continue;
                    row.Sort((x, y) => Centre(x).CompareTo(Centre(y)));
                    bool apart = true;
                    for (int i = 1; i < row.Count; i++) if (Centre(row[i]) - row[i].rect.width * 0.5f < Centre(row[i - 1]) + row[i - 1].rect.width * 0.5f - 1f) apart = false;
                    if (!apart) continue;   // overlapping pieces are a drawing, not a row
                    foreach (var x in row) used.Add(x);
                    var r = new Row { items = row.ToArray(), centre = new float[row.Count] };
                    float gaps = 0f;
                    for (int i = 0; i < row.Count; i++)
                    {
                        r.centre[i] = Centre(row[i]);
                        if (i > 0) gaps += (r.centre[i] - row[i].rect.width * 0.5f) - (r.centre[i - 1] + row[i - 1].rect.width * 0.5f);
                    }
                    var last = row[row.Count - 1];
                    r.mid = ((r.centre[0] - row[0].rect.width * 0.5f) + (r.centre[row.Count - 1] + last.rect.width * 0.5f)) * 0.5f;
                    r.gap = gaps / (row.Count - 1);
                    rows.Add(r);
                }
            }
        }

        static bool Hides(UiLink l) => l.active || !string.IsNullOrEmpty(l.showIf);

        static bool SameLine(RectTransform a, RectTransform b) =>
            a.anchorMin == b.anchorMin && a.anchorMax == b.anchorMax && Mathf.Approximately(a.anchorMin.x, a.anchorMax.x)
            && Mathf.Abs(a.pivot.y - b.pivot.y) < 0.01f && Mathf.Abs(a.anchoredPosition.y - b.anchoredPosition.y) < 2f
            && Mathf.Abs(a.rect.height - b.rect.height) < 2f;

        /// <summary>The middle of a piece, measured from its anchor.</summary>
        static float Centre(RectTransform t) => t.anchoredPosition.x + (0.5f - t.pivot.x) * t.rect.width;

        /// <summary>When the game hides some buttons of a row, the shown ones close up in the middle of where the row was (a dialog
        /// that only offers "OK" has it centred); when all are shown again they go back to their design places.</summary>
        void CloseUpRows()
        {
            foreach (var r in rows)
            {
                int mask = 0, n = 0; float total = 0f;
                for (int i = 0; i < r.items.Length; i++)
                    if (r.items[i] != null && r.items[i].gameObject.activeSelf) { mask |= 1 << i; n++; total += r.items[i].rect.width; }
                if (mask == r.mask) continue;
                r.mask = mask;
                bool all = n == r.items.Length;
                float x = r.mid - (total + r.gap * Mathf.Max(0, n - 1)) * 0.5f;
                for (int i = 0; i < r.items.Length; i++)
                {
                    var t = r.items[i]; if (t == null) continue;
                    float c;
                    if (all || !t.gameObject.activeSelf) c = r.centre[i];
                    else { c = x + t.rect.width * 0.5f; x += t.rect.width + r.gap; }
                    var p = t.anchoredPosition; p.x = c - (0.5f - t.pivot.x) * t.rect.width; t.anchoredPosition = p;
                }
            }
        }

        /// <summary>A window (quests, storage) covers the screen behind it while open: the HUD shows dimmed through and its buttons
        /// do not take clicks, so nothing of it peeks out between the window's tabs as if it belonged to the window.</summary>
        static readonly HashSet<UiFront> openWindows = new HashSet<UiFront>();

        /// <summary>Whether a Figma window (quests, storage) is open now (notices move out of its way, GameRoot).</summary>
        public static bool AnyWindowOpen
        {
            get { openWindows.RemoveWhere(f => f == null || !f.isActiveAndEnabled); return openWindows.Count > 0; }
        }

        void OnDisable() { openWindows.Remove(this); }

        void Dim(bool open)
        {
            if (!dimBehind) return;   // only the big windows (a dialog at the bottom keeps notices where they are)
            if (open) openWindows.Add(this); else openWindows.Remove(this);
            if (dim == null)
            {
                if (!open) return;
                dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
                dim.transform.SetParent(transform, false); dim.transform.SetAsFirstSibling();
                var r = (RectTransform)dim.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                var im = dim.GetComponent<Image>(); im.color = dimColor; im.raycastTarget = true;
            }
            if (dim.activeSelf != open) dim.SetActive(open);
        }

        void HideOld()
        {
            bool find = Time.unscaledTime >= nextFind;
            foreach (var p in hideOld)
            {
                if (string.IsNullOrEmpty(p)) continue;
                if (!hidden.TryGetValue(p, out var t) || t == null)
                {
                    if (!find) continue;
                    t = UiPath.Find(old, p); hidden[p] = t;
                    if (t == null) continue;
                }
                if (!t.TryGetComponent<CanvasGroup>(out var cg)) cg = t.gameObject.AddComponent<CanvasGroup>();
                if (cg.alpha != 0f) cg.alpha = 0f;
                cg.blocksRaycasts = false; cg.interactable = false;
            }
            if (find) nextFind = Time.unscaledTime + 0.25f;
        }

        // ---------------- settings ----------------

        static GameObject settingsWindow;

        /// <summary>The settings window (Figma 58:1007): on top of everything, kept across scenes; its ◀ closes it.</summary>
        public static void OpenSettings()
        {
            if (settingsWindow == null)
            {
                var prefab = UiSkin.FindFront("Settings");
                if (prefab == null) { Mongle.Core.GameRoot.Instance?.Say("설정은 곧 열려요"); return; }
                var go = new GameObject("UI2_SettingsWindow", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 300;
                go.AddComponent<AdaptiveScaler>();
                go.AddComponent<HideBehind>();   // the HUD under it goes away while it's open (no title-on-pill show-through)
                DontDestroyOnLoad(go);
                var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
                dim.transform.SetParent(go.transform, false);
                var drt = (RectTransform)dim.transform; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
                dim.GetComponent<Image>().color = new Color(0.16f, 0.1f, 0.07f, 0.45f);   // the game behind, dimmed; clicks stop here
                var s = Instantiate(prefab, go.transform, false); s.name = "Settings";
                var srt = (RectTransform)s.transform; srt.anchorMin = Vector2.zero; srt.anchorMax = Vector2.one; srt.offsetMin = srt.offsetMax = Vector2.zero;
                var screen = s.GetComponent<SettingsScreen>();
                if (screen != null) screen.Closed = () => { if (settingsWindow != null) settingsWindow.SetActive(false); };
                settingsWindow = go;
            }
            UiKit.EnsureEventSystem();
            settingsWindow.SetActive(true);
        }

        public static bool SettingsOpen => settingsWindow != null && settingsWindow.activeSelf;
    }
}
