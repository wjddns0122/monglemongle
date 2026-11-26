using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The card of a friend (Figma 11-26 "캐릭터 선택하면 뜨는창" 88:1912), along the bottom of the village: on the left its first
    /// photo, its name, the day it moved in and what kind of toy it is (프로필); on the right its accessories as little cards —
    /// the picture, the name, how many are in the box (×N), a blue edge on what it wears — six whole cards a page, ◀ ▶ turn the
    /// pages (no card is ever cut in half at the edge; the user's call over Figma's half-shown seventh) (꾸며주기). Pressing
    /// a card puts it on, pressing it again takes it off. Prefab Assets/_UI수정/3_마을/FriendCard.prefab, opened with
    /// <see cref="Get"/>; the village hands it the friend and listens to <see cref="OnToggle"/>.
    /// </summary>
    public class FriendCardView : MonoBehaviour
    {
        /// <summary>One accessory card.</summary>
        public struct Item
        {
            public string id, name;
            public Sprite icon;
            public Color color;
            public int count;
            public bool worn;
        }

        [Header("프로필")]
        public RawImage photo;
        public TMP_Text nameText, dateText, kindText;
        [Tooltip("사진 둘레에 그려 넣은 액자 몫 (잘라 내요)")] public float bakedBorder = 1f / 16f;
        [Header("꾸며주기")]
        [Tooltip("카드 줄 (가려진 창 안에서 옆으로 미끄러져요)")] public RectTransform content;
        [Tooltip("카드 하나 (복사해서 써요): Plate, Worn, Icon, Swatch, Name, Badge/Count")] public GameObject cardTemplate;
        public TMP_Text emptyText;
        public Button prevButton, nextButton;
        [Tooltip("카드 사이 간격 (왼쪽 끝에서 다음 카드 왼쪽 끝까지)")] public float step = 141.3f;
        [Tooltip("한 쪽에 보이는 카드 수 (반만 보이는 카드 없이, ◀ ▶ 로 한 쪽씩 넘겨요)")] public int perPage = 6;
        [Tooltip("넘길 때 미끄러지는 빠르기 (px/초)")] public float slideSpeed = 2600f;
        [Tooltip("넘길 수 없는 쪽 화살표의 투명도")] public float idleArrowAlpha = 0.35f;

        public Action<string> OnToggle;

        readonly List<GameObject> cards = new List<GameObject>();
        readonly List<Item> items = new List<Item>();
        int page; float targetX;
        bool hooked;
        static FriendCardView current;

        public bool Visible => isActiveAndEnabled;
        public IEnumerable<string> CardIds => items.Select(i => i.id);
        public string NameShown => nameText != null ? nameText.text : "";
        public int Page => page;
        public int PageCount => Mathf.Max(1, (items.Count + perPage - 1) / perPage);

        /// <summary>The card (the prefab listed as "FriendCard"), made once per scene.</summary>
        public static FriendCardView Get()
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("FriendCard");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<FriendCardView>(); current.name = "FriendCard";
                current.gameObject.SetActive(false);
            }
            return current;
        }

        void Awake() => Hook();
        void Hook()
        {
            if (hooked) return; hooked = true;
            if (prevButton != null) prevButton.onClick.AddListener(() => Turn(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Turn(1));
        }

        void OnDestroy() { ForgetPhoto(); if (current == this) current = null; }

        void Update()
        {
            if (content == null) return;
            var p = content.anchoredPosition;
            if (!Mathf.Approximately(p.x, targetX)) { p.x = Mathf.MoveTowards(p.x, targetX, Time.unscaledDeltaTime * slideSpeed); content.anchoredPosition = p; }
        }

        // ---------------- what it shows ----------------
        /// <summary>Opens on a friend: its photo (the card keeps it and lets it go when closed or shown again), name, day, kind and accessories.</summary>
        public void Show(string name, Texture2D picture, string movedIn, string kind, IList<Item> list)
        {
            Hook();
            gameObject.SetActive(true);
            nameText.text = name; dateText.text = movedIn ?? ""; kindText.text = kind ?? "";
            ForgetPhoto();
            photo.texture = picture; photo.enabled = picture != null;
            float b = Mathf.Clamp(bakedBorder, 0f, 0.45f); photo.uvRect = new Rect(b, b, 1f - 2f * b, 1f - 2f * b);
            page = 0;
            SetItems(list);
            if (content != null) content.anchoredPosition = new Vector2(targetX, content.anchoredPosition.y);   // a new friend starts on the first page at once
        }

        /// <summary>The accessory cards again (after one was put on or taken off): the page turned to stays.</summary>
        public void SetItems(IList<Item> list)
        {
            items.Clear(); if (list != null) items.AddRange(list);
            for (int i = 0; i < Mathf.Max(items.Count, cards.Count); i++)
            {
                if (i >= items.Count) { cards[i].SetActive(false); continue; }
                if (i >= cards.Count)
                {
                    var c = Instantiate(cardTemplate, content, false);
                    int k = i; var btn = c.GetComponent<Button>(); if (btn != null) btn.onClick.AddListener(() => { if (k < items.Count) OnToggle?.Invoke(items[k].id); });
                    cards.Add(c);
                }
                Fill(cards[i], items[i], i);
            }
            // the strip: the template's own margin on both ends, the cards a step apart
            var t = (RectTransform)cardTemplate.transform;
            if (content != null) content.sizeDelta = new Vector2(2f * t.anchoredPosition.x + Mathf.Max(0f, items.Count * step - (step - t.sizeDelta.x)), content.sizeDelta.y);
            if (emptyText != null) emptyText.gameObject.SetActive(items.Count == 0);
            page = Mathf.Clamp(page, 0, PageCount - 1); targetX = -page * perPage * step;
            Arrows();
        }

        void Fill(GameObject c, Item it, int i)
        {
            c.SetActive(true); c.name = "Dress_" + it.id;
            var rt = (RectTransform)c.transform; rt.anchoredPosition = new Vector2(((RectTransform)cardTemplate.transform).anchoredPosition.x + i * step, rt.anchoredPosition.y);
            c.transform.Find("Worn").gameObject.SetActive(it.worn);
            var icon = c.transform.Find("Icon").GetComponent<Image>(); icon.sprite = it.icon; icon.enabled = it.icon != null;
            var sw = c.transform.Find("Swatch").GetComponent<Graphic>(); sw.color = it.color; sw.gameObject.SetActive(it.icon == null);
            c.transform.Find("Name").GetComponent<TMP_Text>().text = it.name;
            var badge = c.transform.Find("Badge"); badge.gameObject.SetActive(it.count > 0);
            badge.Find("Count").GetComponent<TMP_Text>().text = "x" + it.count;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            ForgetPhoto();
        }

        void ForgetPhoto()
        {
            if (photo == null || photo.texture == null) return;
            var t = photo.texture; photo.texture = null;
            if (Application.isPlaying) Destroy(t); else DestroyImmediate(t);
        }

        // ---------------- turning the pages ----------------
        /// <summary>◀ (-1) or ▶ (+1): the next six cards slide in (nothing past the first or the last page).</summary>
        public void Turn(int dir)
        {
            int to = Mathf.Clamp(page + dir, 0, PageCount - 1);
            if (to == page) return;
            page = to; targetX = -page * perPage * step;
            Arrows();
        }

        /// <summary>Where the strip is heading (0 on the first page, a page of cards further left on each next one).</summary>
        public float TargetX => targetX;

        void Arrows()
        {
            Arrow(prevButton, page > 0); Arrow(nextButton, page < PageCount - 1);
        }

        void Arrow(Button b, bool can)
        {
            if (b == null) return;
            b.interactable = can;
            var g = b.GetComponent<CanvasGroup>(); if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
            g.alpha = can ? 1f : idleArrowAlpha;
        }

        // ---------------- test hooks ----------------
        public bool QaToggle(string id)
        {
            if (!Visible || !items.Any(i => i.id == id)) return false;
            OnToggle?.Invoke(id); return true;
        }
        public bool Worn(string id) => items.Any(i => i.id == id && i.worn);
        public int CountOf(string id) => items.Where(i => i.id == id).Select(i => i.count).FirstOrDefault();
    }
}
