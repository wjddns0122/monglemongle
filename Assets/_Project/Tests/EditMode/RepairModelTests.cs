using System.Linq;
using Mongle.Repair;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    public class RepairModelTests
    {
        // ---------- Thorn ----------
        ThornModel Thorn() => new ThornModel(Vector3.zero, Vector3.up) { GrabRadius = 0.05f, MinHold = 0.1f, RequiredPull = 0.1f, MaxAngleDeg = 50f };

        [Test] public void ThornGrabFailsOutsideRadius() { var m = Thorn(); Assert.IsFalse(m.TryGrab(new Vector3(0.2f, 0, 0))); Assert.IsFalse(m.Grabbed); }

        [Test]
        public void ThornNeedsMinHoldBeforeProgress()
        {
            var m = Thorn(); m.TryGrab(Vector3.zero);
            m.Drag(new Vector3(0, 0.05f, 0), 0.02f);
            Assert.AreEqual(0f, m.Progress);
            m.Drag(new Vector3(0, 0.05f, 0), 0.2f);
            Assert.AreEqual(0.5f, m.Progress, 1e-4f);
        }

        [Test]
        public void ThornWrongDirectionDoesNotProgress()
        {
            var m = Thorn(); m.TryGrab(Vector3.zero);
            m.Drag(new Vector3(0.05f, 0.005f, 0), 0.5f); // sideways
            Assert.AreEqual(0f, m.Progress);
            m.Drag(new Vector3(0.05f, -0.05f, 0), 0.5f); // backwards
            Assert.AreEqual(0f, m.Progress);
        }

        [Test]
        public void ThornCompletesExactlyOnceAndClamps()
        {
            var m = Thorn(); m.TryGrab(Vector3.zero);
            int fired = 0;
            for (int i = 0; i < 10; i++) { if (m.Drag(new Vector3(0, 0.5f, 0), 0.1f)) fired++; }
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1f, m.Progress);
            Assert.IsTrue(m.Complete);
            Assert.IsFalse(m.TryGrab(Vector3.zero));
        }

        [Test]
        public void ThornReleaseSpringsBackButKeepsHalf()
        {
            var m = Thorn(); m.TryGrab(Vector3.zero);
            m.Drag(new Vector3(0, 0.08f, 0), 0.5f);
            Assert.AreEqual(0.8f, m.Progress, 1e-4f);
            m.Release();
            Assert.AreEqual(0.4f, m.Progress, 1e-4f);
            Assert.IsFalse(m.Grabbed);
        }

        // ---------- Dirt ----------
        DirtModel Dirt()
        {
            var d = new DirtModel { Power = 1f };
            d.Add(new Vector3(0, 0, 0), 0.02f); d.Add(new Vector3(0.05f, 0, 0), 0.02f); d.Add(new Vector3(0, 0.05f, 0), 0.02f);
            return d;
        }

        [Test] public void DirtStartsWithZeroProgress() { Assert.AreEqual(0f, Dirt().Progress); }
        [Test] public void DirtOverlapOnlyNearBlobs() { var d = Dirt(); Assert.IsTrue(d.Overlaps(Vector3.zero, 0.01f)); Assert.IsFalse(d.Overlaps(new Vector3(1, 1, 0), 0.01f)); }

        [Test]
        public void DirtRubCleansOnlyNearestFirst()
        {
            var d = Dirt();
            d.Rub(Vector3.zero, 0.02f, 0.05f);
            Assert.Less(d.Health[0], 1f);
            Assert.AreEqual(1f, d.Health[1]);
            Assert.AreEqual(1f, d.Health[2]);
        }

        [Test]
        public void DirtNoMovementOrFarAwayNoProgress()
        {
            var d = Dirt();
            d.Rub(Vector3.zero, 0.02f, 0f);
            d.Rub(new Vector3(5, 5, 5), 0.02f, 1f);
            Assert.AreEqual(0f, d.Progress);
        }

        [Test]
        public void DirtCompletesOnceAndAllClean()
        {
            var d = Dirt(); int fired = 0;
            for (int i = 0; i < 400; i++)
                foreach (var p in d.Positions.ToArray()) if (d.Rub(p, 0.02f, 0.05f)) fired++;
            Assert.AreEqual(1, fired);
            Assert.IsTrue(d.Complete);
            Assert.IsTrue(d.Health.All(h => h == 0f));
            Assert.AreEqual(1f, d.Progress);
        }

        // ---------- Stitch ----------
        StitchModel Stitch() => new StitchModel(new[] { new Vector3(0, 0, 0), new Vector3(0.05f, -0.03f, 0), new Vector3(0, -0.06f, 0) }) { Radius = 0.02f };

        [Test]
        public void StitchRequiresOrder()
        {
            var s = Stitch();
            Assert.AreEqual(StitchModel.Result.WrongPoint, s.Move(new Vector3(0, -0.06f, 0)));
            Assert.AreEqual(0, s.Done);
            Assert.AreEqual(StitchModel.Result.Stitched, s.Move(Vector3.zero));
            Assert.AreEqual(1, s.Done);
        }

        [Test]
        public void StitchFinishesOnceAndNoDoubleCount()
        {
            var s = Stitch();
            Assert.AreEqual(StitchModel.Result.Stitched, s.Move(Vector3.zero));
            Assert.AreEqual(StitchModel.Result.None, s.Move(Vector3.zero)); // same spot does not count again
            Assert.AreEqual(StitchModel.Result.Stitched, s.Move(new Vector3(0.05f, -0.03f, 0)));
            Assert.AreEqual(StitchModel.Result.Finished, s.Move(new Vector3(0, -0.06f, 0)));
            Assert.AreEqual(StitchModel.Result.None, s.Move(new Vector3(0, -0.06f, 0)));
            Assert.AreEqual(1f, s.Progress);
        }

        [Test]
        public void StitchRadiusFitsAZigzagSeamSoOneDragTakesOnePoint()
        {
            // Mongi's D-037 back tear as measured in play (QA 2026-11-27): the points zigzag 2.7 cm across, 7 mm down each
            var pts = new System.Collections.Generic.List<Vector3>
            {
                new Vector3(-0.0144f, 0.1426f, -0.0804f), new Vector3(0.0126f, 0.1349f, -0.0883f), new Vector3(-0.0144f, 0.1277f, -0.0918f),
                new Vector3(0.0130f, 0.1206f, -0.0966f), new Vector3(-0.0145f, 0.1134f, -0.0987f), new Vector3(0.0129f, 0.1065f, -0.1059f),
            };
            var s = new StitchModel(pts) { Radius = StitchModel.FitRadius(pts) };
            Assert.Less(s.Radius, 0.017f);
            for (int i = 0; i < pts.Count; i++)
            {
                var from = i == 0 ? pts[0] + new Vector3(0f, 0.03f, 0f) : pts[i - 1];
                for (int k = 0; k <= 20; k++) s.Move(Vector3.Lerp(from, pts[i], k / 20f));
                Assert.AreEqual(i + 1, s.Done, "one straight drag to point " + i + " takes that point only");
            }
            Assert.IsTrue(s.Complete);
            Assert.AreEqual(0.022f, StitchModel.FitRadius(new[] { Vector3.zero }), 1e-6f, "a single stitch keeps the full radius");
        }

        [Test]
        public void StitchFastDragSkippingPointsDoesNotAdvance()
        {
            var s = Stitch();
            Assert.AreEqual(StitchModel.Result.WrongPoint, s.Move(new Vector3(0.05f, -0.03f, 0)));
            Assert.AreEqual(0, s.Done);
        }

        // ---------- Cotton ----------
        [Test]
        public void CottonOnlyProgressesInsideRadiusWhileHeld()
        {
            var c = new CottonModel(Vector3.zero) { Radius = 0.05f, HoldSeconds = 2f };
            c.Tick(new Vector3(1, 0, 0), true, 1f); Assert.AreEqual(0f, c.Progress);
            c.Tick(Vector3.zero, false, 1f); Assert.AreEqual(0f, c.Progress);
            c.Tick(Vector3.zero, true, 1f); Assert.AreEqual(0.5f, c.Progress, 1e-4f);
            c.Tick(new Vector3(1, 0, 0), true, 1f); Assert.AreEqual(0.5f, c.Progress, 1e-4f); // never decreases
        }

        [Test]
        public void CottonCompletesOnceAndNeverOverfills()
        {
            var c = new CottonModel(Vector3.zero) { HoldSeconds = 1f }; int fired = 0;
            for (int i = 0; i < 20; i++) if (c.Tick(Vector3.zero, true, 0.2f)) fired++;
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1f, c.Progress);
            Assert.IsFalse(c.Filling);
        }

        // ---------- Battery (D-009) ----------
        static readonly Vector3 Slot = new Vector3(0, 0, 0.2f);
        BatteryModel Battery()
        {
            var b = new BatteryModel(Vector3.zero, Vector3.up, Slot) { SlotRadius = 0.05f, ClickHold = 0.5f };
            b.Pull.GrabRadius = 0.05f; b.Pull.MinHold = 0.1f; b.Pull.RequiredPull = 0.1f; b.Pull.MaxAngleDeg = 50f;
            return b;
        }

        [Test]
        public void BatteryNewCellDoesNothingBeforeTheOldOneIsOut()
        {
            var b = Battery();
            Assert.IsFalse(b.Insert(Slot, true, 5f));
            Assert.AreEqual(0f, b.Progress);
            Assert.IsTrue(b.TryGrab(Vector3.zero));
            b.Drag(new Vector3(0, 0.05f, 0), 0.5f);
            Assert.AreEqual(0.25f, b.Progress, 1e-4f, "pulling fills the first half");
            Assert.IsFalse(b.Removed);
        }

        [Test]
        public void BatteryPullThenClickInCompletesExactlyOnce()
        {
            var b = Battery(); b.TryGrab(Vector3.zero);
            int popped = 0; for (int i = 0; i < 5; i++) if (b.Drag(new Vector3(0, 0.5f, 0), 0.2f)) popped++;
            Assert.AreEqual(1, popped);
            Assert.IsTrue(b.Removed);
            Assert.AreEqual(0.5f, b.Progress, 1e-4f);
            Assert.IsFalse(b.TryGrab(Vector3.zero), "the old cell is gone");
            int clicked = 0; for (int i = 0; i < 10; i++) if (b.Insert(Slot + new Vector3(0.01f, 0, 0), true, 0.1f)) clicked++;
            Assert.AreEqual(1, clicked);
            Assert.IsTrue(b.Complete);
            Assert.AreEqual(1f, b.Progress);
        }

        [Test]
        public void BatteryLeavingTheSlotRestartsTheClickHold()
        {
            var b = Battery(); b.TryGrab(Vector3.zero);
            b.Drag(new Vector3(0, 0.5f, 0), 0.5f);
            b.Insert(Slot, true, 0.3f);
            Assert.AreEqual(0.8f, b.Progress, 1e-4f);
            Assert.IsTrue(b.Inserting);
            b.Insert(Slot + new Vector3(0.2f, 0, 0), true, 0.1f); // slipped off
            Assert.AreEqual(0.5f, b.Progress, 1e-4f);
            b.Insert(Slot, false, 0.3f);                           // let go
            Assert.AreEqual(0.5f, b.Progress, 1e-4f);
            Assert.IsFalse(b.Complete);
        }

        [Test]
        public void BatteryReleaseDuringPullSpringsBack()
        {
            var b = Battery(); b.TryGrab(Vector3.zero);
            b.Drag(new Vector3(0, 0.08f, 0), 0.5f);
            Assert.AreEqual(0.4f, b.Progress, 1e-4f);
            b.Release();
            Assert.AreEqual(0.2f, b.Progress, 1e-4f);
            Assert.IsFalse(b.Removed);
        }
    }
}
