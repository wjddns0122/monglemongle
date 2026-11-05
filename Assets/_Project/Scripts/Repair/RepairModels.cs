using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>Pure progress rules for each repair (no scene access, EditMode-testable). Progress is always in [0,1].</summary>
    public class ThornModel
    {
        public Vector3 GrabPoint;
        public Vector3 RemovalDir;      // unit vector pointing away from the embedded point
        public float GrabRadius = 0.05f;
        public float MinHold = 0.12f;
        public float MaxAngleDeg = 55f;
        public float RequiredPull = 0.07f;
        public float SpringBack = 0.5f;   // fraction of pull kept when released early (never a big loss)

        public bool Grabbed { get; private set; }
        public bool Complete { get; private set; }
        public float Pull { get; private set; }
        public float Progress => Mathf.Clamp01(Pull / RequiredPull);
        float hold; Vector3 grabStart;

        public ThornModel(Vector3 grabPoint, Vector3 removalDir) { GrabPoint = grabPoint; RemovalDir = removalDir.normalized; }

        public bool TryGrab(Vector3 pointerWorld)
        {
            if (Complete || Grabbed) return false;
            if ((pointerWorld - GrabPoint).magnitude > GrabRadius) return false;
            Grabbed = true; hold = 0f; grabStart = pointerWorld - RemovalDir * Pull; // continue from current pull
            return true;
        }

        /// <summary>Returns true when this call completed the removal (exactly once).</summary>
        public bool Drag(Vector3 pointerWorld, float dt)
        {
            if (!Grabbed || Complete) return false;
            hold += dt;
            if (hold < MinHold) return false;
            var delta = pointerWorld - grabStart;
            float along = Vector3.Dot(delta, RemovalDir);
            float angle = delta.sqrMagnitude < 1e-8f ? 0f : Vector3.Angle(delta, RemovalDir);
            if (along > 0f && angle <= MaxAngleDeg) Pull = Mathf.Clamp(along, 0f, RequiredPull);
            else if (along <= 0f) Pull = 0f; // pushing back eases the thorn back in, never below zero
            if (Pull >= RequiredPull) { Complete = true; Grabbed = false; return true; }
            return false;
        }

        public void Release()
        {
            if (!Grabbed) return;
            Grabbed = false;
            if (!Complete) Pull *= SpringBack;
        }
    }

    public class DirtModel
    {
        public readonly List<Vector3> Positions = new List<Vector3>();
        public readonly List<float> Radii = new List<float>();
        public readonly List<float> Health = new List<float>();
        public float Power = 1f;
        public bool Complete { get; private set; }
        public float FinishThreshold = 0.03f;

        public void Add(Vector3 pos, float radius) { Positions.Add(pos); Radii.Add(radius); Health.Add(1f); }
        public int Count => Positions.Count;

        public float Progress
        {
            get
            {
                if (Health.Count == 0) return 1f;
                float s = 0; foreach (var h in Health) s += h;
                return Mathf.Clamp01(1f - s / Health.Count);
            }
        }

        /// <summary>True when the sponge touches any remaining dirt (only then does movement count).</summary>
        public bool Overlaps(Vector3 p, float spongeRadius)
        {
            for (int i = 0; i < Positions.Count; i++)
                if (Health[i] > 0f && (Positions[i] - p).magnitude <= spongeRadius + Radii[i]) return true;
            return false;
        }

        /// <summary>Rub: removes dirt near p proportionally to valid moved distance. Returns true when finished by this call.</summary>
        public bool Rub(Vector3 p, float spongeRadius, float movedDistance)
        {
            if (Complete || movedDistance <= 0f) return false;
            float amount = movedDistance * Power;
            for (int i = 0; i < Positions.Count; i++)
            {
                if (Health[i] <= 0f) continue;
                if ((Positions[i] - p).magnitude <= spongeRadius + Radii[i])
                    Health[i] = Mathf.Max(0f, Health[i] - amount / (Radii[i] * 2f + 0.01f) * 0.12f);
            }
            if (Progress >= 1f - FinishThreshold)
            {
                for (int i = 0; i < Health.Count; i++) Health[i] = 0f;
                Complete = true; return true;
            }
            return false;
        }
    }

    public class StitchModel
    {
        public readonly List<Vector3> Points;
        public float Radius = 0.035f;
        public int Done { get; private set; }
        public bool Complete { get; private set; }
        public float Progress => Points.Count == 0 ? 1f : (float)Done / Points.Count;
        public int NextIndex => Done;

        public enum Result { None, Stitched, WrongPoint, Finished }

        public StitchModel(IEnumerable<Vector3> points) { Points = new List<Vector3>(points); }

        public Result Move(Vector3 pointerWorld)
        {
            if (Complete) return Result.None;
            if ((pointerWorld - Points[Done]).magnitude <= Radius)
            {
                Done++;
                if (Done >= Points.Count) { Complete = true; return Result.Finished; }
                return Result.Stitched;
            }
            for (int i = Done + 1; i < Points.Count; i++)
                if ((pointerWorld - Points[i]).magnitude <= Radius * 0.8f) return Result.WrongPoint;
            return Result.None;
        }
    }

    public class CottonModel
    {
        public Vector3 FillPoint;
        public float Radius = 0.05f;
        public float HoldSeconds = 4f;
        public bool Complete { get; private set; }
        public float Progress { get; private set; }
        public bool Filling { get; private set; }

        public CottonModel(Vector3 fillPoint) { FillPoint = fillPoint; }

        /// <summary>Returns true when this tick completed the fill (exactly once). Never reduces progress.</summary>
        public bool Tick(Vector3 nozzleWorld, bool held, float dt)
        {
            Filling = false;
            if (Complete) return false;
            if (!held || (nozzleWorld - FillPoint).magnitude > Radius) return false;
            Filling = true;
            Progress = Mathf.Clamp01(Progress + dt / HoldSeconds);
            if (Progress >= 1f) { Complete = true; Filling = false; return true; }
            return false;
        }
    }

    /// <summary>
    /// Battery swap (D-009): pull the old cell out like a thorn (progress 0–0.5), then hold the new cell on the slot until it
    /// clicks in (0.5–1). Leaving the slot early only restarts the short click hold.
    /// </summary>
    public class BatteryModel
    {
        public readonly ThornModel Pull;
        public Vector3 SlotPoint;
        public float SlotRadius = 0.05f;
        public float ClickHold = 0.5f;
        public bool Removed => Pull.Complete;
        public bool Complete { get; private set; }
        public bool Inserting { get; private set; }
        public float Hold { get; private set; }
        public float Progress => Complete ? 1f : Removed ? 0.5f + 0.5f * Mathf.Clamp01(Hold / ClickHold) : 0.5f * Pull.Progress;

        public BatteryModel(Vector3 grabPoint, Vector3 removalDir, Vector3 slotPoint) { Pull = new ThornModel(grabPoint, removalDir); SlotPoint = slotPoint; }

        public bool TryGrab(Vector3 pointerWorld) => !Removed && Pull.TryGrab(pointerWorld);
        /// <summary>Phase 1. True when this call pulled the old cell out (exactly once).</summary>
        public bool Drag(Vector3 pointerWorld, float dt) => !Removed && Pull.Drag(pointerWorld, dt);
        public void Release() => Pull.Release();

        /// <summary>Phase 2. True when this tick clicked the new cell in (exactly once). Nothing happens before the old one is out.</summary>
        public bool Insert(Vector3 cellWorld, bool held, float dt)
        {
            Inserting = false;
            if (!Removed || Complete) return false;
            if (!held || (cellWorld - SlotPoint).magnitude > SlotRadius) { Hold = 0f; return false; }
            Inserting = true;
            Hold += dt;
            if (Hold >= ClickHold) { Complete = true; Inserting = false; return true; }
            return false;
        }
    }
}
