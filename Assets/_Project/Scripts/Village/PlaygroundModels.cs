using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// One playground swing (D-011), pure motion: it sways a little when empty, pumps up to a big swing while a toy rides,
    /// and calms down before the toy may hop off. Angle in radians about the top bar. No Unity objects, so it is unit-tested.
    /// </summary>
    public class SwingMotion
    {
        public float Period = 2.4f, RideAmp = 0.5f, IdleAmp = 0.03f, AmpRate = 0.22f, StillAmp = 0.08f;
        public float Amp { get; private set; }
        public float Phase { get; private set; }
        public void Tick(float dt, bool riding)
        {
            Amp = Mathf.MoveTowards(Amp, riding ? RideAmp : IdleAmp, AmpRate * dt);
            Phase = Mathf.Repeat(Phase + dt * 2f * Mathf.PI / Period, 2f * Mathf.PI);
        }
        public float Angle => Amp * Mathf.Sin(Phase);
        /// <summary>Slow enough to climb in or out.</summary>
        public bool Still => Amp <= StillAmp;
    }

    /// <summary>
    /// The pony seesaw (D-011), pure motion: at rest end A leans on the ground; one rider tips their own end down with a little
    /// bounce; two riders rock it up and down in turn. Angle in radians, positive = end A down.
    /// </summary>
    public class SeesawMotion
    {
        public float RestTilt = 0.15f, RockAmp = 0.15f, Period = 2.6f, Follow = 3f;   // the bumpers touch the sand at about 0.166 rad
        public float Angle { get; private set; }
        float t;
        public float Target(bool a, bool b)
        {
            if (a && b) return RockAmp * Mathf.Sin(t * 2f * Mathf.PI / Period);
            if (a) return RestTilt - 0.02f * Mathf.Abs(Mathf.Sin(t * 5f));
            if (b) return -RestTilt + 0.02f * Mathf.Abs(Mathf.Sin(t * 5f));
            return RestTilt;
        }
        public void Tick(float dt, bool a, bool b)
        {
            t += dt;
            Angle = Mathf.Lerp(Angle, Target(a, b), 1f - Mathf.Exp(-Follow * dt));
        }
        public void Snap(bool a, bool b) => Angle = Target(a, b);
    }

    /// <summary>A path of points measured along its length (slides, D-011): the point a given distance down it.</summary>
    public class PolyPath
    {
        readonly Vector3[] pts; readonly float[] acc;
        public float Length { get; }
        public PolyPath(Vector3[] points)
        {
            pts = points; acc = new float[points.Length];
            for (int i = 1; i < points.Length; i++) acc[i] = acc[i - 1] + (points[i] - points[i - 1]).magnitude;
            Length = acc.Length > 0 ? acc[acc.Length - 1] : 0f;
        }
        public Vector3 At(float d) { At(d, out var p, out _); return p; }
        public void At(float d, out Vector3 p, out Vector3 dir)
        {
            if (pts.Length == 1) { p = pts[0]; dir = Vector3.forward; return; }
            d = Mathf.Clamp(d, 0f, Length);
            int i = 1; while (i < pts.Length - 1 && acc[i] < d) i++;
            float seg = acc[i] - acc[i - 1];
            float k = seg > 1e-6f ? (d - acc[i - 1]) / seg : 0f;
            p = Vector3.Lerp(pts[i - 1], pts[i], k);
            dir = (pts[i] - pts[i - 1]).normalized;
        }
    }
}
