using System.Collections.Generic;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Sewing: drag the needle through the glowing stitch points in order; thread connects them and the tear closes.
    /// Mongi's back seam follows the SeamTracker; a site tear or a loose eye button uses the model's fixed SP_&lt;site&gt;_NN points (D-009).
    /// </summary>
    public class SewMiniGame : RepairMiniGame
    {
        StitchModel model;
        readonly List<Transform> sp = new List<Transform>();
        readonly List<StitchThread> threads = new List<StitchThread>();
        GameObject ring;
        LineRenderer live;
        float wrongCooldown;
        bool site; Transform threadParent;
        DamageDefinition Def => Ctx.def;
        bool Eye => Def != null && Def.type == DamageType.EyeButton;

        public override float Progress => model != null ? model.Progress : 0f;
        public override string Instruction => model != null && model.Done == 0 ? (Eye ? "빛나는 곳에 바늘을 꿰어 눈 단추를 달아요" : "빛나는 곳에 바늘을 꿰어요") : "다음 점으로 이어서 꿰매요";
        public override Vector3 HintPoint => model != null && model.NextIndex < sp.Count ? sp[model.NextIndex].position : Ctx.toy.Point(Def != null ? Def.damagePointName : "DP_BACK", Ctx.toy.Point("DP_BACK"));

        protected override void OnBegin()
        {
            var toy = Ctx.toy;
            site = Def != null && Def.HasSite && !string.IsNullOrEmpty(Def.stitchPrefix);
            if (site)
            {
                sp.AddRange(toy.StitchPoints(Def));
                if (sp.Count == 0 && toy.Node(Def.damagePointName) != null) sp.Add(toy.Node(Def.damagePointName)); // a model without stitch points yet: one stitch on the spot
                threadParent = toy.NodeOr(Def.boneName, toy.NodeOr("Spine", toy.transform));
            }
            else
            {
                var seam = toy.Seam; seam?.Refresh();
                if (seam != null) sp.AddRange(seam.Anchors);
                threadParent = toy.NodeOr("Spine", toy.transform);
            }
            if (sp.Count == 0) { Debug.LogError("[Sew] no stitch points"); return; }
            var pts = new List<Vector3>(); foreach (var s in sp) pts.Add(s.position);
            model = new StitchModel(pts) { Radius = 0.022f };
            var lgo = new GameObject("LiveThread"); lgo.transform.SetParent(transform, false);
            live = lgo.AddComponent<LineRenderer>(); live.useWorldSpace = true; live.positionCount = 2; live.widthMultiplier = 0.0035f;
            var m = Ctx.catalog.Mat("M_Thread"); if (m != null) live.sharedMaterial = m;
            live.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; live.enabled = false;
            ring = Fx.Ring(Ctx.catalog, sp[0].position, 0.022f, new Color(1f, 0.9f, 0.45f, 1f));
            ring.transform.SetParent(sp[0], true);
            if (site) ShowMend(0f); else toy.SetTearClosed(0f);
            toy.SetFace(0, 0.7f);
        }

        /// <summary>Site visuals for mending progress k (0..1): the tear closes, the loose eye button is pulled back in, a limp arm firms up.</summary>
        void ShowMend(float k)
        {
            var toy = Ctx.toy;
            if (Def.type == DamageType.Tear) toy.SetTearClosed(Def, k >= 1f ? 1f : k * 0.85f);
            if (Eye) toy.SetShapeAll(Def.shapeKey, 1f - k);
            if (!string.IsNullOrEmpty(Def.limpBone)) toy.LimpTarget = 1f - k;
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null || model.Complete) return;
            wrongCooldown -= Time.deltaTime;
            // keep model points on the moving body
            for (int i = 0; i < sp.Count; i++) model.Points[i] = sp[i].position;

            if (f.held)
            {
                var res = model.Move(p);
                if (res == StitchModel.Result.Stitched || res == StitchModel.Result.Finished) OnStitch(res == StitchModel.Result.Finished);
                else if (res == StitchModel.Result.WrongPoint && wrongCooldown <= 0f)
                {
                    wrongCooldown = 0.8f; Ctx.sfx.Play("wrong", 0.25f, 1.3f);
                    if (ring != null) ring.transform.localScale = Vector3.one * 1.4f; // gentle guide: next point pulses bigger
                }
            }
            if (ring != null) ring.transform.localScale = Vector3.Lerp(ring.transform.localScale, Vector3.one, 0.15f);

            // live thread follows the needle from the last stitched point
            bool show = f.held && model.Done > 0 && !model.Complete;
            live.enabled = show;
            if (show) { live.SetPosition(0, sp[model.Done - 1].position + Vector3.zero); live.SetPosition(1, p); }
        }

        void OnStitch(bool finished)
        {
            int done = model.Done;
            Ctx.sfx.Play("stitch", 0.8f, 0.9f + done * 0.05f);
            Fx.Burst(Ctx.catalog, sp[done - 1].position, new Color(1f, 0.95f, 0.7f), 8, 0.007f, 0.12f, 0.4f, 0f);
            if (done >= 2)
            {
                var a = sp[done - 2]; var b = sp[done - 1];
                threads.Add(StitchThread.Create(threadParent, a, b, Ctx.catalog.Mat("M_Thread")));
            }
            if (site) ShowMend(finished ? 1f : Mathf.Clamp01((float)done / sp.Count));
            else Ctx.toy.SetTearClosed(finished ? 1f : Mathf.Clamp01((float)done / sp.Count) * 0.85f);
            if (finished) { SewFinished(); return; }
            if (ring != null) { ring.transform.SetParent(sp[model.NextIndex], false); ring.transform.localPosition = Vector3.zero; }
        }

        void SewFinished()
        {
            if (ring != null) Destroy(ring);
            if (live != null) live.enabled = false;
            if (Eye) Ctx.toy.SetVisual(Def.visualName, false); // the button is back in place
            Ctx.sfx.Play("success", 0.7f);
            Fx.Burst(Ctx.catalog, site ? Ctx.toy.Point(Def.damagePointName, sp[sp.Count - 1].position) : Ctx.toy.Point("DP_BACK"), new Color(1f, 0.92f, 0.7f), 24, 0.012f, 0.3f, 0.8f, -0.05f);
            Ctx.orbit.Punch(0.01f);
            Ctx.toy.PlayThen("Relief", "Idle");
            Finish();
        }

        protected override void OnCancel()
        {
            foreach (var t in threads) if (t != null) Destroy(t.gameObject);
            if (site) { ShowMend(0f); if (Eye) Ctx.toy.SetVisual(Def.visualName, true); }
            else Ctx.toy.SetTearClosed(0f);
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished()
        {
            // the finished zig-zag stays on the toy as its seam (also restored in later scenes)
            if (site) Ctx.toy.AdoptSeam(Def, threads); else Ctx.toy.AdoptSeam(threads);
        }
    }
}
