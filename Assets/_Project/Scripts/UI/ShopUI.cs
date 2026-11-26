using System;
using System.Collections.Generic;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI
{
    /// <summary>The old code-built canvas still in use: the dress-up window (repair shop) and the canvas the village's own panels (shop, storage, quest board) sit on, with their old skins. The 11-26 views replace them screen by screen (RepairHudView, VillageHudView …).</summary>
    public class ShopUI : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        RectTransform custom; readonly Dictionary<string, Image> accButtons = new Dictionary<string, Image>();
        Button confirmButton;

        public void Build()
        {
            Canvas = UiKit.CreateCanvas("ShopUI", 10, transform);
        }


        string skin;
        /// <summary>
        /// D-035: lays the screen's editable prefab (Resources/UI/Skins/&lt;name&gt;, edited in Unity) over what the code just built.
        /// Parts made later — the tool tiles, the dress-up list — get theirs when they are made.
        /// </summary>
        public void ApplySkin(string name) { skin = name; UiSkin.Apply(Canvas.transform, name); Mongle.UI2.UiFront.AttachFor(Canvas.transform, name); }   // D-040: the Figma screens in front
        /// <summary>Lays the screen's prefab over a part made after <see cref="ApplySkin"/> (a panel, a list filled again).</summary>
        public void ApplySkinTo(Transform part) { if (skin != null && part != null) UiSkin.ApplyUnder(Canvas.transform, part, skin); }
        public string Skin => skin;

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
            var hint = UiKit.Label(custom, "고르면 바로 입어봐요. 다시 누르면 벗어요", 28, UiKit.Brown, new Vector2(0.5f, 1f), new Vector2(0, -120), new Vector2(panelW - 40f, 50), TextAnchor.MiddleCenter, FontStyle.Normal);
            // one line under the title: a narrow panel makes the words smaller instead of breaking them over three lines
            hint.resizeTextForBestFit = true; hint.resizeTextMinSize = 18; hint.resizeTextMaxSize = 28; hint.verticalOverflow = VerticalWrapMode.Truncate;
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
            ApplySkinTo(custom);   // D-035
        }

        public void SetAccessorySelected(string id, bool on)
        {
            if (accButtons.TryGetValue(id, out var im)) { var c = im.color; im.transform.localScale = Vector3.one * (on ? 1.05f : 1f); im.color = on ? new Color(c.r, c.g, c.b, 1f) : new Color(c.r * 0.92f, c.g * 0.92f, c.b * 0.92f, 1f); }
        }

        public void ShowCustomization(bool on) { if (custom != null) custom.gameObject.SetActive(on); }
        public void HideAll() { ShowCustomization(false); }
    }
}
