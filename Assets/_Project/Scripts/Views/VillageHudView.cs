using System;
using System.Collections.Generic;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The village HUD (Figma 11-26 "대화1" 88:231, "마을 초대" 88:510, "탈것" 88:2367): the home button, the 퀘스트 pill and what is
    /// under way beneath it, the zoom pair at the left, the coin pill and the gear at the top right, the three tiles down the
    /// right edge (P 가구 옮기기 · E 앨범 · T 보관함, each with a little tip and a blue edge on hover), the cream hint pill at the
    /// top and the ride bar at the bottom. Scene object of the Village (Assets/_UI수정/3_마을/VillageHud.prefab); the village
    /// only listens to it (VillageDirector).
    /// </summary>
    public class VillageHudView : MonoBehaviour
    {
        [Header("위 줄")]
        public GameObject top;
        public Button homeButton, questButton, gearButton;
        public GameObject questBadge;
        public TMP_Text questBadgeText;
        public RectTransform coins;
        public TMP_Text coinText;
        [Header("왼쪽 (확대·축소)")]
        public GameObject zoom;
        public Button zoomInButton, zoomOutButton;
        [Header("오른쪽 도구 (P · E · T)")]
        public GameObject toolbar;
        public Button furnitureButton, albumButton, storageButton;
        [Tooltip("도구마다 파란 테두리 (가구 · 앨범 · 보관함)")] public GameObject[] glows = new GameObject[0];
        [Tooltip("도구마다 말풍선 도움말 (가구 · 앨범 · 보관함)")] public GameObject[] tips = new GameObject[0];
        [Tooltip("새 소식 점 (Figma 115:1398): 퀘스트 · 앨범 · 보관함")] public GameObject storageBadge;
        public TMP_Text storageBadgeText;
        public GameObject albumBadge;
        [Tooltip("P · E · T 키로 도구를 눌러요")] public bool keys = true;
        [Header("안내")]
        public RectTransform hint;
        public TMP_Text hintText;
        [Tooltip("안내 알약의 글자 양옆 여백")] public float hintPad = 62f;
        [Tooltip("안내 알약의 글자 위아래 여백 (Figma 115:1255: 두 줄 94.6 + 24 + 24 = 142.6)")] public float hintPadY = 24f;
        public float hintMinWidth = 420f, hintMaxWidth = 1060f;
        public RectTransform tracker;
        public TMP_Text trackerText;
        [Header("별 찾기")]
        public RectTransform hunt;
        public TMP_Text huntText;
        public RectTransform huntInfo;
        public TMP_Text huntInfoText;
        [Tooltip("별 찾기 알약 글자 양옆 여백 / 설명 알약 글자 양옆 여백")] public float huntPad = 50f, huntInfoPad = 32f;
        [Header("탈것 알림")]
        public GameObject rideBar;
        public TMP_Text rideText;
        public Button rideButton;

        public Action OnHome, OnQuest, OnFurniture, OnAlbum, OnStorage, OnRide;
        /// <summary>The zoom pair: below 1 brings the camera closer (＋), above 1 steps back (－).</summary>
        public Action<float> OnZoom;

        float hintUntil = float.MaxValue, tipsUntil;
        bool placing;
        readonly bool[] hovered = new bool[3];

        void Awake()
        {
            Hook(homeButton, () => OnHome?.Invoke());
            Hook(questButton, () => OnQuest?.Invoke());
            Hook(furnitureButton, () => OnFurniture?.Invoke());
            Hook(albumButton, () => OnAlbum?.Invoke());
            Hook(storageButton, () => OnStorage?.Invoke());
            Hook(rideButton, () => OnRide?.Invoke());
            Hook(zoomInButton, () => OnZoom?.Invoke(0.85f));
            Hook(zoomOutButton, () => OnZoom?.Invoke(1.18f));
            Hook(gearButton, UiFront.OpenSettings);
            var tiles = new[] { furnitureButton, albumButton, storageButton };
            for (int i = 0; i < tiles.Length; i++) if (tiles[i] != null) Hover(tiles[i].gameObject, i);
            ViewUtil.EnsureEventSystem();
        }

        static void Hook(Button b, UnityEngine.Events.UnityAction a) { if (b != null) b.onClick.AddListener(a); }

        void Hover(GameObject go, int i)
        {
            var t = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter }; enter.callback.AddListener(_ => { hovered[i] = true; RefreshTiles(); });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit }; exit.callback.AddListener(_ => { hovered[i] = false; RefreshTiles(); });
            t.triggers.Add(enter); t.triggers.Add(exit);
        }

        void Update()
        {
            if (hint != null && hint.gameObject.activeSelf && Time.unscaledTime > hintUntil) Hint(null);
            if (tipsUntil > 0f && Time.unscaledTime > tipsUntil) { tipsUntil = 0f; RefreshTiles(); }
            Pulse(questBadge); Pulse(storageBadge); Pulse(albumBadge);
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (!keys || kb == null || toolbar == null || !toolbar.activeInHierarchy || WindowHidden) return;   // not under a window (a friend's talk …)
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null && EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null) return;
            if (kb.pKey.wasPressedThisFrame) OnFurniture?.Invoke();
            if (kb.eKey.wasPressedThisFrame) OnAlbum?.Invoke();
            if (kb.tKey.wasPressedThisFrame) OnStorage?.Invoke();
        }

        static void Pulse(GameObject badge)
        {
            if (badge != null && badge.activeInHierarchy) badge.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        // ---------------- numbers and news ----------------
        public void SetCoins(int n) { if (coinText != null) coinText.text = n.ToString(); }
        public string CoinsText => coinText != null ? coinText.text : "";

        public void SetQuestNews(int n) => Badge(questBadge, questBadgeText, n);
        /// <summary>New photos in the album since it was last opened: the dot on the 앨범 tile.</summary>
        public void SetAlbumNews(int n) => Badge(albumBadge, null, n);
        public bool AlbumNewsShown => albumBadge != null && albumBadge.activeSelf;
        public void SetStorageNews(int n) => Badge(storageBadge, storageBadgeText, n);

        static void Badge(GameObject badge, TMP_Text text, int n)
        {
            if (badge == null) return;
            badge.SetActive(n > 0);
            if (text != null) text.text = "새 " + n;
        }

        // ---------------- the hint pill ----------------
        /// <summary>Shows a line (or two) in the cream pill at the top; null hides it. With <paramref name="seconds"/> it goes away by itself.</summary>
        public void Hint(string text, float seconds = 0f)
        {
            if (hint == null) return;
            if (string.IsNullOrEmpty(text)) { hint.gameObject.SetActive(false); hintUntil = float.MaxValue; RefreshHunt(); return; }
            hintText.text = text;
            var size = hintText.GetPreferredValues(text, hintMaxWidth - 2f * hintPad, 0f);
            float w = Mathf.Clamp(size.x + 2f * hintPad, hintMinWidth, hintMaxWidth);
            hint.sizeDelta = new Vector2(w, Mathf.Max(76f, size.y + 2f * hintPadY));
            hint.gameObject.SetActive(true);
            hintUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            RefreshHunt();
        }

        public bool HintShown => hint != null && hint.gameObject.activeSelf;

        // ---------------- the star hunt ----------------
        string huntLine, huntInfoLine;
        /// <summary>The star hunt in the top row (how many, the time) and what to do under it; a hint in the same place goes first.</summary>
        public void ShowHunt(string line, string info) { huntLine = line; huntInfoLine = info; RefreshHunt(); }
        public void HideHunt() { huntLine = huntInfoLine = null; RefreshHunt(); }
        public bool HuntShown => hunt != null && hunt.gameObject.activeSelf;
        public string HuntText => HuntShown ? huntText.text : "";
        public bool HuntInfoShown => huntInfo != null && huntInfo.gameObject.activeSelf;

        void RefreshHunt()
        {
            if (hunt == null) return;
            bool on = !string.IsNullOrEmpty(huntLine) && !HintShown;
            if (hunt.gameObject.activeSelf != on) hunt.gameObject.SetActive(on);
            bool infoOn = on && huntInfo != null && !string.IsNullOrEmpty(huntInfoLine);
            if (huntInfo != null && huntInfo.gameObject.activeSelf != infoOn) huntInfo.gameObject.SetActive(infoOn);
            if (!on) return;
            if (huntText.text != huntLine) { huntText.text = huntLine; hunt.sizeDelta = new Vector2(Mathf.Max(300f, huntText.GetPreferredValues(huntLine, 99999f, 0f).x + 2f * huntPad), hunt.sizeDelta.y); }
            if (infoOn && huntInfoText.text != huntInfoLine) { huntInfoText.text = huntInfoLine; huntInfo.sizeDelta = new Vector2(huntInfoText.GetPreferredValues(huntInfoLine, 99999f, 0f).x + 2f * huntInfoPad, huntInfo.sizeDelta.y); }
        }
        public string HintText => HintShown ? hintText.text : "";

        // ---------------- what is under way ----------------
        /// <summary>The lines under the 퀘스트 pill (none hides it).</summary>
        public void SetTracker(IList<string> lines)
        {
            if (tracker == null) return;
            bool any = lines != null && lines.Count > 0 && !focus && !placing;
            tracker.gameObject.SetActive(any);
            if (!any) return;
            trackerText.text = string.Join("\n", lines);
            var size = trackerText.GetPreferredValues(trackerText.text, 720f, 0f);
            tracker.sizeDelta = new Vector2(Mathf.Clamp(size.x + 60f, 220f, 780f), size.y + 30f);
        }

        public string TrackerText => tracker != null && tracker.gameObject.activeSelf ? trackerText.text : "";

        // ---------------- the tiles ----------------
        public void ShowHud(bool on) { if (top != null) top.SetActive(on); if (zoom != null) zoom.SetActive(on); ShowToolbar(toolbarWanted); }
        bool toolbarWanted = true, focus;
        public void ShowToolbar(bool on)
        {
            toolbarWanted = on;
            if (toolbar != null) toolbar.SetActive(on && !focus && (top == null || top.activeSelf));
            if (tracker != null && (!on || focus || placing)) tracker.gameObject.SetActive(false);
        }

        /// <summary>A friend's card is open (Figma 88:1912): only the home, zoom, coins and gear stay; the 퀘스트 pill, what is under
        /// way and the tiles step aside until it closes.</summary>
        public void Focus(bool on) => Focus(on, true);

        /// <summary>As <see cref="Focus(bool)"/>; without <paramref name="home"/> the home button steps aside too: a shop is open
        /// (Figma 88:1143 keeps only the coins, the gear and the zoom pair; the shop's own ◀ leads back).</summary>
        public void Focus(bool on, bool home)
        {
            focus = on;
            if (questButton != null) questButton.gameObject.SetActive(!on && !placing);
            if (homeButton != null) homeButton.gameObject.SetActive((!on || home) && !placing);
            ShowToolbar(toolbarWanted);
        }
        public bool Focused => focus;

        /// <summary>
        /// A window is open (shop, storage, quest board, album, claw machine, a friend's talk, a dialog, settings): the whole HUD
        /// steps away until it closes; only the friend card keeps it (user 2026-11-27: "다른 UI들 들어가지면 기본 UI는 꺼지는걸로
        /// … 프로필만 빼고").
        /// </summary>
        public void SetWindowOpen(bool on)
        {
            if (windowFade != null && on == WindowHidden) return;
            if (windowFade == null) { windowFade = GetComponent<CanvasGroup>(); if (windowFade == null) windowFade = gameObject.AddComponent<CanvasGroup>(); }
            WindowHidden = on;
            windowFade.alpha = on ? 0f : 1f; windowFade.blocksRaycasts = !on; windowFade.interactable = !on;
        }
        public bool WindowHidden { get; private set; }
        CanvasGroup windowFade;
        public bool HomeShown => homeButton != null && homeButton.gameObject.activeInHierarchy;
        public bool ToolbarShown => toolbar != null && toolbar.activeInHierarchy;

        /// <summary>
        /// Furniture is being moved (Figma 115:1398): the placing screen's ◀ stands where the home button was, so the home button,
        /// the 퀘스트 pill and what is under way step aside; the tiles, the zoom pair, the coins and the gear stay (no blue edges).
        /// </summary>
        public void SetPlacing(bool on)
        {
            placing = on;
            if (homeButton != null) homeButton.gameObject.SetActive(!on);
            if (questButton != null) questButton.gameObject.SetActive(!on && !focus);
            if (on && tracker != null) tracker.gameObject.SetActive(false);
            RefreshTiles();
        }
        public bool Placing => placing;

        /// <summary>All three tips (and edges) for a while: the first look at the village (Figma 88:510).</summary>
        public void ShowTips(float seconds) { tipsUntil = Time.unscaledTime + seconds; RefreshTiles(); }
        public bool TipsShown => tipsUntil > 0f;

        void RefreshTiles()
        {
            bool all = tipsUntil > 0f;
            for (int i = 0; i < 3; i++)
            {
                bool tip = all || hovered[i];
                if (i < tips.Length && tips[i] != null) tips[i].SetActive(tip);
                if (i < glows.Length && glows[i] != null) glows[i].SetActive(tip);
            }
        }

        // ---------------- the ride bar ----------------
        public void ShowRide(string text) { if (rideBar == null) return; rideText.text = text; rideBar.SetActive(true); }
        public void HideRide() { if (rideBar != null) rideBar.SetActive(false); }
        public bool RideShown => rideBar != null && rideBar.activeSelf;
        public string RideText => RideShown ? rideText.text : "";

        // ---------------- room for notices ----------------
        static readonly Vector3[] corners = new Vector3[4];

        /// <summary>
        /// Where notices may rise to while the hint pill is away: the pill's band, as wide as the room between the home button
        /// and the coin pill allows around the middle (screen pixels; nothing while the pill shows or the top row is hidden).
        /// </summary>
        public Rect FreeTopBand(float gap = 16f)
        {
            if (hint == null || HintShown || HuntShown || top == null || !top.activeInHierarchy) return Rect.zero;
            var canvas = GetComponentInParent<Canvas>(); float s = canvas != null ? canvas.scaleFactor : 1f;
            hint.GetWorldCorners(corners); float bottom = corners[0].y, topY = corners[1].y;
            float mid = Screen.width * 0.5f, room = mid;
            foreach (var t in new[] { homeButton != null ? (RectTransform)homeButton.transform : null, coins })
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                t.GetWorldCorners(corners);
                if (corners[2].x <= mid) room = Mathf.Min(room, mid - corners[2].x); else if (corners[0].x >= mid) room = Mathf.Min(room, corners[0].x - mid);
            }
            room -= gap * s;
            return room > 0f ? new Rect(mid - room, bottom, room * 2f, topY - bottom) : Rect.zero;
        }
    }
}
