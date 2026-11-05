using System.Collections;
using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>Thorn removal: grab the thorn (pebble, twig) with tweezers, pull it away from where it is stuck, last moment goes POP.</summary>
    public class ThornMiniGame : RepairMiniGame
    {
        ThornModel model;
        Transform thorn; Vector3 embedPos; Vector3 removalDirWorld; Vector3 thornStartLocalPos;
        Vector3 grabWorldStart; Vector3 thornRest;
        GameObject ring;
        bool grabSound;

        public string DebugInfo => model == null ? "no model" : $"grabbed={model.Grabbed} pull={model.Pull:F3} grab={model.GrabPoint.ToString("F3")} dir={model.RemovalDir.ToString("F2")} ptr={LastPointer.ToString("F3")} dist={(LastPointer - model.GrabPoint).magnitude:F3}";

        public override float Progress => model != null ? model.Progress : 0f;
        public override string Instruction => model != null && model.Grabbed ? "천천히 바깥쪽으로 당겨요" : "핀셋으로 가시를 꾹 잡아요";
        public override Vector3 HintPoint => model != null ? model.GrabPoint : Ctx.toy.Point(GrabName);
        bool Site => Ctx.def != null && Ctx.def.HasSite;
        string GrabName => Site && !string.IsNullOrEmpty(Ctx.def.grabPointName) ? Ctx.def.grabPointName : "GP_THORN";

        protected override void OnBegin()
        {
            var toy = Ctx.toy;
            var dp = toy.Point(Ctx.def != null ? Ctx.def.damagePointName : "DP_EAR_L", toy.Point("DP_EAR_L"));
            // Mongi: the thorn is pulled away from the ear's center (the skinned thorn node itself is not where it is drawn); a site thorn from its DP
            embedPos = Site ? dp : toy.Point("Ear_L", toy.Point("DP_EAR_L"));
            thorn = toy.BeginThornProxy(Ctx.def);
            // grab at the thorn body itself (the visible mesh), pull away from where it is embedded
            var mf = thorn != null ? thorn.GetComponent<MeshFilter>() : null;
            var gp = toy.Node(GrabName);
            var grab = mf != null && mf.sharedMesh != null ? thorn.TransformPoint(mf.sharedMesh.bounds.center) : gp != null ? gp.position : embedPos + Vector3.up * 0.03f;
            var dir = Site && gp != null ? gp.position - dp : grab - embedPos; if (dir.sqrMagnitude < 1e-8f) dir = Vector3.up + Vector3.right;
            if (Site)
            {
                // the hurt part bends gently toward the pull (a body bone only a little)
                string b = Ctx.def.boneName ?? "";
                toy.pullBone = toy.Node(b); toy.pullTip = gp;
                toy.pullBend = b.StartsWith("Ear") ? 0.45f : b == "Spine" || b == "Hips" || b == "Root" ? 0.08f : b == "Head" ? 0.15f : 0.25f;
            }
            removalDirWorld = dir.normalized;
            // The user can only move the pointer in the camera-facing plane: validate against that projection.
            var plane = Vector3.ProjectOnPlane(removalDirWorld, Ctx.cam.transform.forward);
            var validDir = plane.magnitude > 0.35f ? plane.normalized : Ctx.cam.transform.right;
            model = new ThornModel(grab, validDir) { GrabRadius = 0.045f, MinHold = 0.15f, RequiredPull = 0.065f, MaxAngleDeg = 60f };
            if (thorn != null) { thornStartLocalPos = thorn.localPosition; thornRest = thorn.position; }
            if (Ctx.toy.Animator != null) Ctx.toy.Animator.speed = 0f; // hold still while the thorn is being grabbed
            if (Ctx.tool != null) Ctx.tool.SetPinch(0f);
            ring = Fx.Ring(Ctx.catalog, grab, 0.03f, new Color(1f, 0.85f, 0.4f, 0.9f));
            ring.transform.SetParent(thorn != null ? thorn : toy.NodeOr(Site ? Ctx.def.boneName : "Ear_L", toy.transform), true);
        }

        Vector3 ThornCenter()
        {
            var mf = thorn != null ? thorn.GetComponent<MeshFilter>() : null;
            return mf != null && mf.sharedMesh != null ? thorn.TransformPoint(mf.sharedMesh.bounds.center) : model.GrabPoint;
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model.Complete) return;
            if (!model.Grabbed) model.GrabPoint = ThornCenter();
            if (f.pressed)
            {
                if (model.TryGrab(p)) { Ctx.sfx.Play("grab", 0.7f); Ctx.tool?.SetPinch(1f); if (ring != null) ring.SetActive(false); grabWorldStart = p; }
                else { Ctx.sfx.Play("wrong", 0.35f, 1.2f); }
            }
            if (f.held && model.Grabbed)
            {
                float before = model.Progress;
                bool done = model.Drag(p, Time.deltaTime);
                ApplyVisual();
                if (!grabSound && model.Progress > 0.05f) grabSound = true;
                if (model.Progress > before + 0.001f && Random.value < 0.08f) Ctx.sfx.Play("stitch", 0.18f, 0.8f + model.Progress * 0.6f);
                if (done) { StartCoroutine(PopRoutine()); }
            }
            if (f.released && model.Grabbed && !model.Complete)
            {
                model.Release();
                Ctx.tool?.SetPinch(0f);
                Ctx.sfx.Play("wrong", 0.3f, 1.4f);
                ApplyVisual();
                if (ring != null) ring.SetActive(true);
            }
        }

        void Update2() { }

        void ApplyVisual()
        {
            float pr = model.Progress;
            if (thorn != null) thorn.position = thornRest + removalDirWorld * (pr * 0.05f);
            Ctx.toy.earPull = pr;
            Ctx.toy.earPullDir = removalDirWorld;
            if (pr > 0.01f) Ctx.toy.SetFace(0, 1f);
        }

        IEnumerator PopRoutine()
        {
            var toy = Ctx.toy;
            if (toy.Animator != null) toy.Animator.speed = 1f;
            Ctx.tool?.SetPinch(1f);
            Ctx.sfx.Play("pop", 1f);
            Ctx.orbit.Punch(0.012f);
            Fx.Burst(Ctx.catalog, thorn != null ? thorn.position : model.GrabPoint, new Color(1f, 0.9f, 0.5f), 22, 0.014f, 0.35f, 0.7f, 0.2f);
            toy.PlayThen("Relief", "Idle");
            Finish(); // state change + save happen in RepairSession; visuals continue here
            // fling thorn away, ear bounces back
            if (thorn != null)
            {
                var t = thorn; t.SetParent(null, true);
                var v = (removalDirWorld * 0.35f + Vector3.up * 0.55f + Ctx.cam.transform.forward * -0.2f);
                float life = 0f; var start = t.localScale;
                while (life < 0.7f && t != null)
                {
                    life += Time.deltaTime; v += Vector3.down * 1.6f * Time.deltaTime;
                    t.position += v * Time.deltaTime; t.Rotate(0, 540 * Time.deltaTime, 360 * Time.deltaTime);
                    t.localScale = start * Mathf.Clamp01(1f - life / 0.7f);
                    toy.earPull = Mathf.Lerp(toy.earPull, 0f, 0.25f);
                    yield return null;
                }
                if (t != null) Destroy(t.gameObject);
            }
            toy.earPull = 0f;
        }

        protected override void OnCancel()
        {
            if (Ctx.toy.Animator != null) Ctx.toy.Animator.speed = 1f;
            Ctx.toy.EndThornProxy(true);
            Ctx.toy.earPull = 0f; Ctx.toy.pullBone = null;
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished() { if (ring != null) Destroy(ring); }
    }
}
