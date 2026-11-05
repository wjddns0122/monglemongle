using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Refuel (D-009): the cap opens, hold the nozzle on the port FP_&lt;site&gt; until the tank is full (fuel, or water for the fire
    /// truck, in the definition's stain colour); the power lamps brighten as it fills. No overfill failure.
    /// </summary>
    public class FuelMiniGame : RepairMiniGame
    {
        CottonModel model;
        ParticleSystem stream;
        GameObject ring;
        float jiggle;
        string Port => !string.IsNullOrEmpty(Ctx.def.workPointName) ? Ctx.def.workPointName : Ctx.def.damagePointName;

        public override float Progress => model != null ? model.Progress : 0f;
        public override string Instruction => model != null && model.Progress > 0f ? "그대로 꾹 눌러서 가득 채워요" : "주입구에 대고 꾹 눌러요";
        public override Vector3 HintPoint => model != null ? model.FillPoint : Ctx.toy.Point(Port);

        protected override void OnBegin()
        {
            var toy = Ctx.toy;
            var fp = toy.Point(Port, toy.Point(Ctx.def.damagePointName, toy.transform.position + Vector3.up * 0.15f));
            model = new CottonModel(fp) { Radius = 0.055f, HoldSeconds = 4f };
            // the cap opens only while fueling
            toy.SetVisual(Ctx.def.visualName, false); toy.SetVisual(Ctx.def.openName, true);
            Ctx.sfx.Play("click", 0.8f, 1.1f);
            stream = Fx.Stream(Ctx.catalog, Ctx.tool != null ? Ctx.tool.Tip : transform, Ctx.def.stainColor, 0, 0.009f, 0.22f);
            if (Ctx.tool != null)
            {
                // pour out of the nozzle tip, away from the handle (the tool's +Y)
                var up = Ctx.tool.transform.up;
                stream.transform.SetPositionAndRotation(Ctx.tool.Tip.position + up * 0.012f, Quaternion.LookRotation(-up));
            }
            var main = stream.main; main.gravityModifier = 0.6f; main.startLifetime = 0.35f;
            var sh = stream.shape; sh.angle = 8f;
            ring = Fx.Ring(Ctx.catalog, fp, 0.032f, new Color(1f, 0.85f, 0.5f, 1f));
            ring.transform.SetParent(toy.NodeOr(Ctx.def.boneName, toy.transform), true);
            toy.SetGlow(0f);
            toy.SetFace(0, 0.6f);
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null || model.Complete) return;
            var toy = Ctx.toy;
            model.FillPoint = toy.Point(Port, model.FillPoint);
            bool done = model.Tick(p, f.held, Time.deltaTime);
            bool filling = model.Filling;
            var em = stream.emission; em.rateOverTime = filling ? 60 : 0;
            if (filling && !stream.isPlaying) stream.Play();
            Ctx.sfx.SetPuff(filling ? 0.6f : 0f);
            if (ring != null) ring.SetActive(!filling);
            toy.SetGlow(model.Progress);
            toy.SetFace(Mathf.Clamp01((model.Progress - 0.5f) * 2f) * 0.6f, Mathf.Lerp(0.5f, 0f, model.Progress));
            jiggle = Mathf.Lerp(jiggle, filling ? 1f : 0f, 0.2f);
            toy.bodyPulse = Mathf.Sin(Time.time * 18f) * 0.008f * jiggle;
            if (filling && Random.value < 0.05f) Ctx.sfx.Play("click", 0.12f, 1.3f + model.Progress);
            if (done) Complete();
        }

        void Complete()
        {
            var toy = Ctx.toy;
            Ctx.sfx.SetPuff(0f);
            toy.bodyPulse = 0f;
            toy.SetVisual(Ctx.def.visualName, true); toy.SetVisual(Ctx.def.openName, false); // cap closes
            toy.SetGlow(1f);
            Ctx.sfx.Play("click", 1f, 0.8f);
            Ctx.sfx.Play("success", 0.8f);
            Fx.Burst(Ctx.catalog, model.FillPoint, Color.Lerp(Ctx.def.stainColor, Color.white, 0.5f), 30, 0.014f, 0.35f, 0.9f, -0.05f);
            Ctx.orbit.Punch(0.012f);
            toy.SetFace(0.6f, 0f);
            toy.PlayThen("Happy", "Idle");
            Finish();
        }

        protected override void OnCancel()
        {
            Ctx.sfx.SetPuff(0f);
            Ctx.toy.SetVisual(Ctx.def.visualName, true); Ctx.toy.SetVisual(Ctx.def.openName, false);
            Ctx.toy.SetGlow(0f);
            Ctx.toy.bodyPulse = 0f;
            if (stream != null) Destroy(stream.gameObject);
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished()
        {
            if (stream != null) Destroy(stream.gameObject, 1f);
            if (ring != null) Destroy(ring);
        }
    }
}
