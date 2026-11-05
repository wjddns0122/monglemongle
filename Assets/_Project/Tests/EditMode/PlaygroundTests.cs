using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The playground (D-011): swings pump up and calm down, the seesaw tips toward its riders, slides follow their path.</summary>
    public class PlaygroundTests
    {
        static void Run(SwingMotion m, float seconds, bool riding) { for (float t = 0; t < seconds; t += 0.02f) m.Tick(0.02f, riding); }

        [Test]
        public void SwingPumpsUpWhileRidingAndCalmsDownToLetGo()
        {
            var m = new SwingMotion();
            Assert.IsTrue(m.Still, "an empty swing can be boarded");
            Run(m, 5f, true);
            Assert.AreEqual(m.RideAmp, m.Amp, 1e-4f);
            Assert.IsFalse(m.Still, "nobody hops off a swinging swing");
            float peak = 0f; for (int i = 0; i < 200; i++) { m.Tick(0.02f, true); peak = Mathf.Max(peak, Mathf.Abs(m.Angle)); }
            Assert.Greater(peak, m.RideAmp * 0.9f);
            Run(m, 5f, false);
            Assert.IsTrue(m.Still);
            Assert.LessOrEqual(Mathf.Abs(m.Angle), m.IdleAmp + 1e-4f);
        }

        [Test]
        public void SeesawTipsTowardItsRiders()
        {
            var s = new SeesawMotion();
            s.Snap(false, false); Assert.Greater(s.Angle, 0f, "at rest end A sits on the ground");
            for (int i = 0; i < 200; i++) s.Tick(0.02f, false, true);
            Assert.Less(s.Angle, -0.1f, "a lone rider on B tips B down");
            float lo = 1f, hi = -1f;
            for (int i = 0; i < 400; i++) { s.Tick(0.02f, true, true); lo = Mathf.Min(lo, s.Angle); hi = Mathf.Max(hi, s.Angle); }
            Assert.Less(lo, -0.1f); Assert.Greater(hi, 0.1f);
        }

        [Test]
        public void PolyPathWalksByLength()
        {
            var p = new PolyPath(new[] { Vector3.zero, new Vector3(1, 0, 0), new Vector3(1, 0, 2) });
            Assert.AreEqual(3f, p.Length, 1e-5f);
            Assert.That(Vector3.Distance(p.At(0.5f), new Vector3(0.5f, 0, 0)), Is.LessThan(1e-5f));
            p.At(2f, out var q, out var d);
            Assert.That(Vector3.Distance(q, new Vector3(1, 0, 1)), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(d, Vector3.forward), Is.LessThan(1e-5f));
            Assert.That(Vector3.Distance(p.At(99f), new Vector3(1, 0, 2)), Is.LessThan(1e-5f), "clamped at the end");
        }
    }
}
