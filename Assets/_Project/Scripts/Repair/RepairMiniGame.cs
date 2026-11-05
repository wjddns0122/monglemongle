using System;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Repair
{
    public class RepairContext
    {
        public ToyActor toy;
        public ToyRuntime state;
        public DamageDefinition def;
        public ContentCatalog catalog;
        public Camera cam;
        public OrbitCamera orbit;
        public PointerInput pointer;
        public Sfx sfx;
        public SurfaceProbe probe;
        public ToolView tool;
    }

    /// <summary>
    /// Standard repair runtime model (02 §2): INPUT → VALIDATION → PROGRESS → VISUAL → FEEDBACK → COMPLETE.
    /// Subclasses implement OnTick; the base guarantees one completion and cleanup on cancel.
    /// </summary>
    public abstract class RepairMiniGame : MonoBehaviour
    {
        public RepairContext Ctx { get; private set; }
        public bool Completed { get; private set; }
        public abstract float Progress { get; }
        public abstract string Instruction { get; }
        /// <summary>World position where the next valid action happens (used by hints).</summary>
        public abstract Vector3 HintPoint { get; }
        public event Action<RepairMiniGame> OnCompleted;
        public float SecondsSinceProgress { get; protected set; }
        float lastProgress;
        Vector3 planePoint, lastPointer;
        bool begun;

        public void Begin(RepairContext ctx)
        {
            Ctx = ctx;
            planePoint = HintPointSafe();
            OnBegin();
            planePoint = HintPointSafe(); // the work plane passes through the real target (known only after OnBegin)
            begun = true;
            lastProgress = Progress;
        }

        Vector3 HintPointSafe() { try { return HintPoint; } catch { return Ctx.toy.transform.position; } }

        protected abstract void OnBegin();
        protected abstract void OnTick(PointerFrame f, Vector3 pointerWorld, bool onSurface, Vector3 normal);
        protected virtual void OnCancel() { }
        protected virtual void OnFinished() { }

        void Update()
        {
            if (!begun || Completed || Ctx == null) return;
            var f = Ctx.pointer.World;
            var ray = Ctx.cam.ScreenPointToRay(f.position);
            Vector3 world; bool onSurface = false; Vector3 n = -Ctx.cam.transform.forward;
            if (Ctx.probe != null && Ctx.probe.Raycast(ray, out var hp, out var hn)) { world = hp; n = hn; onSurface = true; }
            else
            {
                var plane = new Plane(-Ctx.cam.transform.forward, planePoint);
                world = plane.Raycast(ray, out float d) ? ray.GetPoint(d) : planePoint;
            }
            if (onSurface) planePoint = Vector3.Lerp(planePoint, world, 0.5f);
            if (Ctx.tool != null) Ctx.tool.Follow(world, Ctx.cam);
            lastPointer = world;
            OnTick(f, world, onSurface, n);
            if (Mathf.Abs(Progress - lastProgress) > 0.0005f) { lastProgress = Progress; SecondsSinceProgress = 0f; }
            else SecondsSinceProgress += Time.unscaledDeltaTime;
        }

        public Vector3 LastPointer => lastPointer;

        protected void Finish()
        {
            if (Completed) return;
            Completed = true;
            OnFinished();
            OnCompleted?.Invoke(this);
        }

        public void Cancel()
        {
            if (!Completed) OnCancel();
            Destroy(this);
        }

        /// <summary>Test hook: act as if the pointer were at a world point (no camera needed).</summary>
        protected void Hit(RepairContext c) { }
    }
}
