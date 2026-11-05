using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Cotton fill: hold the injector on the cute opening; the body slowly plumps up. No overfill failure.
    /// Mongi fills the whole body through the button flap; a site damage fills one part through its hole FP_&lt;site&gt; (D-009).
    /// </summary>
    public class CottonMiniGame : RepairMiniGame
    {
        CottonModel model;
        ParticleSystem puff;
        GameObject ring;
        float jiggle;
        bool Site => Ctx.def != null && Ctx.def.HasSite;
        string FillName => Site && !string.IsNullOrEmpty(Ctx.def.workPointName) ? Ctx.def.workPointName : "FP_FILL";

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
            puff = Fx.Stream(Ctx.catalog, Ctx.tool != null ? Ctx.tool.transform : transform, new Color(1f, 1f, 1f, 0.95f), 0, 0.016f, 0.14f);
            ring = Fx.Ring(Ctx.catalog, fp, 0.032f, new Color(1f, 0.85f, 0.5f, 1f));
            ring.transform.SetParent(toy.NodeOr(Site ? Ctx.def.boneName : "Hips", toy.transform), true);
            Deflate(1f);
        }

        void Deflate(float v) { if (Site) Ctx.toy.SetShapeAll(Ctx.def.shapeKey, v); else Ctx.toy.SetDeflate(v); }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null || model.Complete) return;
            model.FillPoint = Ctx.toy.Point(FillName, model.FillPoint);
            bool done = model.Tick(p, f.held, Time.deltaTime);
            bool filling = model.Filling;
            var em = puff.emission; em.rateOverTime = filling ? 45 : 0;
            if (filling && !puff.isPlaying) puff.Play();
            Ctx.sfx.SetPuff(filling ? 0.8f : 0f);
            if (ring != null) ring.SetActive(!filling);
            // visual: deflated body gradually restores; small happy wobble while filling
            Deflate(1f - Mathf.SmoothStep(0, 1, model.Progress));
            Ctx.toy.SetFace(Mathf.Clamp01((model.Progress - 0.5f) * 2f) * 0.6f, Mathf.Lerp(0.5f, 0f, model.Progress));
            jiggle = Mathf.Lerp(jiggle, filling ? 1f : 0f, 0.2f);
            Ctx.toy.bodyPulse = Mathf.Sin(Time.time * 22f) * 0.012f * jiggle;
            if (filling && Random.value < 0.05f) Ctx.sfx.Play("click", 0.15f, 1.2f + model.Progress);
            if (done) Complete();
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
            Ctx.sfx.SetPuff(0f);
            Deflate(1f);
            if (Site) Ctx.toy.SetVisual(Ctx.def.visualName, true); else Ctx.toy.SetFillOpen(false);
            Ctx.toy.bodyPulse = 0f;
            if (puff != null) Destroy(puff.gameObject);
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished()
        {
            if (puff != null) Destroy(puff.gameObject, 1f);
            if (ring != null) Destroy(ring);
        }
    }
}
