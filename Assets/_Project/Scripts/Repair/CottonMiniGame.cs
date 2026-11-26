using System.Collections;
using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Cotton fill: hold the injector on the opening; the body slowly plumps up. No overfill failure.
    /// D-037: pressing near the opening seats the injector — it tilts and its nozzle slides into the hole at an angle — and
    /// while held the plunger goes down and the cotton in the barrel runs out as the body fills; letting go pulls it back out
    /// (progress is kept). The rules stay in CottonModel (pointer within Radius of the fill point while held).
    /// Mongi fills the whole body through the button flap; a site damage fills one part through its hole FP_&lt;site&gt; (D-009).
    /// </summary>
    public class CottonMiniGame : RepairMiniGame
    {
        CottonModel model;
        GameObject ring;
        float jiggle, outTimer, fluffTimer;
        bool Site => Ctx.def != null && Ctx.def.HasSite;
        string FillName => Site && !string.IsNullOrEmpty(Ctx.def.workPointName) ? Ctx.def.workPointName : "FP_FILL";

        enum Dock { Free, Going, In, Leaving }
        Dock dock; bool completing; Coroutine dockCo;
        Transform plunger, cotton, holeBone; Vector3 plungerRest, cottonScale, holeNormalLocal = Vector3.up;
        const float Travel = 0.052f, CottonLeft = 0.12f, Depth = 0.011f, Tilt = 45f;
        /// <summary>+1/-1: which side of the injector has the window onto the cotton (it is turned toward the camera).</summary>
        const float WindowSide = 1f;

        public override float Progress => model != null ? model.Progress : 0f;
        public override string Instruction => model != null && model.Progress > 0f ? "그대로 꾹 눌러서 솜을 채워요" : Site ? "솜이 빠진 구멍에 대고 꾹 눌러요" : "단추 덮개 위에서 꾹 눌러요";
        public override Vector3 HintPoint => model != null ? model.FillPoint : Ctx.toy.Point(FillName);

        protected override void OnBegin()
        {
            var toy = Ctx.toy;
            var fp = toy.Point(FillName, Site ? toy.Point(Ctx.def.damagePointName, toy.transform.position + Vector3.up * 0.2f) : toy.transform.position + new Vector3(0, 0.09f, -0.1f));
            model = new CottonModel(fp) { Radius = 0.055f, HoldSeconds = 4.2f };
            if (!Site) toy.SetFillOpen(true); // the little button flap opens
            Ctx.sfx.Play("click", 0.8f, 0.8f);
            holeBone = toy.NodeOr(Site ? Ctx.def.boneName : "Hips", toy.transform);
            // the skin normal at the opening, kept in the bone's space so it turns with the body
            var g = fp - holeBone.position; g = g.sqrMagnitude > 1e-8f ? g.normalized : -Ctx.cam.transform.forward;
            var n = g;
            if (Ctx.probe != null && Ctx.probe.Raycast(new Ray(fp + g * 0.06f, -g), out _, out var hn, 0.12f)) n = hn;
            holeNormalLocal = holeBone.InverseTransformDirection(n);
            if (Ctx.tool != null)
            {
                plunger = Ctx.tool.Part("PLUNGER"); cotton = Ctx.tool.Part("COTTON");
                if (plunger != null) plungerRest = plunger.localPosition;
                if (cotton != null) cottonScale = cotton.localScale;
            }
            ring = Fx.Ring(Ctx.catalog, fp, 0.032f, new Color(1f, 0.85f, 0.5f, 1f));
            ring.transform.SetParent(holeBone, true);
            Deflate(1f);
        }

        void Deflate(float v) { if (Site) Ctx.toy.SetShapeAll(Ctx.def.shapeKey, v); else Ctx.toy.SetDeflate(v); }

        // --- the injector's seat in the opening --------------------------------------------------------------------
        Vector3 HoleN => holeBone != null ? holeBone.TransformDirection(holeNormalLocal).normalized : Vector3.up;
        /// <summary>From the nozzle up the barrel: tilted <see cref="Tilt"/>° off the skin normal toward the camera's upper right (seen at an angle, never end-on).</summary>
        Vector3 Handle
        {
            get
            {
                var n = HoleN; var cam = Ctx.cam.transform;
                var want = (-cam.forward * 0.25f + cam.right * 0.6f + cam.up * 0.85f).normalized;   // up and to the side on screen: a clear diagonal, not at the lens
                float ang = Vector3.Angle(n, want);
                return ang < 0.1f ? n : Vector3.Slerp(n, want, Mathf.Min(1f, Tilt / ang)).normalized;
            }
        }
        Quaternion SeatRot
        {
            get
            {
                var h = Handle; var f = Vector3.ProjectOnPlane(-Ctx.cam.transform.forward, h);
                if (f.sqrMagnitude < 1e-8f) f = Vector3.ProjectOnPlane(Ctx.cam.transform.up, h);
                return Quaternion.LookRotation(f.normalized * WindowSide, h);
            }
        }
        Vector3 Outside => model.FillPoint + Handle * 0.012f;
        Vector3 Inside => model.FillPoint - Handle * Depth;
        static float Ease(float k) => k * k * (3f - 2f * k);

        static IEnumerator Anim(float dur, System.Action<float> step)
        {
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; step(Mathf.Clamp01(t / dur)); yield return null; }
            step(1f);
        }

        IEnumerator SeatIn()
        {
            dock = Dock.Going;
            var tool = Ctx.tool;
            if (tool != null)
            {
                tool.Hold = true;
                Vector3 p0 = tool.Tip.position; Quaternion r0 = tool.transform.rotation;
                yield return Anim(0.16f, k => tool.PlaceTip(Vector3.Lerp(p0, Outside, Ease(k)), Quaternion.Slerp(r0, SeatRot, Ease(k))));  // line up, tilted
                Ctx.sfx.Play("click", 0.4f, 1.4f);
                yield return Anim(0.14f, k => tool.PlaceTip(Vector3.Lerp(Outside, Inside, k * k), SeatRot));                                   // nozzle slides in
            }
            dock = Dock.In; dockCo = null;
        }

        IEnumerator SeatOut()
        {
            dock = Dock.Leaving;
            var tool = Ctx.tool;
            if (tool != null)
            {
                Vector3 p0 = tool.Tip.position;
                yield return Anim(0.18f, k => tool.PlaceTip(Vector3.Lerp(p0, model.FillPoint + Handle * 0.03f, Ease(k)), SeatRot));
                tool.Hold = false;
            }
            dock = Dock.Free; dockCo = null;
        }

        void StartDock(IEnumerator co) { if (dockCo != null) StopCoroutine(dockCo); dockCo = StartCoroutine(co); }

        void UpdateInjector(float progress)
        {
            if (plunger != null) plunger.localPosition = plungerRest + Vector3.down * (Travel * progress);   // pushed toward the nozzle
            if (cotton != null) cotton.localScale = new Vector3(cottonScale.x, cottonScale.y * Mathf.Lerp(1f, CottonLeft, progress), cottonScale.z);
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null) return;
            model.FillPoint = Ctx.toy.Point(FillName, model.FillPoint);
            if (!completing)
            {
                bool done = model.Tick(p, f.held, Time.deltaTime);
                bool filling = model.Filling;
                if (filling) { outTimer = 0f; if (dock == Dock.Free || dock == Dock.Leaving) StartDock(SeatIn()); }
                else { outTimer += Time.deltaTime; if (dock == Dock.In && outTimer > 0.15f) StartDock(SeatOut()); }
                bool pushing = filling && dock == Dock.In;
                Ctx.sfx.SetPuff(pushing ? 0.8f : 0f);
                if (ring != null) ring.SetActive(dock == Dock.Free);
                // visual: deflated body gradually restores; small happy wobble while filling
                Deflate(1f - Mathf.SmoothStep(0, 1, model.Progress));
                Ctx.toy.SetFace(Mathf.Clamp01((model.Progress - 0.5f) * 2f) * 0.6f, Mathf.Lerp(0.5f, 0f, model.Progress));
                jiggle = Mathf.Lerp(jiggle, pushing ? 1f : 0f, 0.2f);
                Ctx.toy.bodyPulse = Mathf.Sin(Time.time * 22f) * 0.012f * jiggle;
                if (pushing && Random.value < 0.05f) Ctx.sfx.Play("click", 0.15f, 1.2f + model.Progress);
                if (pushing && (fluffTimer -= Time.deltaTime) <= 0f)
                {   // a wisp of cotton escapes round the nozzle now and then
                    fluffTimer = Random.Range(0.25f, 0.5f);
                    Fx.Burst(Ctx.catalog, model.FillPoint + HoleN * 0.004f, Color.white, 3, 0.0045f, 0.05f, 0.4f, -0.02f);
                }
                if (done) { completing = true; StartCoroutine(CompleteSeq()); }
            }
            if (dock == Dock.In && Ctx.tool != null) Ctx.tool.PlaceTip(Inside, SeatRot);   // stays seated while the body breathes and plumps
            UpdateInjector(model.Progress);
        }

        IEnumerator CompleteSeq()
        {
            Ctx.sfx.SetPuff(0f);
            while (dock == Dock.Going) yield return null;
            if (dockCo != null) { StopCoroutine(dockCo); dockCo = null; }
            if (dock != Dock.Free) yield return SeatOut();
            Complete();
        }

        void Complete()
        {
            Ctx.sfx.SetPuff(0f);
            Deflate(0f);
            Ctx.toy.bodyPulse = 0f;
            if (Site) Ctx.toy.SetVisual(Ctx.def.visualName, false); // the hole is filled
            else Ctx.toy.SetFillOpen(false); // flap closes with its button
            Ctx.sfx.Play("click", 1f, 0.7f);
            Ctx.sfx.Play("success", 0.8f);
            var at = Site ? model.FillPoint : Ctx.toy.Point("Spine", Ctx.toy.transform.position + Vector3.up * 0.2f);
            Fx.Burst(Ctx.catalog, at, new Color(1f, 1f, 0.9f), 34, 0.016f, 0.4f, 0.9f, -0.05f);
            Ctx.orbit.Punch(0.012f);
            Ctx.toy.SetFace(0.6f, 0f);
            Ctx.toy.PlayThen("ShakeBody", "Idle");
            Finish();
        }

        protected override void OnCancel()
        {
            StopAllCoroutines();
            if (Ctx.tool != null) Ctx.tool.Hold = false;
            UpdateInjector(0f);
            Ctx.sfx.SetPuff(0f);
            Deflate(1f);
            if (Site) Ctx.toy.SetVisual(Ctx.def.visualName, true); else Ctx.toy.SetFillOpen(false);
            Ctx.toy.bodyPulse = 0f;
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished()
        {
            if (ring != null) Destroy(ring);
        }
    }
}
