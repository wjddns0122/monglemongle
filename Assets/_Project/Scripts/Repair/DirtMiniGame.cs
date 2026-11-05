using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>Dirt cleaning: rub the sponge over the stains; the rubbed spots clean first. "Paint" uses the same rules with a brush (D-009).</summary>
    public class DirtMiniGame : RepairMiniGame
    {
        DirtField field;
        DirtModel Model => field != null ? field.Model : null;
        Vector3 prev; bool hasPrev;
        ParticleSystem foam;
        float spongeRadius = 0.028f;
        float hintDelay;

        public override float Progress => Model != null ? Model.Progress : 0f;
        bool Paint => Ctx.def != null && Ctx.def.type == DamageType.Paint;
        string Spot => Ctx.def != null && !string.IsNullOrEmpty(Ctx.def.damagePointName) ? Ctx.def.damagePointName : "DP_BELLY";
        public override string Instruction => Paint ? "붓으로 벗겨진 곳을 칠해요" : "스펀지로 얼룩을 문질러요";
        public override Vector3 HintPoint
        {
            get
            {
                if (Model == null) return Ctx.toy.Point(Spot);
                for (int i = 0; i < Model.Count; i++) if (Model.Health[i] > 0) return Model.Positions[i];
                return Ctx.toy.Point(Spot);
            }
        }

        protected override void OnBegin()
        {
            field = Ctx.toy.DirtFieldFor(Ctx.def != null ? Ctx.def.id : null);
            if (field == null) field = Ctx.def != null ? Ctx.toy.EnsureDirtField(Ctx.def) : Ctx.toy.GetComponentInChildren<DirtField>(true);
            if (field == null) { Debug.LogError("[Dirt] no DirtField on toy"); return; }
            field.SetVisible(true);
            field.Model.Power = 1.4f;
            // soap foam floats up; paint drips as little coloured droplets
            foam = Fx.Stream(Ctx.catalog, Ctx.tool != null ? Ctx.tool.transform : transform, Paint ? Ctx.def.cleanColor : new Color(1f, 1f, 1f, 0.9f), 0, Paint ? 0.007f : 0.01f, 0.1f);
            if (Paint) { var mn = foam.main; mn.gravityModifier = 0.5f; mn.startLifetime = 0.45f; }
            Ctx.toy.SetFace(0, 0.8f);
        }

        protected override void OnTick(PointerFrame f, Vector3 p, bool onSurface, Vector3 n)
        {
            if (field == null) return;
            field.SyncPositions();
            bool rubbing = false;
            float speed = 0f;
            if (f.held && onSurface)
            {
                if (hasPrev)
                {
                    float moved = (p - prev).magnitude;
                    speed = moved / Mathf.Max(0.0001f, Time.deltaTime);
                    // only movement that overlaps remaining dirt counts (02 §7)
                    if (moved > 0.0005f && Model.Overlaps(p, spongeRadius))
                    {
                        rubbing = true;
                        bool done = Model.Rub(p, spongeRadius, Mathf.Min(moved, 0.05f));
                        field.Refresh();
                        if (done) { CleanFinished(p); return; }
                    }
                }
                prev = p; hasPrev = true;
            }
            else hasPrev = false;

            // feedback: squish, foam, rub sound, a bit of comfort for the toy
            if (Ctx.tool != null) Ctx.tool.transform.localScale = Vector3.Lerp(Ctx.tool.transform.localScale, new Vector3(1f, rubbing ? 0.82f : 1f, 1f), 0.3f);
            var em = foam.emission; em.rateOverTime = rubbing ? 38 : 0;
            if (rubbing && !foam.isPlaying) foam.Play();
            Ctx.sfx.SetRub(rubbing ? Mathf.Clamp01(speed * 0.8f + 0.2f) : 0f);
            if (rubbing) { Ctx.toy.bodyPulse = Mathf.Sin(Time.time * 18f) * 0.006f; Ctx.toy.SetFace(0, Mathf.Lerp(0.8f, 0.3f, Progress)); }
            else Ctx.toy.bodyPulse = Mathf.Lerp(Ctx.toy.bodyPulse, 0f, 0.3f);
        }

        void CleanFinished(Vector3 p)
        {
            Ctx.sfx.SetRub(0f);
            Ctx.sfx.Play("chime", 0.9f);
            Fx.Burst(Ctx.catalog, p, Paint ? Color.Lerp(Ctx.def.cleanColor, Color.white, 0.4f) : new Color(1f, 1f, 0.85f), 26, 0.012f, 0.3f, 0.8f, -0.05f);
            Ctx.orbit.Punch(0.008f);
            Ctx.toy.bodyPulse = 0f;
            Ctx.toy.PlayThen("Relief", "Idle");
            Finish();
        }

        protected override void OnCancel() { Ctx.sfx.SetRub(0f); Ctx.toy.bodyPulse = 0f; if (foam != null) Destroy(foam.gameObject); }
        protected override void OnFinished() { if (foam != null) Destroy(foam.gameObject, 1f); if (field != null) field.SetVisible(false); }
    }
}
