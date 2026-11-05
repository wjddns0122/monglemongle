using System.Linq;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The cake train (D-012): its loop round the park, and laps that always end back at the station.</summary>
    public class TrainTests
    {
        [Test]
        public void RoundedRectLoopIsClosedAndEven()
        {
            var pts = LoopPath.RoundedRect(new Vector2(0f, 2.9f), new Vector2(18.4f, 13f), 1.2f, 0.5f);
            var loop = new LoopPath(pts);
            float expect = 2f * (36.8f + 26f) - (8f - 2f * Mathf.PI) * 1.2f;
            Assert.AreEqual(expect, loop.Length, 0.2f);
            for (int i = 0; i < pts.Count; i++)
            {
                float step = (pts[(i + 1) % pts.Count] - pts[i]).magnitude;
                Assert.That(step, Is.InRange(0.05f, 0.75f), "gap at " + i);
            }
            Assert.That(pts.Max(p => p.x), Is.EqualTo(18.4f).Within(1e-3f));
            Assert.That(pts.Min(p => p.z), Is.EqualTo(2.9f - 13f).Within(1e-3f));
            loop.At(loop.Length + 1f, out var a, out _); loop.At(1f, out var b, out _);
            Assert.That(Vector3.Distance(a, b), Is.LessThan(1e-3f), "wraps round");
            Assert.AreEqual(loop.Nearest(b), 1f, 0.05f);
        }

        [Test]
        public void CatmullRomLoopIsSmoothAndClosed()
        {
            var w = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 8), new Vector3(0, 0, 8) };
            var pts = LoopPath.CatmullRomClosed(w, 0.25f);
            foreach (var q in w) Assert.IsTrue(pts.Any(p => Vector3.Distance(p, q) < 1e-4f), "passes through every waypoint");
            for (int i = 0; i < pts.Count; i++) Assert.That((pts[(i + 1) % pts.Count] - pts[i]).magnitude, Is.InRange(0.01f, 0.4f));
            Assert.That(new LoopPath(pts).Length, Is.InRange(30f, 40f));
        }

        [Test]
        public void TheTrainWaitsForACrossingToClear()
        {
            var c = new TrainCycle(50f);
            for (int i = 0; i < 200; i++) c.Tick(0.05f, true);
            for (int i = 0; i < 100; i++) c.Tick(0.05f, true);
            Assert.Greater(c.Speed, 0.5f);
            float at = c.Travelled;
            for (int i = 0; i < 100; i++) c.Tick(0.05f, true, true);
            Assert.IsTrue(c.Holding); Assert.AreEqual(0f, c.Speed);
            float stoppedAt = c.Travelled; c.Tick(0.05f, true, true);
            Assert.AreEqual(stoppedAt, c.Travelled, "stands still while blocked");
            Assert.Less(stoppedAt - at, 3f, "stops within a short distance");
            int guard = 0; while (!c.AtStation && guard++ < 100000) c.Tick(0.05f, true);
            Assert.AreEqual(1, c.Trips); Assert.AreEqual(50f, c.Odometer, 1e-3f);
        }

        [Test]
        public void ALapEndsExactlyAtTheStation()
        {
            var c = new TrainCycle(120f);
            for (int i = 0; i < 200; i++) c.Tick(0.05f, false);   // 10 s: past the 8 s stop
            Assert.IsTrue(c.AtStation, "waits until it may go");
            float t = 0f; int guard = 0;
            c.Tick(0.05f, true); Assert.IsFalse(c.AtStation);
            while (!c.AtStation && guard++ < 100000) { c.Tick(0.05f, true); t += 0.05f; Assert.LessOrEqual(c.Speed, c.MaxSpeed + 1e-4f); }
            Assert.AreEqual(1, c.Trips);
            Assert.AreEqual(0f, c.Travelled);
            Assert.AreEqual(120f, c.Odometer, 1e-3f);
            Assert.That(t, Is.InRange(120f / c.MaxSpeed, 120f / c.MaxSpeed + 10f));
            c.Tick(0.05f, true); Assert.IsTrue(c.AtStation, "it waits at the station again before the next lap");
        }
    }
}
