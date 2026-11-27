using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// Dressing up after the stamp (Figma 115:1573 "인게임3 사본"): the cream pill at the top says what to do, the panel at the
    /// bottom holds 꾸며주기, its one line of help, 이대로 확정 and five cards a page (◀ ▶ turn the pages). A card is the accessory's
    /// picture and name; the blue edge marks what is worn, the little blue badge how many the player has (friends only).
    /// Assets/_UI수정/2_수선점/Customize.prefab, listed as "Customize" in Resources/Views/ViewIndex.
    /// </summary>
    public class CustomizeView : MonoBehaviour
    {
        [Serializable]
        public class Card
        {
            public Button button;
            public Image plate;
            public Image icon;
            public TMP_Text label;
            public RectTransform badge;
            public TMP_Text count;
        }

        /// <summary>One accessory on a card; <see cref="count"/> below 0 hides the badge.</summary>
        public struct Item { public string id, name; public Sprite icon; public int count; }

        [Header("위 안내 알약")]
        public RectTransform title;
        public TMP_Text titleText;
        [Tooltip("알약 글자 양옆 여백")] public float titlePad = 62f;
        [Tooltip("알약의 가장 작은 폭")] public float titleMinWidth = 420f;
        [Header("아래 판")]
        public RectTransform panel;
        public TMP_Text hintText, emptyText;
        public Button confirmButton, prevButton, nextButton;
        public Card[] cards = new Card[0];
        public Sprite plateOff, plateOn;
        [Tooltip("개수 배지 글자 양옆 여백")] public float badgePad = 9f;
        [Tooltip("개수 배지의 가장 작은 폭")] public float badgeMinWidth = 38.47f;
        [Tooltip("넘길 쪽이 없을 때 화살표의 진하기")] [Range(0f, 1f)] public float idleAlpha = 0.45f;
        [Tooltip("카드가 다섯 장보다 적으면 가운데로 모아요")] public bool centerFew = true;
        [Tooltip("카드 사이 거리 (가운데로 모을 때)")] public float cardPitch = 170.035f;

        public Action<string> OnPick;
        public Action OnConfirm;

        readonly List<Item> items = new List<Item>();
        readonly HashSet<string> worn = new HashSet<string>();
        int page;
        [NonSerialized] float[] baseX;   // where the cards stand in the prefab (a full row)

        public int PerPage => Mathf.Max(1, cards.Length);
        public int Page => page;
        public int Pages => Mathf.Max(1, Mathf.CeilToInt(items.Count / (float)PerPage));

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int k = i;
                if (cards[i] != null && cards[i].button != null) cards[i].button.onClick.AddListener(() => Pick(k));
            }
            if (prevButton != null) prevButton.onClick.AddListener(() => Turn(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Turn(1));
            if (confirmButton != null) confirmButton.onClick.AddListener(() => OnConfirm?.Invoke());
            ViewUtil.EnsureEventSystem();
        }

        void Pick(int slot)
        {
            int i = page * PerPage + slot;
            if (i >= 0 && i < items.Count) OnPick?.Invoke(items[i].id);
        }

        /// <summary>Opens on the first page: the pill's line, the accessories, and what to say when there are none.</summary>
        public void Show(string line, IList<Item> list, string whenEmpty = null)
        {
            items.Clear(); if (list != null) items.AddRange(list);
            page = 0;
            if (emptyText != null) { emptyText.text = whenEmpty ?? ""; emptyText.gameObject.SetActive(items.Count == 0 && !string.IsNullOrEmpty(whenEmpty)); }
            gameObject.SetActive(true);
            Title(line);
            Draw();
        }

        public void Hide() => gameObject.SetActive(false);
        public bool Shown => gameObject.activeSelf;

        /// <summary>The line in the pill at the top (none hides the pill); the pill hugs it with <see cref="titlePad"/> on each side.</summary>
        public void Title(string line)
        {
            if (title == null) return;
            if (string.IsNullOrEmpty(line)) { title.gameObject.SetActive(false); return; }
            titleText.text = line;
            title.sizeDelta = new Vector2(Mathf.Max(titleMinWidth, titleText.GetPreferredValues(line, 99999f, 0f).x + titlePad * 2f), title.sizeDelta.y);
            title.gameObject.SetActive(true);
        }
        public string TitleText => title != null && title.gameObject.activeSelf ? titleText.text : "";

        /// <summary>What the toy wears now (accessory ids): those cards get the blue edge.</summary>
        public void SetWorn(IEnumerable<string> ids)
        {
            worn.Clear(); if (ids != null) foreach (var id in ids) if (!string.IsNullOrEmpty(id)) worn.Add(id);
            Draw();
        }
        public bool IsWorn(string id) => worn.Contains(id);

        public void Turn(int step)
        {
            int to = Mathf.Clamp(page + step, 0, Pages - 1);
            if (to == page) return;
            page = to; Draw();
        }

        void Draw()
        {
            if (baseX == null || baseX.Length != cards.Length)
            {
                baseX = new float[cards.Length];
                for (int s = 0; s < cards.Length; s++) if (cards[s] != null && cards[s].button != null) baseX[s] = ((RectTransform)cards[s].button.transform).anchoredPosition.x;
            }
            // fewer than a full row: the shown cards gather in the middle (user 2026-11-27: "Center로 고정")
            int shown = Mathf.Clamp(items.Count - page * PerPage, 0, cards.Length);
            float shift = centerFew ? (cards.Length - shown) * cardPitch * 0.5f : 0f;
            for (int s = 0; s < cards.Length; s++)
                if (cards[s] != null && cards[s].button != null)
                {
                    var rt = (RectTransform)cards[s].button.transform;
                    rt.anchoredPosition = new Vector2(baseX[s] + shift, rt.anchoredPosition.y);
                }
            for (int s = 0; s < cards.Length; s++)
            {
                var c = cards[s]; if (c == null || c.button == null) continue;
                int i = page * PerPage + s;
                bool on = i < items.Count;
                c.button.gameObject.SetActive(on);
                if (!on) continue;
                var it = items[i];
                c.button.name = "Acc_" + it.id;
                if (c.label != null) c.label.text = it.name;
                if (c.icon != null) { c.icon.sprite = it.icon; c.icon.enabled = it.icon != null; }
                if (c.plate != null) c.plate.sprite = worn.Contains(it.id) && plateOn != null ? plateOn : plateOff != null ? plateOff : c.plate.sprite;
                if (c.badge != null)
                {
                    c.badge.gameObject.SetActive(it.count >= 0);
                    if (it.count >= 0 && c.count != null)
                    {
                        c.count.text = "x" + it.count;
                        c.badge.sizeDelta = new Vector2(Mathf.Max(badgeMinWidth, c.count.GetPreferredValues(c.count.text, 99999f, 0f).x + badgePad * 2f), c.badge.sizeDelta.y);
                    }
                }
            }
            Dim(prevButton, page > 0); Dim(nextButton, page < Pages - 1);
        }

        void Dim(Button b, bool on)
        {
            if (b == null) return;
            b.interactable = on;
            var g = b.targetGraphic; if (g == null) return;
            var col = g.color; col.a = on ? 1f : idleAlpha; g.color = col;
        }

        /// <summary>The card showing <paramref name="id"/> on this page, or null.</summary>
        public Card CardOf(string id)
        {
            for (int s = 0; s < cards.Length; s++)
            {
                int i = page * PerPage + s;
                if (i < items.Count && items[i].id == id) return cards[s];
            }
            return null;
        }
    }
}
