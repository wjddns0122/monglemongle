using System;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The claw machine (Figma 11-26 "인형뽑기" 88:1866), laid over the machine up close: ◀ at the top left (그만하기), the five
    /// hearts in their pink pill at the top (tries left; one comes back every 30 minutes — the wait shows in a small pill under
    /// it), and at the bottom the four key caps (hold to move the claw), the cream tip pill and the big pink 시작하기 (then
    /// 내리기!). What came out shows in a cream pill in the middle. Prefab Assets/_UI수정/3_마을/Claw.prefab, opened with
    /// <see cref="Get"/>; the village drives it (VillageDirector.Claw).
    /// </summary>
    public class ClawView : MonoBehaviour
    {
        [Header("위")]
        public Button backButton;
        public Image[] hearts = new Image[0];
        [Tooltip("다 쓴 하트의 색 (그림에 곱해요)")] public Color usedHeart = new Color(1f, 1f, 1f, 0.32f);
        public RectTransform timer;
        public TMP_Text timerText;
        [Header("가운데")]
        public RectTransform result;
        public TMP_Text resultText;
        [Header("아래")]
        [Tooltip("왼쪽 · 오른쪽 · 위 · 아래")] public HoldButton[] arrows = new HoldButton[0];
        [Tooltip("움직일 수 없을 때 화살표의 투명도")] public float idleArrowAlpha = 0.45f;
        public RectTransform hint;
        public TMP_Text hintText;
        public Button startButton;
        public TMP_Text startText;
        [Tooltip("알약이 글자에 맞춰 늘어날 때 양옆 여백")] public float pillPad = 34f;
        [Tooltip("나온 것 알약: 글자 양옆 / 위아래 여백, 한 줄 높이 (마을 첫 안내 알약과 같은 크기)")] public float resultPad = 62f, resultPadY = 24f, resultHeight = 97f;

        public Action OnBack, OnStart;

        bool hooked;
        static ClawView current;

        public bool Visible => isActiveAndEnabled;
        public string HintShown => hintText != null ? hintText.text : "";
        public string ResultShown => result != null && result.gameObject.activeSelf ? resultText.text : "";
        public string StartLabel => startText != null ? startText.text : "";

        /// <summary>The claw screen (the prefab listed as "Claw"), made once per scene.</summary>
        public static ClawView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Claw");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<ClawView>(); current.name = "Claw";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        void Awake() => Hook();
        void OnDestroy() { if (current == this) current = null; }

        void Hook()
        {
            if (hooked) return; hooked = true;
            if (backButton != null) backButton.onClick.AddListener(() => OnBack?.Invoke());
            if (startButton != null) startButton.onClick.AddListener(() => OnStart?.Invoke());
        }

        public void Show() { Hook(); gameObject.SetActive(true); ShowResult(null); }
        public void Hide() => gameObject.SetActive(false);

        /// <summary>How many of the hearts are still there.</summary>
        public void SetHearts(int have)
        {
            for (int i = 0; i < hearts.Length; i++) hearts[i].color = i < have ? Color.white : usedHeart;
        }

        /// <summary>The wait for the next heart under the hearts (null hides it).</summary>
        public void SetTimer(string text) => Pill(timer, timerText, text, 0f);

        /// <summary>The tip above 시작하기 ("방향키 이동 · SPACE 내리기", or what just happened).</summary>
        public void SetHint(string text) => Pill(hint, hintText, text, 394.17f);

        /// <summary>What came out of the machine, in the middle (null hides it).</summary>
        public void ShowResult(string text)
        {
            Pill(result, resultText, text, 0f);
            if (result == null || string.IsNullOrEmpty(text)) return;
            // the first hint's pill: 62 in from its ends, 97 tall for one line, taller for two
            var size = resultText.GetPreferredValues(text, 1200f, 0f);
            result.sizeDelta = new Vector2(size.x + 2f * resultPad, Mathf.Max(resultHeight, size.y + 2f * resultPadY));
            var shape = result.GetComponent<Mongle.UI2.SoftRect>(); if (shape != null) { shape.radius = Vector4.one * resultHeight * 0.5f; shape.SetVerticesDirty(); }
        }

        /// <summary>The big button: its words and whether it can be pressed now.</summary>
        public void SetStart(string label, bool can)
        {
            startText.text = label; startButton.interactable = can;
            var g = startButton.GetComponent<CanvasGroup>(); if (g == null) g = startButton.gameObject.AddComponent<CanvasGroup>();
            g.alpha = can ? 1f : 0.6f;
        }

        /// <summary>The key caps work only while the claw is being moved.</summary>
        public void SetArrows(bool can)
        {
            foreach (var a in arrows)
            {
                if (a == null) continue;
                var b = a.GetComponent<Button>(); if (b != null) b.interactable = can;
                var g = a.GetComponent<CanvasGroup>(); if (g == null) g = a.gameObject.AddComponent<CanvasGroup>();
                g.alpha = can ? 1f : idleArrowAlpha;
            }
        }

        /// <summary>The keyboard's part: the key caps whose arrow (or W A S D) key is down, and 시작하기 / 내리기! while Space or
        /// Enter is, look pressed — the same squash and darker picture as a click (only while they can be used).</summary>
        public void ShowKeys(bool left, bool right, bool up, bool down, bool start)
        {
            var on = new[] { left, right, up, down };
            for (int i = 0; i < arrows.Length && i < on.Length; i++)
            {
                if (arrows[i] == null) continue;
                var b = arrows[i].GetComponent<Button>();
                Press(arrows[i].gameObject, on[i] && (b == null || b.interactable));
            }
            if (startButton != null) Press(startButton.gameObject, start && startButton.interactable);
        }

        static void Press(GameObject go, bool on) { var p = go.GetComponent<PressScale>(); if (p != null) p.Hold(on); }

        /// <summary>Whether the key cap <paramref name="i"/> (left, right, up, down) looks pressed (pointer or key).</summary>
        public bool LooksPressed(int i) => i >= 0 && i < arrows.Length && arrows[i] != null && arrows[i].GetComponent<PressScale>() is PressScale p && p.Down;

        /// <summary>Whether the key cap <paramref name="i"/> (left, right, up, down) is held down.</summary>
        public bool Held(int i) => i >= 0 && i < arrows.Length && arrows[i] != null && arrows[i].Held;

        /// <summary>A cream pill that hugs its words (at least <paramref name="minWidth"/> wide, around its own middle).</summary>
        void Pill(RectTransform pill, TMP_Text text, string words, float minWidth)
        {
            if (pill == null) return;
            bool on = !string.IsNullOrEmpty(words);
            pill.gameObject.SetActive(on);
            if (!on) return;
            text.text = words;
            var size = text.GetPreferredValues(words, 1200f, 0f);
            pill.sizeDelta = new Vector2(Mathf.Max(minWidth, size.x + 2f * pillPad), pill.sizeDelta.y);
        }

        // ---------------- test hooks ----------------
        public bool QaStart() { if (!Visible || !startButton.interactable) return false; startButton.onClick.Invoke(); return true; }
    }
}
