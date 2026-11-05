using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>A closed path measured along its length (the cake train's loop, D-012): positions wrap round.</summary>
    public class LoopPath
    {
        readonly Vector3[] pts; readonly float[] acc;
        public float Length { get; }
        public LoopPath(IList<Vector3> points)
        {
            int n = points.Count;
            pts = new Vector3[n + 1]; acc = new float[n + 1];
            for (int i = 0; i < n; i++) pts[i] = points[i];
            pts[n] = points[0];
            for (int i = 1; i <= n; i++) acc[i] = acc[i - 1] + (pts[i] - pts[i - 1]).magnitude;
            Length = acc[n];
        }
        public void At(float d, out Vector3 p, out Vector3 dir)
        {
            d = Mathf.Repeat(d, Length);
            int lo = 0, hi = acc.Length - 1;               // binary search for the segment
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (acc[mid] <= d) lo = mid; else hi = mid; }
            float seg = acc[hi] - acc[lo]; float k = seg > 1e-6f ? (d - acc[lo]) / seg : 0f;
            p = Vector3.Lerp(pts[lo], pts[hi], k); dir = (pts[hi] - pts[lo]).normalized;
        }
        /// <summary>Distance along the loop of the point nearest to q.</summary>
        public float Nearest(Vector3 q)
        {
            float best = float.MaxValue, at = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var ab = pts[i + 1] - a; float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                float dd = (a + ab * t - q).sqrMagnitude;
                if (dd < best) { best = dd; at = acc[i] + ab.magnitude * t; }
            }
            return at;
        }

        /// <summary>
        /// A smooth closed curve through the given points (Catmull-Rom, D-012 follow-up: the train winds through the village),
        /// sampled about every step metres.
        /// </summary>
        public static List<Vector3> CatmullRomClosed(IList<Vector3> w, float step)
        {
            var o = new List<Vector3>(); int n = w.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = w[(i - 1 + n) % n], p1 = w[i], p2 = w[(i + 1) % n], p3 = w[(i + 2) % n];
                int k = Mathf.Max(2, Mathf.CeilToInt((p2 - p1).magnitude / step));
                for (int j = 0; j < k; j++)
                {
                    float t = j / (float)k, t2 = t * t, t3 = t2 * t;
                    o.Add(0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            return o;
        }

        /// <summary>A rounded rectangle on the ground (counter-clockwise seen from above), points about every step metres.</summary>
        public static List<Vector3> RoundedRect(Vector2 center, Vector2 half, float radius, float step)
        {
            var o = new List<Vector3>();
            radius = Mathf.Clamp(radius, 0.01f, Mathf.Min(half.x, half.y));
            var cs = new[] { new Vector2(half.x - radius, -half.y + radius), new Vector2(half.x - radius, half.y - radius), new Vector2(-half.x + radius, half.y - radius), new Vector2(-half.x + radius, -half.y + radius) };
            for (int c = 0; c < 4; c++)
            {
                float a0 = -90f + 90f * c;
                int arcN = Mathf.Max(2, Mathf.CeilToInt(radius * Mathf.PI * 0.5f / step));
                for (int i = 0; i <= arcN; i++)
                {
                    float a = (a0 + 90f * i / arcN) * Mathf.Deg2Rad;
                    var p = center + cs[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                    o.Add(new Vector3(p.x, 0f, p.y));
                }
                // straight to the next corner
                var end = center + cs[c] + new Vector2(Mathf.Cos((a0 + 90f) * Mathf.Deg2Rad), Mathf.Sin((a0 + 90f) * Mathf.Deg2Rad)) * radius;
                var nc = cs[(c + 1) % 4]; float na = (a0 + 90f) * Mathf.Deg2Rad;
                var next = center + nc + new Vector2(Mathf.Cos(na), Mathf.Sin(na)) * radius;
                int sn = Mathf.FloorToInt((next - end).magnitude / step);
                for (int i = 1; i < sn; i++) { var p = Vector2.Lerp(end, next, (float)i / sn); o.Add(new Vector3(p.x, 0f, p.y)); }
            }
            return o;
        }
    }

    public enum TrainPhase { Dwell, Running }

    /// <summary>
    /// The cake train's trips (D-012), pure: it waits at the station, then runs one full lap (speeding up, cruising, slowing down
    /// to stop exactly at the station again) and counts the trip. Nothing Unity, so it is unit-tested.
    /// </summary>
    public class TrainCycle
    {
        public float LapLength, MaxSpeed = 1.6f, Accel = 0.45f, MinDwell = 8f;
        public TrainPhase Phase { get; private set; } = TrainPhase.Dwell;
        public float Travelled { get; private set; }   // since leaving the station on this lap
        public float Speed { get; private set; }
        public float PhaseTime { get; private set; }
        public int Trips { get; private set; }
        /// <summary>Total distance ever driven (wheels turn by it).</summary>
        public float Odometer { get; private set; }
        public TrainCycle(float lap) { LapLength = lap; }

        /// <summary>One step. blocked = someone stands on the track just ahead (a level crossing): slow down and wait.</summary>
        public void Tick(float dt, bool mayDepart, bool blocked = false)
        {
            PhaseTime += dt;
            if (Phase == TrainPhase.Dwell)
            {
                if (PhaseTime >= MinDwell && mayDepart) { Phase = TrainPhase.Running; PhaseTime = 0f; Travelled = 0f; Speed = 0f; }
                return;
            }
            float remaining = LapLength - Travelled;
            float brake = Mathf.Sqrt(2f * Accel * Mathf.Max(0f, remaining));    // fastest speed that still stops at the station
            if (blocked) { Speed = Mathf.Max(0f, Speed - 3f * Accel * dt); Holding = Speed <= 0f; }
            else { Holding = false; Speed = Mathf.Min(MaxSpeed, Speed + Accel * dt, Mathf.Max(brake, 0.08f)); }
            float step = Mathf.Min(Speed * dt, remaining);
            Travelled += step; Odometer += step;
            if (LapLength - Travelled <= 1e-4f) { Travelled = 0f; Speed = 0f; Trips++; Phase = TrainPhase.Dwell; PhaseTime = 0f; }
        }
        public bool AtStation => Phase == TrainPhase.Dwell;
        /// <summary>Stopped on the way, waiting for a crossing to clear.</summary>
        public bool Holding { get; private set; }
    }
}
