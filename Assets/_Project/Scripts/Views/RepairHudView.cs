using System;
using System.Collections;
using System.Collections.Generic;
using Mongle.Data;
using Mongle.Toy;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The repair shop's screen (Figma 11-26 인게임2 88:1704 / 인게임3 88:1767): back (◀, cancels the tool in hand), the hint
    /// banner, the gear, the 진료 수첩 (one row per damage: "?" unknown, "!" found, ✔ mended; the blue row is the one being worked
    /// on), zoom ＋/－, and the tool bar at the bottom — four tools a page, ◀ ▶ for the rest, the hovered tool outlined with its
    /// hint above it. A scene object of RepairShop (Assets/_UI수정/2_수선점/RepairHud.prefab): the game talks to it, nothing is
    /// laid over anything.
    /// </summary>
    public class RepairHudView : MonoBehaviour
    {
        [Header("위")]
        public Button backButton;
        public Button gearButton;
        public RectTransform banner;
        public TMP_Text bannerText;
        [Tooltip("배너의 가장 작은 폭")] public float bannerMinWidth = 630.46f;
        [Tooltip("배너 글자 양옆 여백")] public float bannerPad = 165f;
        [Header("진료 수첩")]
        public RectTransform notebook;
        [Tooltip("줄 하나 (복사해서 손상 수만큼 써요)")] public RectTransform rowTemplate;
        [Tooltip("지금 고치는 줄")] public Color rowOn = new Color(0.627f, 0.863f, 0.973f, 1f);
        [Tooltip("다른 줄")] public Color rowOff = new Color(0.98f, 0.941f, 0.875f, 1f);
        [Header("확대 / 축소")]
        public Button zoomIn, zoomOut;
        [Header("도구 막대")]
        public RectTransform toolBar;
        [Tooltip("도구 칸 (왼쪽부터)")] public Button[] slots = new Button[0];
        [Tooltip("도구 그림 밝기 (그림에 곱해요; 흰 칸 위에서 너무 밝아 잘 안 보여서 낮춰요)")] public Color toolTint = new Color(0.8f, 0.8f, 0.8f, 1f);
        public Button prevPage, nextPage;
        [Tooltip("도구 설명 말풍선")] public RectTransform tip;
        public TMP_Text tipText;
        [Tooltip("말풍선 꼬리 끝 ~ 말풍선 왼쪽 끝")] public float tipTailX = 179.5f;
        [Tooltip("말풍선 아래 ~ 칸 위")] public float tipGap = 19.8f;

        public Action<float> OnZoom;
        public Action OnBack;

        readonly List<RectTransform> rows = new List<RectTransform>();
        ToolDefinition[] tools = new ToolDefinition[0];
        Action<string> onPick;
        int page, hovered = -1;
        string pulseId;

        void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke());
            if (gearButton != null) gearButton.onClick.AddListener(UiFront.OpenSettings);
            if (zoomIn != null) zoomIn.onClick.AddListener(() => OnZoom?.Invoke(0.85f));
            if (zoomOut != null) zoomOut.onClick.AddListener(() => OnZoom?.Invoke(1.18f));
            if (prevPage != null) prevPage.onClick.AddListener(() => { page--; ShowPage(); });
            if (nextPage != null) nextPage.onClick.AddListener(() => { page++; ShowPage(); });
            for (int i = 0; i < slots.Length; i++)
            {
                int k = i;
                slots[i].onClick.AddListener(() => { var t = ToolAt(k); if (t != null) onPick?.Invoke(t.id); });
                var trigger = slots[i].gameObject.GetComponent<EventTrigger>() ?? slots[i].gameObject.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(_ => Hover(k));
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; exit.callback.AddListener(_ => { if (hovered == k) Hover(-1); });
                trigger.triggers.Add(enter); trigger.triggers.Add(exit);
            }
            if (rowTemplate != null) rowTemplate.gameObject.SetActive(false);
        }

        // ---------------- top ----------------

        /// <summary>The hint plate at the top; empty hides it. It hugs its words (never narrower than the design).</summary>
        public void Banner(string text)
        {
            if (banner == null) return;
            if (string.IsNullOrEmpty(text)) { banner.gameObject.SetActive(false); return; }
            bannerText.text = text;
            banner.sizeDelta = new Vector2(Mathf.Max(bannerMinWidth, bannerText.GetPreferredValues(text, 99999f, 0f).x + bannerPad * 2f), banner.sizeDelta.y);
            banner.gameObject.SetActive(true);
        }
        public string BannerText => bannerText != null && banner.gameObject.activeSelf ? bannerText.text : "";
        public bool BannerShown => banner != null && banner.gameObject.activeSelf;

        public void ShowBack(bool on) { if (backButton != null) backButton.gameObject.SetActive(on); }
        public void SetInspectControls(bool on) { if (zoomIn != null) zoomIn.gameObject.SetActive(on); if (zoomOut != null) zoomOut.gameObject.SetActive(on); }

        // ---------------- notebook ----------------

        public void ShowNotebook(bool on) { if (notebook != null) notebook.gameObject.SetActive(on); }

        /// <summary>One row per damage: "?" until found, then "!" and its name, a green check when mended; the row being worked on is blue.</summary>
        public void RefreshNotebook(ToyRuntime t, ContentCatalog catalog, string highlightId = null)
        {
            if (rowTemplate == null || t == null) return;
            while (rows.Count < t.Damages.Count)
            {
                var r = Instantiate(rowTemplate, rowTemplate.parent, false); r.name = "Row" + rows.Count; rows.Add(r);
            }
            for (int i = 0; i < rows.Count; i++)
            {
                bool on = i < t.Damages.Count; rows[i].gameObject.SetActive(on);
                if (!on) continue;
                var d = t.Damages[i]; var def = catalog != null ? catalog.Damage(d.id) : null;
                bool known = d.status != DamageStatus.Hidden, done = d.status == DamageStatus.Completed;
                var row = rows[i];
                Find(row, "Check").gameObject.SetActive(done);
                var mark = Find(row, "Mark"); mark.gameObject.SetActive(!done);
                var markText = mark.GetComponentInChildren<TMP_Text>(true); if (markText != null) markText.text = known ? "!" : "?";
                var name = Find(row, "Name").GetComponent<TMP_Text>();
                name.text = known && def != null ? catalog.DamageName(t.KindId, def) : "";
                var plate = row.GetComponent<Graphic>(); if (plate != null) plate.color = highlightId == d.id ? rowOn : rowOff;   // the one looked at / just mended
            }
            LayoutRebuilder.MarkLayoutForRebuild(notebook);
        }

        static Transform Find(Transform row, string name)
        {
            foreach (var c in row.GetComponentsInChildren<Transform>(true)) if (c.name == name) return c;
            return row;
        }

        // ---------------- tool bar ----------------

        public void ShowShelf(bool on) { if (toolBar != null) toolBar.gameObject.SetActive(on); if (!on) Hover(-1); }

        /// <summary>The tools, four a page (◀ ▶ when there are more); a click picks the tool.</summary>
        public void BuildToolShelf(ToolDefinition[] list, Action<string> pick)
        {
            tools = list ?? new ToolDefinition[0]; onPick = pick; page = 0;
            ShowPage();
        }

        int Pages => Mathf.Max(1, (tools.Length + slots.Length - 1) / Mathf.Max(1, slots.Length));
        ToolDefinition ToolAt(int slot) { int i = page * slots.Length + slot; return i >= 0 && i < tools.Length ? tools[i] : null; }

        void ShowPage()
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            for (int i = 0; i < slots.Length; i++)
            {
                var t = ToolAt(i);
                var icon = Find(slots[i].transform, "Icon").GetComponent<Image>();
                var sp = t != null ? Resources.Load<Sprite>("UI/Tools/" + t.id) : null;
                icon.sprite = sp; icon.enabled = sp != null; icon.color = toolTint;
                var label = Find(slots[i].transform, "Name").GetComponent<TMP_Text>();
                label.text = t != null && sp == null ? t.displayName : "";   // a tool without a picture shows its name
                slots[i].interactable = t != null;
                slots[i].name = t != null ? "Btn_" + t.displayName : "Slot" + i;   // QA finds tools by name
            }
            if (prevPage != null) { prevPage.gameObject.SetActive(Pages > 1); prevPage.interactable = page > 0; }
            if (nextPage != null) { nextPage.gameObject.SetActive(Pages > 1); nextPage.interactable = page < Pages - 1; }
            Hover(-1);
        }

        /// <summary>The tool under the pointer (-1: none): outlined, its hint above it.</summary>
        public void Hover(int slot)
        {
            hovered = slot;
            var t = slot >= 0 ? ToolAt(slot) : null;
            if (tip != null)
            {
                bool show = t != null && !string.IsNullOrEmpty(t.shortHint);
                tip.gameObject.SetActive(show);
                if (show)
                {
                    tipText.text = t.shortHint;
                    var trt = tipText.rectTransform;
                    tip.sizeDelta = new Vector2(Mathf.Max(234.46f, trt.anchoredPosition.x + tipText.GetPreferredValues(t.shortHint, 99999f, 0f).x + 34f), tip.sizeDelta.y);
                    var s = (RectTransform)slots[slot].transform;
                    // the tail's tip over the middle of the tool: placed from the bar's own origin (its pivot)
                    var parent = (RectTransform)tip.parent;
                    tip.anchorMin = tip.anchorMax = parent.pivot; tip.pivot = Vector2.zero;
                    var top = parent.InverseTransformPoint(s.TransformPoint(new Vector3(s.rect.center.x, s.rect.yMax, 0f)));
                    tip.anchoredPosition = new Vector2(top.x - tipTailX, top.y + tipGap);
                }
            }
            UpdateOutlines();
        }

        /// <summary>The tool the game wants next glows after a while (outline + a soft beat).</summary>
        public void PulseTool(string id, bool on)
        {
            string was = pulseId; pulseId = on ? id : null;
            if (was == pulseId) return;
            if (pulseId == null) foreach (var s in slots) s.transform.localScale = Vector3.one;
            UpdateOutlines();
        }

        /// <summary>A wrong tool: its tile shakes.</summary>
        public void ShakeTool(string id)
        {
            for (int i = 0; i < slots.Length; i++) { var t = ToolAt(i); if (t != null && t.id == id) StartCoroutine(Shake(slots[i].transform)); }
        }

        IEnumerator Shake(Transform t)
        {
            float e = 0; var p = t.localPosition;
            while (e < 0.3f) { e += Time.unscaledDeltaTime; t.localPosition = p + new Vector3(Mathf.Sin(e * 60) * 8f * (1 - e / 0.3f), 0, 0); yield return null; }
            t.localPosition = p;
        }

        void UpdateOutlines()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var t = ToolAt(i);
                bool on = t != null && (i == hovered || (pulseId != null && t.id == pulseId));
                var o = Find(slots[i].transform, "Outline"); if (o != slots[i].transform && o.gameObject.activeSelf != on) o.gameObject.SetActive(on);
            }
        }

        void Update()
        {
            if (pulseId == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                var t = ToolAt(i);
                slots[i].transform.localScale = t != null && t.id == pulseId ? Vector3.one * (1.04f + 0.03f * Mathf.Sin(Time.unscaledTime * 7f)) : Vector3.one;
            }
        }

        /// <summary>Everything off (the stamp / dress-up screens take over).</summary>
        public void HideAll()
        {
            ShowNotebook(false); ShowShelf(false); ShowBack(false); SetInspectControls(false); Banner(null);
        }
    }
}
