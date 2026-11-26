using System.Collections;
using System.Collections.Generic;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Sewing: drag the needle through the glowing stitch points in order. Each stitch plays as real hand sewing (D-037):
    /// the needle pierces the fabric from the outside at the point, scoops under it and its tip comes out a little further
    /// down the seam, then it is pulled through — the thread follows it out, the stitch over the gap pulls tight and the
    /// tear closes a step. The rules (order, radius, progress) stay in StitchModel; stitches are queued, so fast dragging
    /// never loses one (the scoops just play faster).
    /// Mongi's back seam follows the SeamTracker; a site tear or a loose eye button uses the model's fixed SP_&lt;site&gt;_NN points (D-009).
    /// </summary>
    public class SewMiniGame : RepairMiniGame
    {
        StitchModel model;
        readonly List<Transform> sp = new List<Transform>();
        readonly List<Transform> entries = new List<Transform>(), exits = new List<Transform>();
        readonly List<StitchThread> threads = new List<StitchThread>();
        readonly Queue<int> pending = new Queue<int>();
        readonly List<Vector3> livePts = new List<Vector3>();
        GameObject ring;
        ThreadTube live; Transform eye, lastExit;
        float wrongCooldown, mendShown, mendTarget, liveSlack = 1f;
        bool site, animating, finishing, finished;
        Transform threadParent; Vector3 seamAxis;
        DamageDefinition Def => Ctx.def;
        bool Eye => Def != null && Def.type == DamageType.EyeButton;
        const float ScoopLength = 0.0045f;   // the needle comes back out this far down the seam from where it went in

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
            seamAxis = sp.Count > 1 ? sp[sp.Count - 1].position - sp[0].position : Vector3.down;
            seamAxis = seamAxis.sqrMagnitude > 1e-8f ? seamAxis.normalized : Vector3.down;
            for (int i = 0; i < sp.Count; i++) AddEntryExit(i);
            live = ThreadTube.Create(transform, "LiveThread", Ctx.catalog.Mat("M_Thread"), 0.0009f);
            eye = Ctx.tool != null ? (Ctx.tool.Part("EYE") ?? Ctx.tool.transform) : null;
            ring = Fx.Ring(Ctx.catalog, sp[0].position, 0.022f, new Color(1f, 0.9f, 0.45f, 1f));
            ring.transform.SetParent(sp[0], true);
            ApplyMend(0f);
            toy.SetFace(0, 0.7f);
        }

        /// <summary>Where the needle goes in (on the skin under the stitch point) and where its tip comes back out (a little further down the seam).</summary>
        void AddEntryExit(int i)
        {
            var s = sp[i]; Vector3 point, n;
            if (s.name.StartsWith("SEAM_")) { n = s.forward; point = s.position - n * (Ctx.toy.Seam != null ? Ctx.toy.Seam.Lift : 0f); }
            else
            {
                var g = StitchThread.SurfaceNormal(s, s.position); point = s.position; n = g;
                if (Ctx.probe != null && Ctx.probe.Raycast(new Ray(s.position + g * 0.05f, -g), out var hp, out var hn, 0.1f)) { point = hp; n = hn; }
            }
            var along = Vector3.ProjectOnPlane(seamAxis, n); along = along.sqrMagnitude > 1e-8f ? along.normalized : Vector3.Cross(n, Vector3.right).normalized;
            entries.Add(Marker("ENT_" + i, s, point, n));
            exits.Add(Marker("XIT_" + i, s, point + along * ScoopLength, n));
        }

        static Transform Marker(string name, Transform parent, Vector3 pos, Vector3 n)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.LookRotation(n, Vector3.up)); return t;
        }

        /// <summary>Visual mend state for progress frac (0..1): the tear closes, the loose eye button is pulled back in, a limp arm firms up.</summary>
        void ApplyMend(float frac)
        {
            if (site) ShowMend(frac);
            else Ctx.toy.SetTearClosed(frac >= 0.999f ? 1f : frac * 0.85f);
        }

        void ShowMend(float k)
        {
            var toy = Ctx.toy;
            if (Def.type == DamageType.Tear) toy.SetTearClosed(Def, k >= 0.999f ? 1f : k * 0.85f);
            if (Eye) toy.SetShapeAll(Def.shapeKey, 1f - k);
            if (!string.IsNullOrEmpty(Def.limpBone)) toy.LimpTarget = 1f - k;
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null || finished) return;
            wrongCooldown -= Time.deltaTime;
            for (int i = 0; i < sp.Count; i++) model.Points[i] = sp[i].position;   // keep the points on the moving body

            if (!model.Complete && f.held)
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
            if (!animating && pending.Count > 0) StartCoroutine(Stitch(pending.Dequeue()));

            mendShown = Mathf.MoveTowards(mendShown, mendTarget, Time.deltaTime * 0.9f);   // the gap closes as the thread is pulled tight
            ApplyMend(mendShown);
            if (!animating) liveSlack = Mathf.MoveTowards(liveSlack, 0.35f, Time.deltaTime * 0.8f);
            UpdateLiveThread();
        }

        void OnStitch(bool done)
        {
            pending.Enqueue(model.Done - 1);
            if (done) { finishing = true; if (ring != null) Destroy(ring); }
            else if (ring != null) { ring.transform.SetParent(sp[model.NextIndex], false); ring.transform.localPosition = Vector3.zero; }
        }

        // --- one stitch, played on the tool -------------------------------------------------------------------------
        Vector3 Ent(int i) => entries[i].position;
        Vector3 Xit(int i) => exits[i].position;
        Vector3 N(int i) => entries[i].forward;
        Vector3 Along(int i) { var d = Xit(i) - Ent(i); return d.sqrMagnitude > 1e-10f ? d.normalized : seamAxis; }
        Vector3 DirIn(int i) => (-N(i) * 0.72f + Along(i) * 0.7f).normalized;    // tip going into the fabric
        Vector3 DirOut(int i) => (N(i) * 0.62f + Along(i) * 0.78f).normalized;   // tip coming back out
        Quaternion NeedleRot(int i, Vector3 tipDir) => Quaternion.LookRotation(Vector3.Cross(N(i), Along(i)).normalized, -tipDir); // tool +Y = from the tip to the eye
        static float Ease(float k) => k * k * (3f - 2f * k);

        static IEnumerator Anim(float dur, System.Action<float> step)
        {
            float t = 0f;
            while (t < dur) { t += Time.deltaTime; step(Mathf.Clamp01(t / dur)); yield return null; }
            step(1f);
        }

        IEnumerator Stitch(int i)
        {
            animating = true;
            var tool = Ctx.tool; bool hasTool = tool != null && tool.Tip != null;
            if (hasTool) tool.Hold = true;
            float speed = 1f + pending.Count * 0.9f;
            float len = hasTool && eye != null ? Mathf.Max(0.03f, (eye.position - tool.Tip.position).magnitude) : 0.1f;
            if (hasTool)
            {
                // 1. the point comes down onto the stitch point, the needle tilted into the fabric
                Vector3 p0 = tool.Tip.position; Quaternion r0 = tool.transform.rotation;
                yield return Anim(0.13f / speed, k => tool.PlaceTip(Vector3.Lerp(p0, Ent(i) + N(i) * 0.0015f, Ease(k)), Quaternion.Slerp(r0, NeedleRot(i, DirIn(i)), Ease(k))));
                Ctx.sfx.Play("stitch", 0.45f, 1.3f);
                // 2. push in: from the outside into the fabric
                yield return Anim(0.17f / speed, k => tool.PlaceTip(Ent(i) + N(i) * 0.0015f + DirIn(i) * (0.008f * k * k), NeedleRot(i, DirIn(i))));
                // 3. scoop: rock the needle under the fabric so the tip comes up and out a little further down the seam
                yield return Anim(0.18f / speed, k =>
                {
                    var a = Ent(i) + N(i) * 0.0015f + DirIn(i) * 0.008f; var c = Ent(i) + Along(i) * 0.0028f - N(i) * 0.0045f; var b = Xit(i) + N(i) * 0.0045f + DirOut(i) * 0.002f;
                    float u = Ease(k);
                    tool.PlaceTip((1 - u) * (1 - u) * a + 2 * (1 - u) * u * c + u * u * b, Quaternion.Slerp(NeedleRot(i, DirIn(i)), NeedleRot(i, DirOut(i)), u));
                });
            }
            // 4. pull through: the thread follows the needle out of the exit and the stitch over the gap pulls tight
            StitchThread th = null;
            if (i > 0)
            {
                th = StitchThread.Create(threadParent, exits[i - 1], entries[i], Ctx.catalog.Mat("M_Thread"));
                th.Dip = 0.0012f; th.Slack = 1f; threads.Add(th);
            }
            lastExit = exits[i]; liveSlack = 1f;
            mendTarget = Mathf.Max(mendTarget, Mathf.Clamp01((i + 1f) / sp.Count));
            Ctx.sfx.Play("stitch", 0.85f, 0.9f + i * 0.05f);
            Vector3 from = Xit(i) + N(i) * 0.003f; float pull = len * 1.12f;
            yield return Anim(0.26f / speed, k =>
            {
                float u = 1f - (1f - k) * (1f - k);
                if (hasTool) tool.PlaceTip(Xit(i) + N(i) * 0.003f + DirOut(i) * (pull * u), NeedleRot(i, DirOut(i)));
                if (th != null) th.Slack = 1f - u;
                liveSlack = 1f - 0.65f * u;
            });
            if (th != null) th.Slack = 0f;
            if (hasTool) tool.Hold = false;
            animating = false;
            if (finishing && pending.Count == 0) SewFinished();
        }

        /// <summary>The thread from where it last came out of the fabric up to the needle's eye, sagging a little.</summary>
        void UpdateLiveThread()
        {
            if (live == null) return;
            if (finished || lastExit == null || eye == null || (Ctx.tool != null && !Ctx.tool.gameObject.activeInHierarchy)) { live.Hide(); return; }
            var a = lastExit.position; var nA = lastExit.forward; var e = eye.position; float len = (e - a).magnitude;
            livePts.Clear();
            for (int k = 0; k <= 16; k++)
            {
                float u = k / 16f, s = Mathf.Sin(Mathf.PI * u);
                livePts.Add(Vector3.Lerp(a, e, u) + nA * (0.005f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, u * 1.6f)) - 0.0012f * Mathf.Pow(1f - Mathf.Min(1f, u * 4f), 3f))
                            + Vector3.down * (len * 0.22f * liveSlack * s));
            }
            live.SetPoints(livePts);
        }

        void SewFinished()
        {
            if (finished) return;
            finished = true;
            if (ring != null) Destroy(ring);
            if (live != null) live.Hide();
            mendShown = mendTarget = 1f; ApplyMend(1f);
            if (Eye) Ctx.toy.SetVisual(Def.visualName, false); // the button is back in place
            Ctx.sfx.Play("success", 0.7f);
            Fx.Burst(Ctx.catalog, site ? Ctx.toy.Point(Def.damagePointName, sp[sp.Count - 1].position) : Ctx.toy.Point("DP_BACK"), new Color(1f, 0.92f, 0.7f), 24, 0.012f, 0.3f, 0.8f, -0.05f);
            Ctx.orbit.Punch(0.01f);
            Ctx.toy.PlayThen("Relief", "Idle");
            Finish();
        }

        void ClearMarkers()
        {
            foreach (var t in entries) if (t != null) Destroy(t.gameObject);
            foreach (var t in exits) if (t != null) Destroy(t.gameObject);
            entries.Clear(); exits.Clear();
            if (live != null) { Destroy(live.gameObject); live = null; }
        }

        protected override void OnCancel()
        {
            StopAllCoroutines();
            if (Ctx.tool != null) Ctx.tool.Hold = false;
            foreach (var t in threads) if (t != null) Destroy(t.gameObject);
            ApplyMend(0f);
            if (site && Eye) Ctx.toy.SetVisual(Def.visualName, true);
            if (ring != null) Destroy(ring);
            ClearMarkers();
        }

        protected override void OnFinished()
        {
            // D-018: no loose thread stays behind; the sewn-shut tear shows its own neat seam (D-037 Closed key)
            if (site) Ctx.toy.AdoptSeam(Def, threads); else Ctx.toy.AdoptSeam(threads);
            ClearMarkers();
        }

        void OnDestroy() { ClearMarkers(); }
    }
}
