using System.Collections;
using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Battery swap (D-009): grab the old battery and pull it out of its slot (it pops away like a thorn), then bring the new one
    /// (the tool) to the slot FP_&lt;site&gt; and hold it there until it clicks in. The power lamps come back on.
    /// </summary>
    public class BatteryMiniGame : RepairMiniGame
    {
        BatteryModel model;
        Transform cell; Vector3 cellRest, outDir;
        GameObject ring;

        public override float Progress => model != null ? model.Progress : 0f;
        public override string Instruction => model == null || !model.Removed
            ? (model != null && model.Pull.Grabbed ? "천천히 바깥쪽으로 빼요" : "다 쓴 건전지를 꾹 잡아요")
            : "새 건전지를 빈 칸에 대고 꾹 눌러요";
        public override Vector3 HintPoint => model == null ? Ctx.toy.Point(Ctx.def.damagePointName) : !model.Removed ? model.Pull.GrabPoint : model.SlotPoint;
        /// <summary>Which way the old battery has to be pulled out (on the camera-facing plane), for the scripted QA.</summary>
        public Vector3 PullDirection => model != null ? model.Pull.RemovalDir : Vector3.zero;

        protected override void OnBegin()
        {
            var toy = Ctx.toy; var def = Ctx.def;
            var dp = toy.Point(def.damagePointName, toy.BodyBounds.center);
            var dn = toy.Node(def.normalPointName);
            outDir = dn != null && (dn.position - dp).sqrMagnitude > 1e-8f ? (dn.position - dp).normalized : toy.transform.forward;
            cell = toy.BeginThornProxy(def);
            var grab = CellCenter(dp);
            // the pointer only moves in the camera-facing plane: validate the pull against that projection
            var plane = Vector3.ProjectOnPlane(outDir, Ctx.cam.transform.forward);
            var validDir = plane.magnitude > 0.35f ? plane.normalized : Ctx.cam.transform.right;
            model = new BatteryModel(grab, validDir, toy.Point(def.workPointName, dp)) { SlotRadius = 0.05f, ClickHold = 0.5f };
            model.Pull.GrabRadius = 0.045f; model.Pull.MinHold = 0.12f; model.Pull.RequiredPull = 0.06f; model.Pull.MaxAngleDeg = 65f;
            if (cell != null) cellRest = cell.position;
            if (toy.Animator != null) toy.Animator.speed = 0f; // hold still while the old battery is pulled
            if (Ctx.tool != null) Ctx.tool.SetVisible(false);  // first the old one comes out
            ring = Fx.Ring(Ctx.catalog, grab, 0.03f, new Color(1f, 0.85f, 0.4f, 0.9f));
            ring.transform.SetParent(cell != null ? cell : toy.NodeOr(def.boneName, toy.transform), true);
            toy.SetGlow(0f);
            toy.SetFace(0, 0.8f);
        }

        Vector3 CellCenter(Vector3 fallback)
        {
            var mf = cell != null ? cell.GetComponent<MeshFilter>() : null;
            return mf != null && mf.sharedMesh != null ? cell.TransformPoint(mf.sharedMesh.bounds.center) : cell != null ? cell.position : fallback;
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (model == null || model.Complete) return;
            if (!model.Removed)
            {
                if (!model.Pull.Grabbed) model.Pull.GrabPoint = CellCenter(model.Pull.GrabPoint);
                if (f.pressed)
                {
                    if (model.TryGrab(p)) { Ctx.sfx.Play("grab", 0.7f); if (ring != null) ring.SetActive(false); }
                    else Ctx.sfx.Play("wrong", 0.35f, 1.2f);
                }
                if (f.held && model.Pull.Grabbed)
                {
                    bool popped = model.Drag(p, Time.deltaTime);
                    if (cell != null) cell.position = cellRest + outDir * (model.Pull.Progress * 0.04f);
                    if (model.Pull.Progress > 0.01f) Ctx.toy.SetFace(0, 1f);
                    if (popped) StartCoroutine(PopOld());
                }
                if (f.released && model.Pull.Grabbed && !model.Removed)
                {
                    model.Release();
                    Ctx.sfx.Play("wrong", 0.3f, 1.4f);
                    if (cell != null) cell.position = cellRest + outDir * (model.Pull.Progress * 0.04f);
                    if (ring != null) ring.SetActive(true);
                }
                return;
            }
            // the new battery rides on the pointer, which moves in the camera plane: compare at the slot's depth
            var toy = Ctx.toy; var fwd = Ctx.cam.transform.forward;
            model.SlotPoint = toy.Point(Ctx.def.workPointName, model.SlotPoint);
            var q = p - fwd * Vector3.Dot(p - model.SlotPoint, fwd);
            bool done = model.Insert(q, f.held, Time.deltaTime);
            if (ring != null) ring.SetActive(!model.Inserting);
            if (done) ClickIn();
        }

        IEnumerator PopOld()
        {
            var toy = Ctx.toy;
            if (toy.Animator != null) toy.Animator.speed = 1f;
            Ctx.sfx.Play("pop", 1f);
            Ctx.orbit.Punch(0.01f);
            var t = cell; cell = null;
            Fx.Burst(Ctx.catalog, t != null ? t.position : model.Pull.GrabPoint, new Color(1f, 0.95f, 0.6f), 16, 0.012f, 0.3f, 0.6f, 0.2f);
            // the empty slot glows: now the new battery
            if (ring != null)
            {
                ring.transform.SetParent(null, true); ring.transform.position = model.SlotPoint;
                ring.transform.SetParent(toy.NodeOr(Ctx.def.boneName, toy.transform), true); ring.SetActive(true);
            }
            if (Ctx.tool != null) { Ctx.tool.SetVisible(true); Ctx.tool.Follow(model.SlotPoint + outDir * 0.06f, Ctx.cam, true); }
            Ctx.sfx.Play("sparkle", 0.5f);
            toy.SetFace(0, 0.5f);
            if (t == null) yield break;
            t.SetParent(null, true);
            var v = outDir * 0.3f + Vector3.up * 0.5f + Ctx.cam.transform.forward * -0.2f;
            float life = 0f; var start = t.localScale;
            while (life < 0.7f && t != null)
            {
                life += Time.deltaTime; v += Vector3.down * 1.6f * Time.deltaTime;
                t.position += v * Time.deltaTime; t.Rotate(0, 420 * Time.deltaTime, 300 * Time.deltaTime);
                t.localScale = start * Mathf.Clamp01(1f - life / 0.7f);
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        void ClickIn()
        {
            var toy = Ctx.toy;
            if (Ctx.tool != null) Ctx.tool.SetVisible(false);
            toy.SetVisual(Ctx.def.visualDoneName, true);
            if (ring != null) Destroy(ring);
            Ctx.sfx.Play("click", 1f, 0.9f);
            Ctx.sfx.Play("success", 0.8f);
            Fx.Burst(Ctx.catalog, model.SlotPoint, new Color(1f, 0.95f, 0.6f), 30, 0.014f, 0.35f, 0.9f, -0.05f);
            Ctx.orbit.Punch(0.012f);
            StartCoroutine(PowerUp());
            toy.SetFace(0.6f, 0f);
            toy.PlayThen("Happy", "Idle");
            Finish();
        }

        IEnumerator PowerUp()
        {
            for (float t = 0f; t < 0.6f; t += Time.deltaTime) { Ctx.toy.SetGlow(t / 0.6f); yield return null; }
            Ctx.toy.SetGlow(1f);
        }

        protected override void OnCancel()
        {
            var toy = Ctx.toy;
            if (toy.Animator != null) toy.Animator.speed = 1f;
            toy.EndThornProxy(true);
            toy.SetVisual(Ctx.def.visualDoneName, false);
            toy.SetGlow(0f);
            if (Ctx.tool != null) Ctx.tool.SetVisible(true);
            if (ring != null) Destroy(ring);
        }

        protected override void OnFinished() { if (ring != null) Destroy(ring); }
    }
}
