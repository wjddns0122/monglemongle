using System;
using System.Collections;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using Mongle.Views;
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
        Camera cam; PhotoView view;
        // framing (PhotoAim): where the camera looks, how wide the viewfinder's window sees at the middle zoom, the zoom
        Vector3 focus = new Vector3(0f, 0.235f, 0f);
        float windowWidth = 0.76f, zoom = 1f;
        const int Size = 1024;
        const string Hint = "프레임과 포즈를 고르고 사진을 찍어요";

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
            FrameToy();
            root.Pointer.Mode = PointerMode.None;
            root.Audio.StartBgm();
            SetPose("Happy");
        }

        int poseIndex;
        int frameIndex;
        public string FrameId => PhotoFrames.Ids[frameIndex];
        static readonly string[] Poses = { "Idle", "Happy", "Wave" };

        void ApplyFrame() { if (view != null) view.SetFrame(PhotoFrames.Pattern(frameIndex), PhotoFrames.Names[frameIndex]); }

        /// <summary>The 11-26 photo screen (a PhotoStudio scene object, Assets/_UI수정/4_사진관/Photo.prefab): the game only listens to it.</summary>
        void BuildUi()
        {
            view = FindAnyObjectByType<PhotoView>(FindObjectsInactive.Include);
            if (view == null) { var prefab = ViewIndex.Prefab("Photo"); if (prefab != null) view = Instantiate(prefab).GetComponent<PhotoView>(); }
            view.gameObject.SetActive(true);
            poseIndex = 1;
            view.OnPose = () => { poseIndex = (poseIndex + 1) % Poses.Length; SetPose(Poses[poseIndex]); view.SetPose(PoseKorean(Poses[poseIndex])); };
            view.OnFrame = () => { frameIndex = (frameIndex + 1) % PhotoFrames.Ids.Length; ApplyFrame(); root.Audio.Play("click", 0.5f, 1.1f); };
            view.OnShoot = Capture;
            view.OnKeep = () => KeepPhoto();
            view.OnRetake = () => { view.HideReview(); view.ShowControls(true); view.Banner(Hint); };
            // the slider: ＋ (top) brings the toy closer, － (bottom) steps back; the photo is taken with the same zoom
            view.OnZoom = v => zoom = Mathf.Lerp(PhotoAim.ZoomNear, PhotoAim.ZoomFar, v);
            view.SetPose(PoseKorean(Poses[poseIndex]));
            ApplyFrame();
            view.HideReview(); view.ShowControls(true); view.ShowViewfinder(true); view.Banner(Hint); view.Flash(0f);
        }

        /// <summary>Looks at the middle of this toy from the studio's distance, with a view sized from it, so every friend
        /// sits in the middle of the polaroid's window and fills the same share of it.</summary>
        void FrameToy()
        {
            var b = PhotoAim.Measure(Toy.gameObject);
            focus = new Vector3(b.center.x, b.center.y, 0f);
            windowWidth = PhotoAim.WindowWidth(b.size, view.WindowAspect);
            cam.transform.position = focus + new Vector3(0f, 0.02f, PhotoAim.Distance);
            cam.transform.LookAt(focus);
            Aim();
        }

        void LateUpdate() { if (view != null && cam != null && Toy != null) Aim(); }

        /// <summary>Every frame (the window moves with the screen's shape): the toy lands in the window's middle, and the
        /// window shows exactly what the photo keeps.</summary>
        void Aim()
        {
            var w = view.WindowViewport();
            if (w.width < 0.01f || w.height < 0.01f) return;   // the screen is not laid out yet
            cam.fieldOfView = PhotoAim.LiveFov(windowWidth, zoom, w.width * cam.aspect);
            cam.ResetProjectionMatrix();
            cam.projectionMatrix = PhotoAim.Shift(cam.projectionMatrix, w.center);
        }

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
            view.ShowControls(false);
            root.Audio.Play("shutter", 1f);
            float t = 0; while (t < 0.08f) { t += Time.deltaTime; view.Flash(t / 0.08f); yield return null; }
            yield return new WaitForEndOfFrame();
            LastShot = Render();
            PhotoFrames.Bake(LastShot, frameIndex, Size / 16);   // the chosen frame goes into the picture (D-025)
            t = 0; while (t < 0.3f) { t += Time.deltaTime; view.Flash(1 - t / 0.3f); yield return null; }
            view.Flash(0f);
            view.ShowReview(LastShot, null, PhotoAim.Border);   // no date / name / pose line under the photo (user 2026-11-26)
            view.Banner("마음에 들면 남겨요");
            Capturing = false;
        }

        public static string PoseKorean(string p) => p == "Happy" ? "활짝 웃기" : p == "Wave" ? "손 흔들기" : "가만히";

        /// <summary>Renders the studio camera's view to a square texture (UI is an overlay and is not included): looking straight
        /// at the toy, its inside as wide as the viewfinder's window (PhotoAim), so the photo is what the window showed.</summary>
        Texture2D Render()
        {
            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var go = new GameObject("CaptureCam"); var c = go.AddComponent<Camera>();
            c.CopyFrom(cam); c.ResetProjectionMatrix();   // the studio camera's sideways shift is only for the screen
            c.targetTexture = rt; c.aspect = 1f; c.fieldOfView = PhotoAim.CaptureFov(windowWidth, zoom);
            c.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
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
            if (Friend)
            {
                // the friend's photo is in the album; now it moves into the village (D-005)
                view.Banner(Josa.EulReul(ToyName + "의 첫 사진") + " 앨범에 넣었어요! 이제 마을로 이사 가요");
                root.MovePatientIn();
                StartCoroutine(Next(SceneNames.Village));
                return true;
            }
            view.Banner("첫 사진을 간직했어요!");
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
