using System;
using System.Collections.Generic;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>All screen UI of the repair shop (notebook, banner, tool shelf, dialogs, stamp, customization).</summary>
    public class ShopUI : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        Text banner; Image bannerPanel;
        static readonly Color FigmaBrown = new Color32(0x7a, 0x59, 0x4d, 255), FigmaIcon = new Color32(0x81, 0x62, 0x53, 255);
        RectTransform notebook; readonly List<Image> rowIcons = new List<Image>(), rowChecks = new List<Image>(); readonly List<Text> rowIconTexts = new List<Text>(); readonly List<Text> rowNames = new List<Text>();
        readonly List<Image> rowPanels = new List<Image>();
        RectTransform shelf; readonly Dictionary<string, Image> toolButtons = new Dictionary<string, Image>();
        Button back, zoomIn, zoomOut;
        GameObject dialog; Text dialogTitle, dialogBody; Button dialogButton, dialogButton2;
        RectTransform stampPanel; Button stampButton; Image stampImage;
        RectTransform custom; readonly Dictionary<string, Image> accButtons = new Dictionary<string, Image>();
        Button confirmButton;
        public Action<float> OnZoom;
        public Action OnBack;

        public void Build()
        {
            Canvas = UiKit.CreateCanvas("ShopUI", 10, transform);
            // banner (Figma D-019: stitched cream plate, 1121x119 at the top, a little right of centre)
            bannerPanel = UiKit.SpriteImage(Canvas.transform, "Banner", "banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22.5f), new Vector2(1121f, 119f));   // centred (D-024)
            if (bannerPanel.sprite == null) { bannerPanel.sprite = UiKit.Round; bannerPanel.type = Image.Type.Sliced; bannerPanel.color = UiKit.Paper; }
            banner = UiKit.TitleLabel(bannerPanel.transform, "", 49, FigmaBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 100f));
            // the plate hugs its text (D-023): short lines get a short plate, long ones widen it up to 1440 (clear of the gear on the right)
            UiKit.Slice(bannerPanel); UiKit.AutoFit(bannerPanel, banner, 420f, 140f, 1440f);
            bannerPanel.gameObject.SetActive(false);
            BuildNotebook();
            BuildShelf();
            // zoom buttons: right edge, middle (Figma centres at x -79.7 / y +56.4 and -78.1 / -62.9 from the screen middle)
            zoomIn = UiKit.SpriteButton(Canvas.transform, "Btn_＋", "btn_plus", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-79.7f, 56.4f), new Vector2(106.5f, 106.5f), () => OnZoom?.Invoke(0.85f));
            zoomOut = UiKit.SpriteButton(Canvas.transform, "Btn_－", "btn_minus", new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-78.1f, -62.9f), new Vector2(106.5f, 106.5f), () => OnZoom?.Invoke(1.18f));
            UiKit.TitleLabel(zoomOut.transform, "-", 60, new Color32(0x7d, 0x5d, 0x51, 255), new Vector2(0.5f, 0.5f), new Vector2(0, 3f), new Vector2(100f, 100f));
            // back button (top left, 270x91)
            back = UiKit.SpriteButton(Canvas.transform, "Btn_돌아가기", "btn_back", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36.2f, -35.6f), new Vector2(270.5f, 91.5f), () => OnBack?.Invoke());
            UiKit.TitleLabel(back.transform, "돌아가기", 40, FigmaBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 80f));
            back.gameObject.SetActive(false);
            // settings (D-023): the Figma gear at the top right; the window itself is not made yet
            settings = UiKit.FigButton(Canvas.transform, "hud_settings", () => Mongle.Core.GameRoot.Instance?.Say("설정은 곧 열려요"), UiKit.Edge.End, UiKit.Edge.Start, 0f, 0f, "Btn_설정");
            UiKit.HiddenLabel(settings, "설정");
            BuildDialog();
            BuildStamp();
            SetInspectControls(false);
        }

        public bool BannerShown => bannerPanel != null && bannerPanel.gameObject.activeSelf;

        /// <summary>The village layout (Figma): the zoom buttons sit at the left edge, away from the tiles on the right.</summary>
        Button settings;
        public void ShowSettingsButton(bool on) { if (settings != null) settings.gameObject.SetActive(on); }

        /// <summary>The village layout (Figma 아트보드 6): zoom at the left edge, the message plate where the status plate is (clear of the coin pill).</summary>
        public void VillageLayout()
        {
            ZoomToLeft();
            var r = FigmaLayout.Rects["hud_banner"]; var rt = bannerPanel.rectTransform;
            bannerPanel.sprite = UiKit.UiSprite("hud_banner"); UiKit.Slice(bannerPanel);
            rt.anchoredPosition = new Vector2(r.center.x - 960f, -r.y); rt.sizeDelta = new Vector2(r.width, r.height);
            banner.fontSize = 49;   // same as the 진료 수첩 title on screen (61 at 80 %, D-025)
            var f = bannerPanel.GetComponent<FitToText>(); f.minW = 420f; f.maxW = 1040f; f.Apply();
        }

        public void ZoomToLeft()
        {
            Place(zoomIn, 112.4f, 533.5f); Place(zoomOut, 114f, 652.9f);
        }
        static void Place(Button b, float artCx, float artCy)
        {
            var rt = (RectTransform)b.transform; rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.anchoredPosition = new Vector2(artCx, -(artCy - 540f));
        }

        public void SetInspectControls(bool on) { zoomIn.gameObject.SetActive(on); zoomOut.gameObject.SetActive(on); }
        public void ShowBack(bool on) { back.gameObject.SetActive(on); }

        // ---------------- banner ----------------
        public void Banner(string text)
        {
            if (string.IsNullOrEmpty(text)) { bannerPanel.gameObject.SetActive(false); return; }
            banner.text = text; bannerPanel.gameObject.SetActive(true);
        }
        public string BannerText => banner != null ? banner.text : "";

        // ---------------- notebook ----------------
        // Figma D-019: the 진료 수첩 sheet is 562x709 at the left (36 units from the edge, 262 from the top); rows sit 142 units apart,
        // each with an orange icon square and the damage name; the button pin and the star hang over its top left corner.
        static readonly float[] RowTops = { 118.1f, 259.1f, 402.9f, 545.7f };   // sprite tops, from the sheet's top edge
        void BuildNotebook()
        {
            // D-023: 80 % of the Figma size and higher up, so it clears the tool shelf at the bottom (and the back button above)
            var p = UiKit.SpriteImage(Canvas.transform, "Notebook", "panel", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(33.6f, -205f), new Vector2(562f, 709.5f));
            p.rectTransform.localScale = Vector3.one * 0.8f;
            if (p.sprite == null) { p.sprite = UiKit.Round; p.type = Image.Type.Sliced; p.color = UiKit.Paper; }
            notebook = p.rectTransform;
            UiKit.SpriteImage(notebook, "Pin", "pin", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.6f, 62.9f), new Vector2(107f, 107f));
            UiKit.SpriteImage(notebook, "Star", "star", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32.6f, -39.4f), new Vector2(41f, 59.7f));
            UiKit.TitleLabel(notebook, "진료 수첩", 61, FigmaBrown, new Vector2(0f, 1f), new Vector2(278.5f, -67.2f), new Vector2(400f, 90f));
            for (int i = 0; i < 4; i++)
            {
                var row = UiKit.SpriteImage(notebook, "Row" + i, "row_off", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40.1f, -RowTops[i]), new Vector2(484.5f, 127f));
                var icon = UiKit.SpriteImage(row.transform, "Icon", "icon", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28.1f, -23.7f), new Vector2(73.5f, 74f));
                var it = UiKit.TitleLabel(icon.transform, "?", 61, FigmaIcon, new Vector2(0.5f, 0.5f), new Vector2(0, 2f), new Vector2(73f, 90f));
                // a mended damage: a drawn check (the title font has no ✓) on the green square (D-026)
                var check = UiKit.SpriteImage(icon.transform, "Check", "nb_check", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(73.5f, 74f));
                check.gameObject.SetActive(false); rowChecks.Add(check);
                var nm = UiKit.TitleLabel(row.transform, "", 45, FigmaBrown, new Vector2(0f, 0.5f), new Vector2(134.7f, 0f), new Vector2(330f, 90f), TextAnchor.MiddleLeft, new Vector2(0f, 0.5f));
                nm.rectTransform.sizeDelta = new Vector2(330f, 64f); nm.verticalOverflow = VerticalWrapMode.Truncate;
                nm.horizontalOverflow = HorizontalWrapMode.Wrap; nm.resizeTextForBestFit = true; nm.resizeTextMinSize = 24; nm.resizeTextMaxSize = 45;   // long names shrink inside the row
                rowPanels.Add(row); rowIcons.Add(icon); rowIconTexts.Add(it); rowNames.Add(nm);
            }
            notebook.gameObject.SetActive(false);
        }

        public void ShowNotebook(bool on) { notebook.gameObject.SetActive(on); }

        public void RefreshNotebook(ToyRuntime t, ContentCatalog catalog, string highlightId = null)
        {
            for (int i = 0; i < 4; i++)
            {
                if (i >= t.Damages.Count) { rowPanels[i].gameObject.SetActive(false); continue; }
                var d = t.Damages[i]; var def = catalog.Damage(d.id);
                rowPanels[i].gameObject.SetActive(true);
                bool known = d.status != DamageStatus.Hidden;
                bool done = d.status == DamageStatus.Completed;
                rowIconTexts[i].text = done ? "" : known ? "!" : "?";
                rowChecks[i].gameObject.SetActive(done);
                var sq = UiKit.UiSprite(done ? "nb_icon_done" : "icon"); if (sq != null) rowIcons[i].sprite = sq;
                rowNames[i].text = known ? catalog.DamageName(t.KindId, def) : "";   // an unknown damage shows only its "?"
                // Figma D-020: the blue row is the damage being looked at / repaired; the others stay cream ("?" unknown, "!" found, green check mended)
                bool current = highlightId == d.id && !done;
                var on = UiKit.UiSprite(current ? "row_on" : "row_off"); if (on != null) rowPanels[i].sprite = on;
                rowPanels[i].transform.localScale = Vector3.one;
            }
        }

        // ---------------- tool shelf ----------------
        void BuildShelf()
        {
            var p = UiKit.Panel(Canvas.transform, "Shelf", new Color(1f, 0.96f, 0.88f, 0.9f), new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(980, 220), false);
            shelf = p.rectTransform; shelf.gameObject.SetActive(false);
        }

        public void BuildToolShelf(ToolDefinition[] tools, Action<string> onPick)
        {
            foreach (Transform c in shelf) if (c.name != "Stitch") Destroy(c.gameObject);
            toolButtons.Clear();
            // up to 4 tools keep the roomy layout; more (D-009: 7) shrink to fit a 4:3 screen (1440 reference units wide at match 0.5)
            float step = Mathf.Min(230f, 1290f / Mathf.Max(1, tools.Length)); float x0 = -(tools.Length - 1) * step / 2f;
            float k = step / 230f, w = step - 25f;
            shelf.sizeDelta = new Vector2(Mathf.Max(980f, tools.Length * step + 40f), 220f);
            for (int i = 0; i < tools.Length; i++)
            {
                var t = tools[i]; string id = t.id;
                var b = UiKit.MakeButton(shelf, "", t.uiColor, new Vector2(0.5f, 0.5f), new Vector2(x0 + i * step, 0), new Vector2(w, 190), () => onPick(id));
                UiKit.SetButtonText(b, ""); UiKit.NoStitch(b.GetComponent<Image>());   // plain tools on the stitched shelf (D-027)
                var name = UiKit.Label(b.transform, t.displayName, Mathf.RoundToInt(Mathf.Lerp(30f, 36f, (k - 0.75f) * 4f)), UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, 25), new Vector2(w - 5f, 60));
                var hint = UiKit.Label(b.transform, t.shortHint, k < 0.99f ? 22 : 24, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, -35), new Vector2(w - 12f, 70), TextAnchor.UpperCenter, FontStyle.Normal);
                toolButtons[id] = b.GetComponent<Image>();
            }
        }

        public void ShowShelf(bool on) { shelf.gameObject.SetActive(on); }
        public void PulseTool(string id, bool on)
        {
            foreach (var kv in toolButtons) kv.Value.transform.localScale = (on && kv.Key == id) ? Vector3.one * (1.08f + 0.05f * Mathf.Sin(Time.unscaledTime * 7f)) : Vector3.one;
        }
        public void ShakeTool(string id) { if (toolButtons.TryGetValue(id, out var im)) StartCoroutine(Shake(im.transform)); }
        System.Collections.IEnumerator Shake(Transform t)
        {
            float e = 0; var p = t.localPosition;
            while (e < 0.3f) { e += Time.unscaledDeltaTime; t.localPosition = p + new Vector3(Mathf.Sin(e * 60) * 8f * (1 - e / 0.3f), 0, 0); yield return null; }
            t.localPosition = p;
        }

        // ---------------- dialog ----------------
        // Figma D-020: the 몽이가 왔어요 box (1182x445), the blue button is the main answer, the orange one the second answer.
        void BuildDialog()
        {
            var p = UiKit.Fig(Canvas.transform, "dlg2_panel", UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, true, "Dialog");
            UiKit.Slice(p); dialog = p.gameObject;
            // laid out from the top (title, body) and the bottom (buttons) so the box can grow with the text (D-021)
            dialogTitle = UiKit.TitleLabel(p.transform, "", 50, UiKit.FigBrown, new Vector2(0.5f, 1f), new Vector2(0f, -74f), new Vector2(1100f, 90f));
            dialogBody = UiKit.TitleLabel(p.transform, "", 38, UiKit.FigBrown, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1060f, 100f), TextAnchor.UpperCenter, new Vector2(0.5f, 1f));
            dialogBody.horizontalOverflow = HorizontalWrapMode.Wrap; dialogBody.lineSpacing = 1.2f;
            dialogButton = DialogButton(p.transform, "dlg2_btn_blue", "Btn_ok");
            dialogButton2 = DialogButton(p.transform, "dlg2_btn_orange", "Btn_second");
            dialog.SetActive(false);
        }
        static Button DialogButton(Transform panel, string sprite, string name)
        {
            var r = FigmaLayout.Rects[sprite];
            var b = UiKit.SpriteButton(panel, name, sprite, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 118f), r.size, null);
            UiKit.Slice(b.GetComponent<Image>());
            var t = UiKit.TitleLabel(b.transform, "", 38, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(r.width, 80f));
            UiKit.AutoFit(b.GetComponent<Image>(), t, r.width, 80f, 560f);
            return b;
        }

        public void ShowDialog(string title, string body, string ok, Action onOk, string second = null, Action onSecond = null, bool bottom = false)
        {
            var drt = (RectTransform)dialog.transform;
            dialogTitle.text = title; dialogBody.text = body;
            dialogButton.onClick.RemoveAllListeners();
            UiKit.SetButtonText(dialogButton, ok); dialogButton.GetComponent<FitToText>().Apply();
            dialogButton.onClick.AddListener(() => { dialog.SetActive(false); onOk?.Invoke(); });
            var rt1 = (RectTransform)dialogButton.transform; var rt2 = (RectTransform)dialogButton2.transform;
            if (second != null)
            {
                UiKit.SetButtonText(dialogButton2, second); dialogButton2.GetComponent<FitToText>().Apply();
                float w1 = rt1.sizeDelta.x, w2 = rt2.sizeDelta.x, gap = 30f;
                rt1.anchoredPosition = new Vector2(-(gap + w2) / 2f, 118f); rt2.anchoredPosition = new Vector2((gap + w1) / 2f, 118f);
                rt2.gameObject.SetActive(true);
                dialogButton2.onClick.RemoveAllListeners();
                dialogButton2.onClick.AddListener(() => { dialog.SetActive(false); onSecond?.Invoke(); });
            }
            else { rt1.anchoredPosition = new Vector2(0f, 118f); rt2.gameObject.SetActive(false); }
            // the box: design size (1182x445) or larger when the title, body or buttons need it
            var design = FigmaLayout.Rects["dlg2_panel"].size;
            float buttons = rt1.sizeDelta.x + (second != null ? rt2.sizeDelta.x + 30f : 0f);
            float w = Mathf.Clamp(Mathf.Max(design.x, dialogTitle.preferredWidth + 160f, buttons + 160f), design.x, 1840f);
            dialogBody.rectTransform.sizeDelta = new Vector2(w - 120f, 100f);
            float bodyH = string.IsNullOrEmpty(body) ? 0f : dialogBody.preferredHeight;
            dialogBody.rectTransform.sizeDelta = new Vector2(w - 120f, bodyH);
            dialogTitle.rectTransform.sizeDelta = new Vector2(w - 80f, 90f);
            float h = Mathf.Max(design.y, 140f + bodyH + 40f + 161f);
            drt.sizeDelta = new Vector2(w, h);
            drt.anchorMin = drt.anchorMax = bottom ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = bottom ? new Vector2(0, 40f + h / 2f) : new Vector2(0, -14.7f);
            dialog.SetActive(true);
        }
        public bool DialogOpen => dialog != null && dialog.activeSelf;

        // ---------------- stamp ----------------
        // Figma D-020: a panel with eight stamp slots (one per friend that moved in; the new stamp lands in the next free one).
        readonly List<GameObject> stampSlotMarks = new List<GameObject>(); int stampSlot;
        static readonly float[] SlotX = { 591.6f, 794.1f, 996.7f, 1199.2f }, SlotY = { 358.3f, 555.2f };
        void BuildStamp()
        {
            var p = UiKit.Fig(Canvas.transform, "st_panel", UiKit.Edge.Middle, UiKit.Edge.Middle, 0f, 0f, false, "StampPanel");
            stampPanel = p.rectTransform; stampSlotMarks.Clear();
            var slot = FigmaLayout.Rects["st_slot"];
            // D-029 (아트보드 5 새 파일): the stamp is the paw print in a brown ring, drawn where Figma puts it in the first slot
            var paw = FigmaLayout.Rects["st_paw"];
            var pawOff = paw.center - new Rect(SlotX[0], SlotY[0], slot.width, slot.height).center;
            for (int i = 0; i < 8; i++)
            {
                var r = new Rect(SlotX[i % 4], SlotY[i / 4], slot.width, slot.height);
                UiKit.FigInRect(stampPanel, "Slot" + i, r, "st_panel", UiKit.UiSprite("st_slot"));
                var at = new Rect(r.x + pawOff.x + (slot.width - paw.width) / 2f, r.y + pawOff.y + (slot.height - paw.height) / 2f, paw.width, paw.height);
                var mark = UiKit.FigInRect(stampPanel, "Mark" + i, at, "st_panel", UiKit.UiSprite("st_paw")).gameObject;
                mark.SetActive(false); stampSlotMarks.Add(mark);
            }
            UiKit.FigIn(stampPanel, "st_pin", "st_panel"); UiKit.FigIn(stampPanel, "st_star", "st_panel");
            UiKit.FigTextAt(stampPanel, "st_panel", "수선 완료!", 61, UiKit.FigBrown, 962f, 301f, 700f, 110f);
            stampButton = UiKit.FigIn(stampPanel, "st_button", "st_panel", true, "Btn_stamp").gameObject.AddComponent<Button>();
            stampImage = stampButton.GetComponent<Image>(); stampButton.targetGraphic = stampImage;
            var cb = stampButton.colors; cb.highlightedColor = new Color(1.05f, 1.05f, 1.05f, 1f); cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f); cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.7f); stampButton.colors = cb;
            UiKit.FigTextAt(stampButton.transform, "st_button", "도장 찍기!", 43, UiKit.FigBrown, 973f, 791f, 320f, 80f);
            stampPanel.gameObject.SetActive(false);
        }

        /// <summary>Shows the stamp panel; filled = stamps already in the book (friends that moved in), the new one goes into the next slot.</summary>
        public void ShowStamp(bool on, Action onStamp = null, int filled = 0)
        {
            stampPanel.gameObject.SetActive(on);
            if (!on) return;
            stampSlot = Mathf.Clamp(filled, 0, 7);
            for (int i = 0; i < stampSlotMarks.Count; i++) { stampSlotMarks[i].SetActive(i < stampSlot); stampSlotMarks[i].transform.localScale = Vector3.one; }
            stampButton.interactable = true;
            stampButton.onClick.RemoveAllListeners();
            stampButton.onClick.AddListener(() => onStamp?.Invoke());
        }

        public void PlayStampAnim(Action onDone)
        {
            stampButton.interactable = false;
            StartCoroutine(StampRoutine(onDone));
        }

        System.Collections.IEnumerator StampRoutine(Action onDone)
        {
            var mark = stampSlotMarks[stampSlot]; mark.SetActive(true);
            float t = 0;
            while (t < 0.18f) { t += Time.unscaledDeltaTime; mark.transform.localScale = Vector3.one * Mathf.Lerp(3.2f, 1f, t / 0.18f); yield return null; }
            mark.transform.localScale = Vector3.one;
            t = 0; var p0 = stampPanel.anchoredPosition;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; stampPanel.anchoredPosition = p0 + new Vector2(0, Mathf.Sin(t * 70) * 10 * (1 - t / 0.3f)); yield return null; }
            stampPanel.anchoredPosition = p0;
            onDone?.Invoke();
        }

        // ---------------- customization ----------------
        public void BuildCustomization(AccessoryDefinition[] accs, Action<AccessoryDefinition> onToggle, Action onConfirm, Func<AccessoryDefinition, string> subtitle = null, string emptyHint = null)
        {
            if (custom != null) Destroy(custom.gameObject);
            // up to 3 items keep the roomy single column; more (9 accessories since D-010) go into two columns that fit the panel
            int n = accs.Length, cols = n > 4 ? 2 : 1, rows = Mathf.Max(1, Mathf.CeilToInt(n / (float)cols));
            float panelW = cols == 2 ? 640f : 520f, panelH = n > 3 ? 900f : 760f;
            var p = UiKit.Panel(Canvas.transform, "Custom", UiKit.Paper, new Vector2(1f, 0.5f), new Vector2(-panelW / 2f - 40f, 0), new Vector2(panelW, panelH), false);
            custom = p.rectTransform; accButtons.Clear();
            UiKit.Label(custom, "꾸며주기", 56, UiKit.Brown, new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(panelW - 40f, 80));
            UiKit.Label(custom, "고르면 바로 입어봐요. 다시 누르면 벗어요", 28, UiKit.Brown, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(panelW - 40f, 50), TextAnchor.MiddleCenter, FontStyle.Normal);
            // rows share the space between the header and the confirm button
            float top = 160f, bottom = 150f, rowH = Mathf.Min(150f, (panelH - top - bottom) / rows);
            float bw = cols == 2 ? 290f : 440f, bh = Mathf.Min(124f, rowH - 14f);
            bool compact = bh < 112f;
            for (int i = 0; i < n; i++)
            {
                var a = accs[i]; int col = i % cols, row = i / cols;
                float x = cols == 2 ? (col == 0 ? -152f : 152f) : 0f;
                var b = UiKit.MakeButton(custom, "", a.uiColor, new Vector2(0.5f, 1f), new Vector2(x, -top - rowH * (row + 0.5f)), new Vector2(bw, bh), () => onToggle(a));
                UiKit.SetButtonText(b, "");
                UiKit.Label(b.transform, a.displayName, compact ? 32 : 40, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, compact ? 13 : 14), new Vector2(bw - 20f, compact ? 44 : 60));
                string slot = a.slot == AccessorySlot.Head ? "머리" : a.slot == AccessorySlot.Face ? "얼굴" : "목";
                UiKit.Label(b.transform, subtitle != null ? subtitle(a) : slot + " 자리", compact ? 22 : 26, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, compact ? -24 : -34), new Vector2(bw - 20f, 36), TextAnchor.MiddleCenter, FontStyle.Normal);
                b.name = "Acc_" + a.id;
                accButtons[a.id] = b.GetComponent<Image>();
            }
            if (accs.Length == 0 && !string.IsNullOrEmpty(emptyHint))
                UiKit.Label(custom, emptyHint, 30, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(440, 360), TextAnchor.MiddleCenter, FontStyle.Normal);
            confirmButton = UiKit.MakeButton(custom, "이대로 확정!", UiKit.Peach, new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(440, 110), onConfirm, 42);
        }

        public void SetAccessorySelected(string id, bool on)
        {
            if (accButtons.TryGetValue(id, out var im)) { var c = im.color; im.transform.localScale = Vector3.one * (on ? 1.05f : 1f); im.color = on ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r * 0.92f, c.g * 0.92f, c.b * 0.92f, 1f); }
        }

        public void ShowCustomization(bool on) { if (custom != null) custom.gameObject.SetActive(on); }
        public void HideAll()
        {
            notebook.gameObject.SetActive(false); shelf.gameObject.SetActive(false); ShowStamp(false); ShowCustomization(false);
            Banner(null); back.gameObject.SetActive(false); SetInspectControls(false);
        }
    }
}
