using System;
using System.Collections;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Photo
{
    /// <summary>PhotoStudio scene: pose, capture, review, keep the first photo (D-001-7) — Mongi's, or a hospital friend's before it moves in.</summary>
    public class PhotoDirector : MonoBehaviour
    {
        public const string BackgroundName = "Garden";
        public static PhotoDirector Current { get; private set; }
        public ToyActor Toy { get; private set; }
        public string Pose { get; private set; } = "Idle";
        public bool Capturing { get; private set; }
        public Texture2D LastShot { get; private set; }
        GameRoot root; ToyRuntime state; ContentCatalog catalog; ToyDefinition def;
        bool Friend => root.Patient != null && state == root.Patient;
        string ToyName => def != null ? def.displayName : "몽이";
        Camera cam; ShopUI ui; PhotoPanel panel; Canvas canvas;
        Image flash; Text banner; GameObject controls; GameObject frameGroup;
        const int Size = 1024;

        void OnDestroy() { if (Current == this) Current = null; }

        IEnumerator Start()
        {
            Current = this;
            while ((root = GameRoot.Instance) == null) yield return null;
            catalog = root.Catalog; state = root.ActiveToy;
            if (state == null || root.CurrentPhase != FlowPhase.PhotoStudio) { root.GoToCurrentPhase(); yield break; }
            def = catalog.ToyDef(state.KindId) ?? catalog.toy;
            cam = Camera.main;
            Toy = ToyActor.Spawn(catalog, def, Vector3.zero, Quaternion.identity, false);
            Toy.ApplyRepairState(state);
            Toy.SetAccessories(state.Equipped.ToDictionary(k => k.Key, v => v.Value), catalog);
            Toy.Play("Idle", 0.1f);
            BuildUi();
            root.Pointer.Mode = PointerMode.None;
            root.Audio.StartBgm();
            SetPose("Happy");
        }

        int poseIndex; Text poseLabel;
        int frameIndex; Text frameLabel; readonly System.Collections.Generic.List<RawImage> frameBars = new System.Collections.Generic.List<RawImage>();
        public string FrameId => PhotoFrames.Ids[frameIndex];

        /// <summary>One side of the viewfinder frame: a RawImage that tiles the frame pattern over its size.</summary>
        void FrameBar(string name, Vector2 pos, Vector2 size)
        {
            var rt = UiKit.Fixed(frameGroup.transform, name, new Vector2(0.5f, 0.5f), pos, size);
            var ri = rt.gameObject.AddComponent<RawImage>(); ri.raycastTarget = false;
            frameBars.Add(ri);
        }

        void ApplyFrame()
        {
            var tex = PhotoFrames.Pattern(frameIndex);
            foreach (var ri in frameBars)
            {
                var sz = ((RectTransform)ri.transform).sizeDelta;
                ri.texture = tex; ri.uvRect = new Rect(0f, 0f, sz.x / PhotoFrames.Tile, sz.y / PhotoFrames.Tile);
            }
            if (frameLabel != null) frameLabel.text = PhotoFrames.Names[frameIndex];   // inside the mint circle, like the pose in 동작
        }

        void BuildUi()
        {
            canvas = UiKit.CreateCanvas("PhotoUI", 10, transform);
            UiKit.HiddenLabel(UiKit.FigButton(canvas.transform, "hud_settings", Mongle.UI2.UiFront.OpenSettings, UiKit.Edge.End, UiKit.Edge.Start, 0f, 0f, "Btn_설정"), "설정");   // D-023
            // viewfinder: a polaroid frame drawn with the chosen pattern (D-025: plain cream / dots / gingham / stars)
            frameGroup = UiKit.Rect(canvas.transform, "ViewFinder", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            const float h = 760f;
            frameBars.Clear();
            FrameBar("Top", new Vector2(0, h / 2 + 20), new Vector2(h + 80, 40));
            FrameBar("Bottom", new Vector2(0, -h / 2 - 50), new Vector2(h + 80, 100));
            FrameBar("Left", new Vector2(-h / 2 - 20, -15), new Vector2(40, h + 110));
            FrameBar("Right", new Vector2(h / 2 + 20, -15), new Vector2(40, h + 110));
            var cap = UiKit.Panel(frameGroup.transform, "Caption", new Color(1f, 0.99f, 0.95f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0, -h / 2 - 50), new Vector2(h - 40, 70), false);
            UiKit.NoStitch(cap);
            banner = UiKit.Label(cap.transform, "포즈를 고르고 사진을 찍어요", 34, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(h - 60, 66));
            ApplyFrame();
            // controls
            controls = UiKit.Rect(canvas.transform, "Controls", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            // Figma D-029 (아트보드 9 새 파일): the three round buttons on a half-clear cream board at the right edge;
            // 동작 steps through the poses, 프레임 through the frames (each shows its choice inside), the big pink one takes the picture
            UiKit.Fig(controls.transform, "ph2_panel", UiKit.Edge.End, UiKit.Edge.Middle, 0f, 0f, false, "Board");
            string[] poses = { "Idle", "Happy", "Wave" }; poseIndex = 1;
            var motion = UiKit.FigButton(controls.transform, "ph2_circle_motion", () => { poseIndex = (poseIndex + 1) % poses.Length; SetPose(poses[poseIndex]); poseLabel.text = PoseKorean(poses[poseIndex]); }, UiKit.Edge.End, UiKit.Edge.Middle, 0f, 0f, "Btn_동작");
            poseLabel = UiKit.FigTextAt(motion.transform, "ph2_circle_motion", PoseKorean(poses[poseIndex]), 30, UiKit.FigBrown2, 1714.8f, 344.1f, 136f, 60f);
            UiKit.FigTextAt(motion.transform, "ph2_circle_motion", "동작", 18, UiKit.FigBrown2, 1711.9f, 433f, 100f, 28f);
            var frameBtn = UiKit.FigButton(controls.transform, "ph2_circle_frame", () => { frameIndex = (frameIndex + 1) % PhotoFrames.Ids.Length; ApplyFrame(); root.Audio.Play("click", 0.5f, 1.1f); }, UiKit.Edge.End, UiKit.Edge.Middle, 0f, 0f, "Btn_프레임");
            frameLabel = UiKit.FigTextAt(frameBtn.transform, "ph2_circle_frame", "", 30, UiKit.FigBrown2, 1714.8f, 529.1f, 136f, 60f);
            UiKit.FigTextAt(frameBtn.transform, "ph2_circle_frame", "프레임", 18, UiKit.FigBrown2, 1712.4f, 624f, 100f, 28f);
            ApplyFrame();
            var shutter = UiKit.FigButton(controls.transform, "ph2_circle_shutter", () => Capture(), UiKit.Edge.End, UiKit.Edge.Middle, 0f, 0f, "Btn_찰칵");
            UiKit.FigTextAt(shutter.transform, "ph2_circle_shutter", "찰칵!", 18, UiKit.FigBrown2, 1716.3f, 902f, 100f, 28f);
            UiKit.HiddenLabel(shutter, "찰칵!");
            panel = new PhotoPanel(canvas.transform, "이 사진으로 남길래요", "다시 찍기");
            panel.Primary.onClick.AddListener(() => KeepPhoto());
            panel.Secondary.onClick.AddListener(() => { panel.Hide(); controls.SetActive(true); frameGroup.SetActive(true); });
            var fr = UiKit.Rect(canvas.transform, "Flash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            flash = fr.gameObject.AddComponent<Image>(); flash.color = new Color(1, 1, 1, 0); flash.raycastTarget = false;
            UiSkin.Apply(canvas.transform, SkinName);   // the photo screen's prefab, edited in Unity (Assets/_UI수정, D-035)
            Mongle.UI2.UiFront.AttachFor(canvas.transform, SkinName);   // D-040: the Figma photo screen in front
        }

        /// <summary>D-035: the editable prefab of the photo screen.</summary>
        public const string SkinName = "PhotoScreen";
#if UNITY_EDITOR
        /// <summary>The photo screen with sample contents, for its editable prefab (UiSkinBaker); the photo sheet is switched off.</summary>
        public GameObject BuildSkinSample()
        {
            BuildUi();
            panel.Show(null, "2026-03-02 · 몽이 · 활짝 웃기"); panel.Hide();
            return canvas.gameObject;
        }
#endif

        public void SetPose(string pose)
        {
            // every press starts the pose over from its first frame (pressing the pose that is showing used to do nothing)
            Pose = pose;
            if (!Toy.Restart(pose, 0.15f)) Toy.Play(pose, 0.15f);
            root.Audio.Play("click", 0.5f, pose == "Wave" ? 1.3f : 1f);
            if (pose == "Happy") Toy.SetFace(1f, 0f);
            else if (pose == "Wave") Toy.SetFace(0.8f, 0f);
            else Toy.SetFace(0.5f, 0f);
        }

        public void Capture() { if (!Capturing) StartCoroutine(CaptureRoutine()); }

        IEnumerator CaptureRoutine()
        {
            Capturing = true;
            controls.SetActive(false);
            root.Audio.Play("shutter", 1f);
            float t = 0; while (t < 0.08f) { t += Time.deltaTime; flash.color = new Color(1, 1, 1, t / 0.08f); yield return null; }
            yield return new WaitForEndOfFrame();
            LastShot = Render();
            PhotoFrames.Bake(LastShot, frameIndex, Size / 16);   // the chosen frame goes into the picture (D-025)
            t = 0; while (t < 0.3f) { t += Time.deltaTime; flash.color = new Color(1, 1, 1, 1 - t / 0.3f); yield return null; }
            flash.color = new Color(1, 1, 1, 0);
            frameGroup.SetActive(false);
            panel.Show(LastShot, DateTime.Now.ToString("yyyy-MM-dd") + " · " + ToyName + " · " + PoseKorean(Pose));
            Capturing = false;
        }

        public static string PoseKorean(string p) => p == "Happy" ? "활짝 웃기" : p == "Wave" ? "손 흔들기" : "가만히";

        /// <summary>Renders the studio camera to a square texture (UI is an overlay and is not included).</summary>
        Texture2D Render()
        {
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("CaptureCam"); var c = go.AddComponent<Camera>();
            c.CopyFrom(cam); c.targetTexture = rt; c.aspect = 1f; c.fieldOfView = 24f;
            var focus = new Vector3(0, 0.235f, 0);
            c.transform.position = focus + new Vector3(0, 0.02f, 1.35f); c.transform.LookAt(focus);
            c.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            Destroy(go);
            return tex;
        }

        public bool KeepPhoto()
        {
            if (LastShot == null || state.PhotoConfirmed) return false;
            var rec = new PhotoRecord
            {
                photoId = Guid.NewGuid().ToString("N"),
                residentId = state.ResidentId,
                pose = Pose, background = BackgroundName,
                imageFile = Friend ? PhotoStore.FileFor(state.ResidentId) : PhotoStore.FirstPhotoFile,
                takenAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                accessoryIds = state.Equipped.Values.ToList()
            };
            PhotoStore.Write(root.Saves, rec.imageFile, LastShot.EncodeToPNG());
            if (!state.ConfirmPhoto(rec)) return false;
            root.Audio.Play("success", 0.9f);
            panel.Hide();
            frameGroup.SetActive(true);
            if (Friend)
            {
                // the friend's photo is in the album; now it moves into the village (D-005)
                banner.text = Josa.EulReul(ToyName + "의 첫 사진") + " 앨범에 넣었어요! 이제 마을로 이사 가요";
                root.MovePatientIn();
                StartCoroutine(Next(SceneNames.Village));
                return true;
            }
            banner.text = "첫 사진을 간직했어요!";
            StartCoroutine(Next(null));
            return true;
        }

        IEnumerator Next(string scene)
        {
            yield return new WaitForSeconds(1.8f);
            if (scene != null) root.GoToScene(scene); else root.GoToCurrentPhase();
        }
    }
}
