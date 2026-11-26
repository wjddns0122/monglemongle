using System.Collections.Generic;
using Mongle.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// The settings screen (D-038, Figma 58:1007 / 58:1098 / 58:1162): a left menu (일반 · 그래픽 · 오디오) and rows of
    /// dropdowns, sliders and switches bound to <see cref="GameSettings"/> — every change applies at once and is saved.
    /// </summary>
    public class SettingsScreen : MonoBehaviour
    {
        public Button back;
        public Button[] navButtons = new Button[3];
        public TMP_Text[] navLabels = new TMP_Text[3];
        public RectTransform navArrow;
        public GameObject[] pages = new GameObject[3];
        public TMP_Dropdown language, quality, resolution;
        /// <summary>Figma: the ▶ ends 27.5 px before the label's first letter (its picture has 2 px of padding).</summary>
        public float arrowGap = 25.5f;
        public Slider fov, mouse, pad, brightness, master, music, effects;
        public Toggle fullscreen, mute;
        public Color navOn = new Color(0.478f, 0.349f, 0.302f), navOff = new Color(0.604f, 0.478f, 0.424f);
        public System.Action Closed;
        int page; bool binding;
        List<Vector2Int> resList;
        List<int> qualityLevels;

        void Awake()
        {
            for (int i = 0; i < navButtons.Length; i++) { int k = i; if (navButtons[i] != null) navButtons[i].onClick.AddListener(() => Show(k)); }
            if (back != null) back.onClick.AddListener(Close);
            Hook(fov, v => GameSettings.Set(d => d.fov = v));
            Hook(mouse, v => GameSettings.Set(d => d.mouse = v));
            Hook(pad, v => GameSettings.Set(d => d.pad = v));
            Hook(brightness, v => GameSettings.Set(d => d.brightness = v));
            Hook(master, v => GameSettings.Set(d => d.master = v));
            Hook(music, v => GameSettings.Set(d => d.music = v));
            Hook(effects, v => GameSettings.Set(d => d.effects = v));
            if (fullscreen != null) fullscreen.onValueChanged.AddListener(v => { if (!binding) GameSettings.Set(d => d.fullscreen = v); });
            if (mute != null) mute.onValueChanged.AddListener(v => { if (!binding) GameSettings.Set(d => d.mute = v); });
            if (language != null) language.onValueChanged.AddListener(i => { if (!binding) GameSettings.Set(d => d.lang = i); });
            if (quality != null) quality.onValueChanged.AddListener(i => { if (!binding && qualityLevels != null && i < qualityLevels.Count) GameSettings.Set(d => d.quality = qualityLevels[i]); });
            if (resolution != null) resolution.onValueChanged.AddListener(i => { if (!binding && resList != null && i < resList.Count) GameSettings.Set(d => { d.resW = resList[i].x; d.resH = resList[i].y; }); });
        }

        void Hook(Slider s, System.Action<float> set) { if (s != null) s.onValueChanged.AddListener(v => { if (!binding) set(v); }); }

        void OnEnable() { Loc.Changed += Bind; Bind(); Show(page); }
        void OnDisable() { Loc.Changed -= Bind; }

        /// <summary>Fill every control from the current settings (and the option lists in the current language).</summary>
        public void Bind()
        {
            binding = true;
            var d = GameSettings.Data;
            if (language != null)
            {
                language.ClearOptions();
                language.AddOptions(new List<string> { Loc.Get("settings.lang.ko"), Loc.Get("settings.lang.en") });
                language.SetValueWithoutNotify(Mathf.Clamp(d.lang, 0, 1));
            }
            if (quality != null)
            {
                // the project's quality levels, named Low / Medium / High from the bottom, top three if there are more
                var names = QualitySettings.names; qualityLevels = new List<int>();
                var opts = new List<string>();
                string[] tags = { "settings.quality.low", "settings.quality.mid", "settings.quality.high" };
                int n = names.Length;
                for (int i = 0; i < Mathf.Min(3, n); i++) { int lvl = n <= 3 ? i : n - 3 + i; qualityLevels.Add(lvl); opts.Add(Loc.Get(tags[n >= 3 ? i : n == 2 ? i + 1 : 2])); }
                quality.ClearOptions(); quality.AddOptions(opts);
                int cur = d.quality >= 0 ? d.quality : QualitySettings.GetQualityLevel();
                int idx = qualityLevels.IndexOf(cur); quality.SetValueWithoutNotify(idx >= 0 ? idx : qualityLevels.Count - 1);
            }
            if (resolution != null)
            {
                resList = GameSettings.Resolutions();
                var opts = new List<string>(); foreach (var r in resList) opts.Add(r.x + "x" + r.y);
                resolution.ClearOptions(); resolution.AddOptions(opts);
                var want = new Vector2Int(d.resW > 0 ? d.resW : Screen.width, d.resH > 0 ? d.resH : Screen.height);
                int idx = resList.IndexOf(want); resolution.SetValueWithoutNotify(idx >= 0 ? idx : 0);
            }
            Set(fov, d.fov); Set(mouse, d.mouse); Set(pad, d.pad); Set(brightness, d.brightness);
            Set(master, d.master); Set(music, d.music); Set(effects, d.effects);
            if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(d.fullscreen);
            if (mute != null) mute.SetIsOnWithoutNotify(d.mute);
            foreach (var sv in GetComponentsInChildren<SwitchVisual>(true)) sv.Snap();
            binding = false;
        }

        static void Set(Slider s, float v) { if (s != null) s.SetValueWithoutNotify(v); }

        public void Show(int p)
        {
            page = Mathf.Clamp(p, 0, pages.Length - 1);
            for (int i = 0; i < pages.Length; i++) if (pages[i] != null) pages[i].SetActive(i == page);
            for (int i = 0; i < navLabels.Length; i++) if (navLabels[i] != null) navLabels[i].color = i == page ? navOn : navOff;
            PlaceArrow();
        }

        // Every frame: the label text changes width after a language switch (LocText refreshes after our Loc.Changed handler).
        void LateUpdate() { PlaceArrow(); }

        void PlaceArrow()
        {
            if (navArrow == null || navLabels.Length <= page || navLabels[page] == null) return;
            var lp = navLabels[page].rectTransform;   // both top-anchored: centre the arrow on the label, just left of its text
            float textLeft = lp.anchoredPosition.x + lp.rect.width * 0.5f - Mathf.Min(navLabels[page].preferredWidth, lp.rect.width) * 0.5f;
            navArrow.anchoredPosition = new Vector2(Mathf.Max(20f, textLeft - navArrow.rect.width - arrowGap), lp.anchoredPosition.y - (lp.rect.height - navArrow.rect.height) * 0.5f);
        }

        public void Close() { Closed?.Invoke(); if (Closed == null) gameObject.SetActive(false); }
    }
}
