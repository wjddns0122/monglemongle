using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The photo album (Figma 11-26 "앨범" 88:1393): an open ring binder. The right page holds the photos as nine little cards
    /// a page (◀ 1/2 ▶ turns the pages), the chosen card has a blue edge; the left page shows that photo big in a gingham frame
    /// with tape, its date and name underneath. Prefab Assets/_UI수정/3_마을/Album.prefab, opened by the village with
    /// <see cref="Open"/>; the village only hands it the list.
    /// </summary>
    public class AlbumView : MonoBehaviour
    {
        /// <summary>One photo: its line ("2026.11.26 · 몽이") and how to load the picture (loaded when its page shows).</summary>
        public struct Entry
        {
            public string caption;
            public Func<Texture2D> load;
            public Entry(string caption, Func<Texture2D> load) { this.caption = caption; this.load = load; }
        }

        [Serializable]
        public class Card
        {
            public Button button;
            [Tooltip("고른 카드의 파란 테두리")] public GameObject mark;
            public RawImage photo;
        }

        public const int PerPage = 9;

        [Header("왼쪽 장 (고른 사진)")]
        public RawImage bigPhoto;
        public GameObject slot;
        public TMP_Text caption, emptyText;
        [Tooltip("큰 사진에서 잘라 낼 둘레 몫 (0: 찍을 때 고른 액자 무늬까지 그대로 보여요)")] public float bakedBorder = 0f;
        [Tooltip("큰 사진 뒤의 폴라로이드")] public GameObject polaroid;
        [Tooltip("폴라로이드를 채우는 액자 무늬 (사진을 찍을 때 고른 것, 사진 테두리와 이어져요)")] public RawImage polaroidPattern;
        [Tooltip("큰 사진의 한 변 / 폴라로이드의 왼쪽·위 여백 / 아래 여백")] public float bigSize = 318.4f, polaroidSide = 16f, polaroidFoot = 70f;
        /// <summary>The pattern the chosen photo was taken with (Mongle.Photo.PhotoFrames index; 0 the plain frame).</summary>
        public int ChosenFrame { get; private set; }
        [Header("오른쪽 장 (사진 카드)")]
        public Card[] cards = new Card[0];
        public Button prevButton, nextButton;
        public TMP_Text pageLabel;
        [Tooltip("넘길 수 없는 쪽 화살표의 투명도")] public float idleArrowAlpha = 0.35f;
        public Button closeButton;

        public Action OnClosed;

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<int, Texture2D> loaded = new Dictionary<int, Texture2D>();
        int page, chosen;
        static AlbumView current;

        public static bool IsOpen => current != null && current.isActiveAndEnabled;

        /// <summary>Opens the album on its first photo (the prefab listed as "Album").</summary>
        public static AlbumView Open(IList<Entry> list)
        {
            if (current == null)
            {
                var prefab = ViewIndex.Prefab("Album");
                if (prefab == null) return null;
                ViewUtil.EnsureEventSystem();
                current = Instantiate(prefab).GetComponent<AlbumView>(); current.name = "Album";
            }
            current.Show(list);
            return current;
        }

        public static void Close() { if (current != null) current.Hide(); }

        public int Count => entries.Count;
        public int Page => page;
        public int PageCount => Mathf.Max(1, (entries.Count + PerPage - 1) / PerPage);
        public int Chosen => chosen;

        void Awake()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                int k = i;
                if (cards[i].button != null) cards[i].button.onClick.AddListener(() => Choose(page * PerPage + k));
            }
            if (prevButton != null) prevButton.onClick.AddListener(() => ShowPage(page - 1));
            if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(page + 1));
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
        }

        void OnDestroy() { Forget(); if (current == this) current = null; }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (kb.leftArrowKey.wasPressedThisFrame) ShowPage(page - 1);
            if (kb.rightArrowKey.wasPressedThisFrame) ShowPage(page + 1);
        }

        public void Show(IList<Entry> list)
        {
            Forget();
            entries.Clear();
            if (list != null) entries.AddRange(list);
            gameObject.SetActive(true);
            page = 0; chosen = 0;
            ShowPage(0);
            Choose(0);
        }

        public void Hide()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            Forget();
            OnClosed?.Invoke();
        }

        /// <summary>Turns to a page (0-based; outside the album does nothing): its cards show their photos.</summary>
        public void ShowPage(int p)
        {
            if (p < 0 || p >= PageCount) return;
            page = p;
            for (int i = 0; i < cards.Length; i++)
            {
                var c = cards[i]; int index = page * PerPage + i;
                var tex = index < entries.Count ? Picture(index) : null;
                if (c.photo != null) { c.photo.texture = tex; c.photo.enabled = tex != null; }
                if (c.button != null) c.button.interactable = index < entries.Count;
            }
            if (pageLabel != null) pageLabel.text = (page + 1) + "/" + PageCount;
            Arrow(prevButton, page > 0); Arrow(nextButton, page < PageCount - 1);
            MarkChosen();
        }

        /// <summary>
        /// The whole polaroid in the photo's own frame pattern (user 2026-11-27: "프레임을 전체 적용"): found in the photo's border,
        /// tiled at the size it has in the photo and lined up with it, so the border runs on into the polaroid without a seam.
        /// </summary>
        void FillPolaroid(Texture2D tex)
        {
            if (polaroidPattern == null) return;
            ChosenFrame = tex != null ? Mongle.Photo.PhotoFrames.Detect(tex) : 0;
            if (tex == null) { polaroidPattern.texture = null; return; }
            var pattern = Mongle.Photo.PhotoFrames.Pattern(ChosenFrame);
            var tile = (Vector2)Mongle.Photo.PhotoFrames.Size(ChosenFrame) * (Mongle.Photo.PhotoFrames.PhotoScale(tex.width) * bigSize / tex.width);   // frame px
            var card = ((RectTransform)polaroidPattern.transform).rect.size;
            polaroidPattern.texture = pattern;
            // the polaroid's bottom left is (-side, -foot) from the photo's: the pattern's (0, 0) is the photo's bottom left
            polaroidPattern.uvRect = new Rect(-polaroidSide / tile.x, -polaroidFoot / tile.y, card.x / tile.x, card.y / tile.y);
        }

        /// <summary>Shows one photo big on the left page (its own frame border cut off: the gingham frame goes round it).</summary>
        public void Choose(int index)
        {
            bool has = index >= 0 && index < entries.Count;
            if (has) chosen = index;
            var tex = has ? Picture(index) : null;
            if (bigPhoto != null)
            {
                bigPhoto.texture = tex; bigPhoto.enabled = tex != null;
                float b = Mathf.Clamp(bakedBorder, 0f, 0.45f);
                bigPhoto.uvRect = new Rect(b, b, 1f - 2f * b, 1f - 2f * b);
            }
            if (slot != null) slot.SetActive(tex == null);
            if (polaroid != null) polaroid.SetActive(tex != null);
            FillPolaroid(tex as Texture2D);
            if (caption != null) caption.text = has ? entries[index].caption : "";
            if (emptyText != null) emptyText.gameObject.SetActive(entries.Count == 0);
            MarkChosen();
        }

        void MarkChosen()
        {
            for (int i = 0; i < cards.Length; i++)
                if (cards[i].mark != null) cards[i].mark.SetActive(page * PerPage + i == chosen && chosen < entries.Count);
        }

        void Arrow(Button b, bool can)
        {
            if (b == null) return;
            b.interactable = can;
            var g = b.GetComponent<CanvasGroup>(); if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
            g.alpha = can ? 1f : idleArrowAlpha;
        }

        /// <summary>The photo of an entry, loaded once while the album is open (null when its file is gone).</summary>
        Texture2D Picture(int index)
        {
            if (loaded.TryGetValue(index, out var t)) return t;
            t = entries[index].load != null ? entries[index].load() : null;
            loaded[index] = t;
            return t;
        }

        /// <summary>Lets go of the loaded photos (they are the album's own copies).</summary>
        void Forget()
        {
            foreach (var t in loaded.Values)
                if (t != null) { if (Application.isPlaying) Destroy(t); else DestroyImmediate(t); }
            loaded.Clear();
            if (bigPhoto != null) bigPhoto.texture = null;
            foreach (var c in cards) if (c.photo != null) c.photo.texture = null;
        }
    }
}
