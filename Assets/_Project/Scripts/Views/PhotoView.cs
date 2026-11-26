using System;
using Mongle.UI2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Views
{
    /// <summary>
    /// The photo studio (Figma 11-26 "사진찍기" 88:1081): the frame (F) and pose (P) pills at the top left, a zoom slider (＋ at the
    /// top), the hint banner, the polaroid viewfinder (its border in the chosen frame's pattern, corner marks and a centre cross)
    /// and the camera button (C). After the shot the photo sits in the polaroid with its date line, and two pills ask to keep
    /// it or take it again. Keys F / P / C do the same as the buttons. Scene object of PhotoStudio
    /// (Assets/_UI수정/4_사진관/Photo.prefab); the game only listens to it (PhotoDirector).
    /// </summary>
    public class PhotoView : MonoBehaviour
    {
        [Header("고르기")]
        public Button frameButton, poseButton;
        public TMP_Text frameLabel, poseLabel;
        public Slider zoom;
        public Button shutter;
        public GameObject controls;
        [Header("안내")]
        public RectTransform banner;
        public TMP_Text bannerText;
        [Tooltip("배너의 가장 작은 폭")] public float bannerMinWidth = 630.46f;
        [Tooltip("배너 글자 양옆 여백")] public float bannerPad = 165f;
        [Header("뷰파인더 (폴라로이드)")]
        public RectTransform viewfinder;
        [Tooltip("테두리 네 조각 (위·아래·왼쪽·오른쪽)")] public RawImage[] borders = new RawImage[0];
        [Tooltip("무늬 그림 한 픽셀의 화면 크기 (px) — 클수록 무늬가 커져요")] public float texelSize = 1.875f;
        public GameObject marks;
        public RawImage photo;
        public TMP_Text caption;
        [Tooltip("글줄 뒤 크림색 받침 (무늬 위에서도 읽히게)")] public GameObject captionBox;
        [Header("찍은 뒤")]
        public GameObject review;
        public Button keepButton, retakeButton;
        public Image flash;
        [Tooltip("오른쪽 위 톱니: 설정 창")] public Button gearButton;

        public Action OnFrame, OnPose, OnShoot, OnKeep, OnRetake;
        /// <summary>The slider, 0 (＋, closest) … 1 (－, farthest).</summary>
        public Action<float> OnZoom;

        void Awake()
        {
            if (frameButton != null) frameButton.onClick.AddListener(() => OnFrame?.Invoke());
            if (poseButton != null) poseButton.onClick.AddListener(() => OnPose?.Invoke());
            if (shutter != null) shutter.onClick.AddListener(() => OnShoot?.Invoke());
            if (keepButton != null) keepButton.onClick.AddListener(() => OnKeep?.Invoke());
            if (retakeButton != null) retakeButton.onClick.AddListener(() => OnRetake?.Invoke());
            if (zoom != null) zoom.onValueChanged.AddListener(v => OnZoom?.Invoke(v));
            if (gearButton != null) gearButton.onClick.AddListener(UiFront.OpenSettings);
            ViewUtil.EnsureEventSystem();
        }

        void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || controls == null || !controls.activeInHierarchy) return;
            if (kb.fKey.wasPressedThisFrame) OnFrame?.Invoke();
            if (kb.pKey.wasPressedThisFrame) OnPose?.Invoke();
            if (kb.cKey.wasPressedThisFrame) OnShoot?.Invoke();
        }

        /// <summary>The chosen frame: its pattern tiles over the polaroid's border, its name on the F pill.</summary>
        public void SetFrame(Texture pattern, string name)
        {
            // every piece samples the same pattern laid over the whole polaroid, so the tiles run on across the corners
            var tile = pattern != null ? new Vector2(pattern.width, pattern.height) * texelSize : Vector2.one * 180f;
            foreach (var b in borders)
            {
                if (b == null) continue;
                var r = b.rectTransform.rect; var p = (Vector2)b.rectTransform.localPosition;
                b.texture = pattern;
                b.uvRect = new Rect((p.x + r.xMin) / tile.x, (p.y + r.yMin) / tile.y, r.width / tile.x, r.height / tile.y);
            }
            if (frameLabel != null) frameLabel.text = name;
        }

        public void SetPose(string name) { if (poseLabel != null) poseLabel.text = name; }

        public void Banner(string text)
        {
            if (banner == null) return;
            if (string.IsNullOrEmpty(text)) { banner.gameObject.SetActive(false); return; }
            bannerText.text = text;
            banner.sizeDelta = new Vector2(Mathf.Max(bannerMinWidth, bannerText.GetPreferredValues(text, 99999f, 0f).x + bannerPad * 2f), banner.sizeDelta.y);
            banner.gameObject.SetActive(true);
        }
        public string BannerText => bannerText != null ? bannerText.text : "";

        public void ShowControls(bool on) { if (controls != null) controls.SetActive(on); }
        public void ShowViewfinder(bool on) { if (viewfinder != null) viewfinder.gameObject.SetActive(on); }

        static readonly Vector3[] corners = new Vector3[4];

        /// <summary>The polaroid's window: width over height.</summary>
        public float WindowAspect
        {
            get { if (photo == null) return 1f; var r = photo.rectTransform.rect; return r.height > 0f ? r.width / r.height : 1f; }
        }

        /// <summary>Where the polaroid's window is on the screen (viewport, 0..1 from the bottom left), so the studio camera
        /// can put the toy in its middle.</summary>
        public Rect WindowViewport()
        {
            if (photo == null) return new Rect(0f, 0f, 1f, 1f);
            var canvas = GetComponentInParent<Canvas>();
            var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            float sw = cam != null ? cam.pixelWidth : Screen.width, sh = cam != null ? cam.pixelHeight : Screen.height;
            if (sw <= 0f || sh <= 0f) return new Rect(0f, 0f, 0f, 0f);
            photo.rectTransform.GetWorldCorners(corners);
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, corners[0]), b = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(a.x / sw, a.y / sh, b.x / sw, b.y / sh);
        }

        /// <summary>The square photo in the polaroid's window, its line underneath, and the keep / again pills. The window shows
        /// the photo's inside cut to its own shape: <paramref name="border"/> is the share of the photo on each side that is
        /// the baked frame (the polaroid already draws that pattern around the window).</summary>
        public void ShowReview(Texture shot, string line, float border = 0f)
        {
            if (photo != null)
            {
                photo.texture = shot; photo.enabled = shot != null;
                float aspect = WindowAspect, inside = 1f - 2f * Mathf.Clamp(border, 0f, 0.45f);
                float h = inside * Mathf.Min(1f, 1f / Mathf.Max(0.01f, aspect)), w = inside * Mathf.Min(1f, aspect);
                photo.uvRect = new Rect((1f - w) * 0.5f, (1f - h) * 0.5f, w, h);
            }
            if (caption != null) { caption.text = line ?? ""; CaptionBox.SetActive(!string.IsNullOrEmpty(line)); }
            if (marks != null) marks.SetActive(false);
            ShowControls(false);
            if (review != null) review.SetActive(true);
        }

        public void HideReview()
        {
            if (photo != null) { photo.enabled = false; photo.texture = null; }
            if (caption != null) CaptionBox.SetActive(false);
            if (marks != null) marks.SetActive(true);
            if (review != null) review.SetActive(false);
        }

        GameObject CaptionBox => captionBox != null ? captionBox : caption.gameObject;

        public bool Reviewing => review != null && review.activeSelf;

        public void Flash(float a) { if (flash != null) flash.color = new Color(1f, 1f, 1f, a); }
    }
}
