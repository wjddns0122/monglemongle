using Mongle.UI2;
using Mongle.Views;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Mongle.EditorTools.ViewKit;

namespace Mongle.EditorTools
{
    /// <summary>
    /// The 11-26 photo studio (Figma "사진찍기" 88:1081) as a prefab: Assets/_UI수정/4_사진관/Photo.prefab, placed in the
    /// PhotoStudio scene. The pills, slider pieces, camera button and viewfinder marks are the Figma vectors (Art/UI_1126/Photo);
    /// the polaroid's border is the chosen frame's pattern (PhotoFrames), drawn with Figma's polaroid proportions.
    /// </summary>
    public static class PhotoBuild
    {
        public const string PrefabPath = UiEditFolder.PhotoDir + "/Photo.prefab";
        static readonly Color Pill = Hex("#F9EDDD"), Minus = Hex("#7D5D51"), Blue = Hex("#A0DCF8"), Peach = Hex("#FFB58A");
        const float ReviewX = 1490f, ReviewW = 390f;

        public static GameObject Make()
        {
            var root = Root("Photo", 10);
            var v = root.gameObject.AddComponent<PhotoView>();

            Viewfinder(root, v);

            // the hint pill: in the middle of the screen, the cream pill of Figma 115:1573 like every top line (user 2026-11-27: "사진
            // 찍고 찍었어요 할때 맨 위에 뜨는 설명글 padding이 달라 그것도 다 맞춰"): 97 tall, middle at y 97.75, 38 px, 62 in from its ends
            var banner = Node(root, "Banner", 640.5f, 49.25f, 639f, 97f, T);
            banner.pivot = new Vector2(0.5f, 0.5f); banner.anchoredPosition = new Vector2(0f, -97.75f);
            Shape(banner.gameObject, Pill, Vector4.one * 48.5f);
            var bt = Words(banner, "Text", "프레임과 포즈를 고르고 사진을 찍어요", 38f, Brown, 0f, 0f, 639f, 97f, C, HorizontalAlignmentOptions.Center, null, new Vector2(639f, 97f));
            bt.rectTransform.anchorMin = Vector2.zero; bt.rectTransform.anchorMax = Vector2.one; bt.rectTransform.offsetMin = Vector2.zero; bt.rectTransform.offsetMax = Vector2.zero;
            v.banner = banner; v.bannerText = bt; v.bannerPad = 62f; v.bannerMinWidth = 300f;

            v.gearButton = PictureButton(root, "Btn_Gear", "Common/gear.png", 1772.9f, 32f, 103f, 103f, TR);

            // the controls: frame and pose pills, the zoom slider, the camera button (hidden while a photo is shown)
            var controls = Node(root, "Controls", 0f, 0f, 1920f, 1080f, C);
            controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one; controls.offsetMin = controls.offsetMax = Vector2.zero;
            v.frameButton = PictureButton(controls, "Btn_Frame", "Photo/opt_frame.png", 24.5f, 25.5f, 428f, 132.5f, TL);
            Words(v.frameButton.transform, "Key", "F", 34.45f, Brown, 16.25f, 4.5f, 22f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(428f, 132.5f));
            v.frameLabel = Words(v.frameButton.transform, "Label", "기본", 34.45f, Brown, 125f, 51.05f, 250f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(428f, 132.5f));
            v.poseButton = PictureButton(controls, "Btn_Pose", "Photo/opt_pose.png", 24.5f, 157f, 428f, 126f, TL);
            Words(v.poseButton.transform, "Key", "P", 34.45f, Brown, 16.25f, 2.84f, 25f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(428f, 126f));
            v.poseLabel = Words(v.poseButton.transform, "Label", "활짝 웃기", 34.45f, Brown, 135f, 51.2f, 250f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(428f, 126f));
            v.zoom = ZoomSlider(controls);
            v.shutter = PictureButton(controls, "Btn_Shutter", "Photo/shutter.png", 1569f, 726f, 242f, 242.5f, BR);
            Words(v.shutter.transform, "Key", "C", 34.45f, Brown, 25.58f, 3.74f, 23f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(242f, 242.5f));
            v.controls = controls.gameObject;

            // after the shot: keep it, or take it again (the message box's pills), in the camera button's place — clear of the
            // polaroid (its right edge is at 1383) with the same 40 margin to the screen's edge as the gear's side
            var review = Node(root, "Review", ReviewX, 750.5f, ReviewW, 192.57f, BR);
            var col = review.gameObject.AddComponent<VerticalLayoutGroup>(); col.spacing = 19.43f; col.childAlignment = TextAnchor.UpperRight;
            col.childControlWidth = col.childControlHeight = false; col.childForceExpandWidth = col.childForceExpandHeight = false;
            v.keepButton = PillButton(review, "Btn_Keep", Blue, "이 사진으로 남길래요");
            v.retakeButton = PillButton(review, "Btn_Retake", Peach, "다시 찍기");
            review.gameObject.SetActive(false);
            v.review = review.gameObject;

            var fl = Node(root, "Flash", 0f, 0f, 1920f, 1080f, C);
            fl.anchorMin = Vector2.zero; fl.anchorMax = Vector2.one; fl.offsetMin = fl.offsetMax = Vector2.zero;
            v.flash = fl.gameObject.AddComponent<Image>(); v.flash.color = new Color(1f, 1f, 1f, 0f); v.flash.raycastTarget = false;
            return root.gameObject;
        }

        /// <summary>The polaroid (Figma 88:1089): 845x859, a window 731x632 at (57, 59); its border in the frame's pattern.</summary>
        static void Viewfinder(RectTransform root, PhotoView v)
        {
            var vs = new Vector2(845.37f, 858.64f);
            var vf = Node(root, "ViewFinder", 960f - vs.x / 2f, 187.31f, vs.x, vs.y, B);   // in the middle of the screen (user 2026-11-27; Figma had it 71.7 right)
            var photo = Node(vf, "Photo", 56.98f, 59.4f, 731.44f, 631.59f, TL, vs).gameObject.AddComponent<RawImage>();
            photo.raycastTarget = false; photo.enabled = false;
            v.borders = new[]
            {
                Border(vf, "Top", 0f, 0f, vs.x, 59.4f, vs), Border(vf, "Bottom", 0f, 690.99f, vs.x, 167.65f, vs),
                Border(vf, "Left", 0f, 59.4f, 56.98f, 631.59f, vs), Border(vf, "Right", 788.42f, 59.4f, 56.95f, 631.59f, vs),
            };
            v.marks = Picture(vf, "Marks", "Photo/vf_marks.png", 162.52f, 124.19f, 523.5f, 523.5f, TL, vs).gameObject;
            // the date line sits on a cream plate in the middle of the bottom band, so it reads over any pattern
            var box = Node(vf, "CaptionBox", 0f, 0f, 400f, 60f, TL, vs);
            box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f); box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(0f, (vs.y - 690.99f) * 0.5f);
            Shape(box.gameObject, Pill, Vector4.one * 30f);
            var row = box.gameObject.AddComponent<HorizontalLayoutGroup>(); row.padding = new RectOffset(34, 34, 10, 10);
            row.childAlignment = TextAnchor.MiddleCenter; row.childControlWidth = row.childControlHeight = true; row.childForceExpandWidth = row.childForceExpandHeight = false;
            var fit = box.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var cap = Words(box, "Caption", "", 34f, Brown, 0f, 0f, 300f, 45f, TL, HorizontalAlignmentOptions.Center, null, new Vector2(400f, 60f));
            box.gameObject.SetActive(false);
            v.viewfinder = vf; v.photo = photo; v.caption = cap; v.captionBox = box.gameObject;
        }

        static RawImage Border(Transform vf, string name, float x, float y, float w, float h, Vector2 vs)
        {
            var ri = Node(vf, name, x, y, w, h, TL, vs).gameObject.AddComponent<RawImage>();
            ri.raycastTarget = false; ri.color = Color.white;
            return ri;
        }

        /// <summary>The zoom slider (Figma 88:1121–1130): ＋ at the top, － at the bottom, the pink fill from the top to the knob.</summary>
        static Slider ZoomSlider(RectTransform controls)
        {
            var ps = new Vector2(86f, 511.5f);
            var panel = Picture(controls, "Zoom", "Photo/slider_panel.png", 370f, 335f, ps.x, ps.y, L);
            panel.raycastTarget = true;
            Picture(panel.transform, "Plus", "Photo/slider_plus.png", 28f, 15.5f, 29.5f, 29.5f, TL, ps);
            Words(panel.transform, "Minus", "-", 60f, Minus, 25.54f, 436.94f, 35f, 78f, TL, HorizontalAlignmentOptions.Center, null, ps);
            var track = Picture(panel.transform, "Track", "Photo/slider_track.png", 35f, 65f, 15.5f, 381.5f, TL, ps);
            track.preserveAspect = false; track.raycastTarget = true;
            var fillArea = Node(track.transform, "Fill Area", 0f, 0f, 15.5f, 381.5f, TL, new Vector2(15.5f, 381.5f));
            fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one; fillArea.offsetMin = new Vector2(-0.75f, 0f); fillArea.offsetMax = new Vector2(0.75f, 0.5f);
            var fill = Node(fillArea, "Fill", 0f, 0f, 17f, 213.5f, TL, new Vector2(17f, 381.5f)).gameObject.AddComponent<Image>();
            fill.sprite = Art("Photo/slider_fill.png"); fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 1f; fill.raycastTarget = false;
            // the slider moves the fill's anchors; its own size stays 0 so the fill is exactly the part above the knob
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one; fill.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            fill.rectTransform.sizeDelta = Vector2.zero; fill.rectTransform.anchoredPosition = Vector2.zero;
            var handleArea = Node(track.transform, "Handle Area", 0f, 0f, 15.5f, 381.5f, TL, new Vector2(15.5f, 381.5f));
            handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one; handleArea.offsetMin = handleArea.offsetMax = Vector2.zero;
            var knob = Node(handleArea, "Knob", 0f, 0f, 44f, 43.5f, C, new Vector2(15.5f, 381.5f)).gameObject.AddComponent<Image>();
            knob.sprite = Art("Photo/slider_knob.png"); knob.preserveAspect = true;
            var s = track.gameObject.AddComponent<Slider>();
            s.transition = Selectable.Transition.None; s.targetGraphic = knob;
            s.fillRect = fill.rectTransform; s.handleRect = knob.rectTransform;
            s.direction = Slider.Direction.TopToBottom; s.minValue = 0f; s.maxValue = 1f; s.value = 0.5f;
            knob.rectTransform.sizeDelta = new Vector2(44f - 15.5f, 43.5f);   // the knob is wider than the track (the slider stretches it across)
            knob.rectTransform.anchoredPosition = Vector2.zero;   // the slider moves only the anchors, so the knob sits right on the value
            return s;
        }

        static Button PillButton(Transform parent, string name, Color fill, string words)
        {
            var rt = Node(parent, name, 0f, 0f, ReviewW, 86.57f, TL, new Vector2(ReviewW, 86.57f));
            var s = Shape(rt.gameObject, fill, Vector4.one * 43.285f); s.raycastTarget = true;
            var b = rt.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None; b.targetGraphic = s;
            rt.gameObject.AddComponent<PressScale>();
            var label = Words(rt, "Label", words, 34f, Brown, 0f, 0f, ReviewW, 45f, C, HorizontalAlignmentOptions.Center, null, new Vector2(ReviewW, 86.57f));
            label.rectTransform.anchoredPosition = new Vector2(0f, 3.2f);
            return b;
        }

        public static GameObject Build()
        {
            var go = Make();
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Register("Photo", prefab);
                return prefab;
            }
            finally { Object.DestroyImmediate(go); }
        }

        public static void AddTo(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = "Photo";
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
