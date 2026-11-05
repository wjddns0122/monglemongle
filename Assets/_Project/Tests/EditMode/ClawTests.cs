using System.Collections.Generic;
using Mongle.Village;
using NUnit.Framework;

namespace Mongle.Tests
{
    /// <summary>The claw machine (D-014): five hearts back one per 30 minutes, and a round that only wins near the marker.</summary>
    public class ClawTests
    {
        [Test]
        public void HeartsComeBackOnePerHalfHourAndNeverPastFive()
        {
            long now = 1_000_000; var h = new ClawHearts(() => now);
            Assert.AreEqual(5, h.Count); Assert.AreEqual(0, h.SecondsToNext);
            for (int i = 0; i < 5; i++) Assert.IsTrue(h.TryUse());
            Assert.IsFalse(h.TryUse(), "no hearts left"); Assert.AreEqual(0, h.Count);
            Assert.AreEqual(1800, h.SecondsToNext);
            now += 1799; Assert.AreEqual(0, h.Count); Assert.AreEqual(1, h.SecondsToNext);
            now += 1; h.Refresh(); Assert.AreEqual(1, h.Count);
            now += 3 * 1800 + 100; h.Refresh(); Assert.AreEqual(4, h.Count); Assert.AreEqual(1700, h.SecondsToNext);
            now += 10 * 3600; h.Refresh(); Assert.AreEqual(5, h.Count); Assert.AreEqual(0, h.SecondsToNext);
        }

        [Test]
        public void TheFirstMissingHeartStartsTheTimerAndTheRestKeepRunning()
        {
            long now = 5_000; var h = new ClawHearts(() => now);
            now += 100000; Assert.IsTrue(h.TryUse());                       // a full set: the clock starts now, not 100000 s ago
            Assert.AreEqual(4, h.Count); Assert.AreEqual(1800, h.SecondsToNext);
            now += 900; Assert.IsTrue(h.TryUse()); Assert.AreEqual(3, h.Count);
            Assert.AreEqual(900, h.SecondsToNext, "using another heart does not restart the charge");
            now += 900; h.Refresh(); Assert.AreEqual(4, h.Count);
        }

        [Test]
        public void ASavedClockSurvivesAndASetBackClockGivesNothing()
        {
            long now = 10_000; var h = new ClawHearts(() => now);
            for (int i = 0; i < 5; i++) h.TryUse();
            long saved = h.LastUnix; int count = h.Count;
            now += 3600 + 50;
            var h2 = new ClawHearts(() => now); h2.Restore(count, saved);
            Assert.AreEqual(2, h2.Count, "an hour away is two hearts");
            now -= 99999; h2.Refresh(); Assert.AreEqual(2, h2.Count, "no hearts for a clock set back");
            var fresh = new ClawHearts(() => now); fresh.Restore(-1, 0);
            Assert.AreEqual(5, fresh.Count, "an older save starts full");
        }

        [Test]
        public void SteeringMovesTheClawAndStopsAtTheWalls()
        {
            var r = new ClawRound(0.2f, 0.9f);
            Assert.AreEqual(ClawPhase.Move, r.Phase); Assert.AreEqual(0.2f, r.X); Assert.AreEqual(0.9f, r.Y);
            r.Steer(1f, 0f, 0.4f); Assert.AreEqual(0.2f + 0.5f * 0.4f, r.X, 1e-5f); Assert.AreEqual(0.9f, r.Y);
            r.Steer(0f, -1f, 10f); Assert.AreEqual(0f, r.Y, "stops at the back wall");
            r.Steer(5f, 0f, 10f); Assert.AreEqual(1f, r.X, "stops at the right wall");
            r.Release(); Assert.AreEqual(ClawPhase.Drop, r.Phase);
            r.Steer(-1f, 1f, 1f); Assert.AreEqual(1f, r.X, "cannot steer once it is dropping");
            r.Finish(true); Assert.AreEqual(ClawPhase.Done, r.Phase); Assert.IsTrue(r.Won);
        }

        [Test]
        public void TheClawTakesTheHighestCapsuleItReaches()
        {
            var caps = new List<UnityEngine.Vector3>
            {
                new UnityEngine.Vector3(0.00f, 0.90f, 0.00f),   // under the middle, low
                new UnityEngine.Vector3(0.03f, 1.03f, 0.00f),   // on top of it, a bit off
                new UnityEngine.Vector3(0.40f, 1.00f, 0.00f),   // far away
            };
            int i = ClawGrab.PickTarget(caps, new UnityEngine.Vector3(0f, 0f, 0f), out float d);
            Assert.AreEqual(1, i); Assert.AreEqual(0.03f, d, 1e-4f);
            Assert.AreEqual(-1, ClawGrab.PickTarget(caps, new UnityEngine.Vector3(-0.3f, 0f, 0.3f), out _), "nothing within reach");
        }

        [Test]
        public void TheGripIsWeakSoItOftenLetsGoOnTheWay()
        {
            Assert.AreEqual(ClawGrab.Outcome.Miss, ClawGrab.Decide(ClawGrab.ReachRadius + 0.01f, 0f), "out of reach");
            var rng = new System.Random(2); int n = 6000;
            float Rate(float d) { int c = 0; for (int i = 0; i < n; i++) if (ClawGrab.Decide(d, (float)rng.NextDouble()) == ClawGrab.Outcome.Catch) c++; return c / (float)n; }
            float centre = Rate(0.0f), mid = Rate(0.05f), edge = Rate(0.08f);
            Assert.That(centre, Is.InRange(0.35f, 0.55f), "right under it: not even every second time");
            Assert.Less(mid, centre); Assert.Less(edge, mid);
            Assert.That(edge, Is.InRange(0.05f, 0.2f), "at the edge it rarely brings one home");
            Assert.That(ClawGrab.SlipPoint(0f), Is.EqualTo(0.12f).Within(1e-5f)); Assert.That(ClawGrab.SlipPoint(1f), Is.EqualTo(0.8f).Within(1e-5f));
        }

        [Test]
        public void ATakenCapsuleComesBackOnePerHalfHour()
        {
            long now = 50_000; var s = new ClawStock(() => now);
            Assert.IsFalse(s.IsGone(7)); Assert.AreEqual(0, s.SecondsToNext);
            s.Take(7); now += 600; s.Take(12);
            Assert.IsTrue(s.IsGone(7) && s.IsGone(12));
            Assert.AreEqual(1200, s.SecondsToNext, "the 30 minutes started with the first capsule");
            now += 1200; s.Refresh();
            Assert.IsFalse(s.IsGone(7), "the oldest comes back first"); Assert.IsTrue(s.IsGone(12));
            now += 1799; s.Refresh(); Assert.IsTrue(s.IsGone(12));
            now += 1; s.Refresh(); Assert.IsFalse(s.IsGone(12)); Assert.AreEqual(0, s.Removed.Count);
            s.Take(3); now += 99_999; s.Refresh(); Assert.AreEqual(0, s.Removed.Count, "a long time away: all back");
        }

        [Test]
        public void TheStockSurvivesTheSaveAndAClockSetBackBringsNothing()
        {
            long now = 90_000; var s = new ClawStock(() => now);
            s.Take(5); s.Take(9); s.Take(1);
            var s2 = new ClawStock(() => now + 3600 + 30);
            s2.Restore(s.Serialize(), s.LastUnix);
            Assert.IsFalse(s2.IsGone(5) || s2.IsGone(9), "an hour away: two capsules back"); Assert.IsTrue(s2.IsGone(1));
            var s3 = new ClawStock(() => now - 5000); s3.Restore("4,x,4,-2,8", now);
            CollectionAssert.AreEqual(new[] { 4, 8 }, s3.Removed, "broken entries dropped, no repeats");
            s3.Refresh(); Assert.AreEqual(2, s3.Removed.Count, "no capsules come back for a clock set back");
        }

        [Test]
        public void ThePrizeIsOneOfTheShopItems()
        {
            var ids = new List<string> { "A", "B", "C" }; var rng = new System.Random(1); var seen = new HashSet<string>();
            for (int i = 0; i < 200; i++) seen.Add(ClawRewards.Pick(ids, rng));
            CollectionAssert.AreEquivalent(ids, seen);
            Assert.IsNull(ClawRewards.Pick(new List<string>(), rng));
        }
    }
}
