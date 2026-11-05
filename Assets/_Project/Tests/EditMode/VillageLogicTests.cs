using System.Collections.Generic;
using System.Linq;
using Mongle.Toy;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    public class VillageLogicTests
    {
        VillageGrid Grid() => new VillageGrid(14, 10, 0.4f, new Vector3(-2.8f, 0, -2.0f));
        static readonly Vector2Int Bench = new Vector2Int(2, 1), Pot = new Vector2Int(1, 1);

        [Test] public void RotationSwapsFootprint() { Assert.AreEqual(new Vector2Int(1, 2), VillageGrid.Size(Bench, 1)); Assert.AreEqual(Bench, VillageGrid.Size(Bench, 2)); }

        [Test]
        public void BlockedDiscRefusesPlacementOnlyUnderIt()
        {
            var g = Grid(); var none = new List<(Vector2Int, FurniturePlacement)>();
            g.BlockDisc(new Vector3(0.45f, 0, -2.4f), 0.76f);    // the garden carousel (north edge, between the shops)
            Assert.IsTrue(g.Blocked.Contains(new Vector2Int(8, 0)));
            Assert.IsFalse(g.IsValid(none, Pot, 8, 0, 0));
            Assert.IsFalse(g.IsValid(none, Bench, 7, 0, 0));     // any overlapping cell is enough
            Assert.IsTrue(g.IsValid(none, Pot, 8, 2, 0));       // default flower pot spot stays free
            Assert.IsTrue(g.IsValid(none, Pot, 5, 1, 0));       // QA tulip spot stays free
            Assert.IsTrue(g.IsValid(none, Pot, 7, 2, 0));       // QA lamp spot stays free
        }

        [Test]
        public void OutOfBoundsIsInvalid()
        {
            var g = Grid(); var none = new List<(Vector2Int, FurniturePlacement)>();
            Assert.IsTrue(g.IsValid(none, Bench, 0, 0, 0));
            Assert.IsTrue(g.IsValid(none, Bench, 12, 9, 0));
            Assert.IsFalse(g.IsValid(none, Bench, 13, 9, 0)); // width 2 sticks out
            Assert.IsFalse(g.IsValid(none, Bench, -1, 0, 0));
            Assert.IsFalse(g.IsValid(none, Bench, 0, 9, 1));  // rotated: depth 2 sticks out
        }

        [Test]
        public void OverlapIsInvalid()
        {
            var g = Grid();
            var others = new List<(Vector2Int, FurniturePlacement)> { (Bench, new FurniturePlacement { furnitureId = "Fur_Bench", cellX = 5, cellY = 5 }) };
            Assert.IsFalse(g.IsValid(others, Pot, 6, 5, 0));
            Assert.IsTrue(g.IsValid(others, Pot, 7, 5, 0));
            Assert.IsFalse(g.IsValid(others, Bench, 4, 5, 0));
            Assert.IsTrue(g.IsValid(others, Bench, 5, 6, 0));
            Assert.IsFalse(g.IsValid(others, Bench, 5, 4, 1)); // rotated bench covers (5,4),(5,5)
        }

        [Test]
        public void SnapClampsInsideGrid()
        {
            var g = Grid();
            var c = g.SnapOrigin(Bench, 0, new Vector3(100, 0, 100));
            Assert.AreEqual(new Vector2Int(12, 9), c);
            c = g.SnapOrigin(Pot, 0, new Vector3(-100, 0, -100));
            Assert.AreEqual(new Vector2Int(0, 0), c);
        }

        [Test]
        public void CenterAndCellRoundTrip()
        {
            var g = Grid();
            var center = g.CenterOf(Pot, 3, 4, 0);
            Assert.AreEqual(new Vector2Int(3, 4), g.CellAt(center));
            Assert.AreEqual(g.Origin.x + 3.5f * 0.4f, center.x, 1e-4f);
        }

        // ---------------- intent selection ----------------
        [Test]
        public void UnavailableIntentsAreNeverPicked()
        {
            var s = new IntentSelector(1);
            for (int i = 0; i < 500; i++) { var p = s.Pick(false, false, 0f); Assert.IsTrue(p == Intent.Idle || p == Intent.Wander, p.ToString()); }
        }

        [Test]
        public void ShyFavorsBenchAndFlowerOverWander()
        {
            var s = new IntentSelector(42); var count = new Dictionary<Intent, int>();
            for (int i = 0; i < 4000; i++) { var p = s.Pick(true, true, 0f); count[p] = (count.TryGetValue(p, out var c) ? c : 0) + 1; }
            Assert.Greater(count[Intent.Bench], count[Intent.Wander]);
            Assert.Greater(count[Intent.Flower], count[Intent.Wander]);
            Assert.Greater(count[Intent.Bench], 500);
            Assert.Greater(count[Intent.Flower], 500);
            Assert.Greater(count[Intent.Idle], 100); // never degenerates to a single behavior
        }

        [Test]
        public void CooldownLowersRepeatChance()
        {
            var s = new IntentSelector(5);
            float fresh = s.Weight(Intent.Bench, true, 0f);
            s.StartCooldown(Intent.Bench, 0f);
            float cooling = s.Weight(Intent.Bench, true, 1f);
            float after = s.Weight(Intent.Bench, true, 30f);
            Assert.Less(cooling, fresh * 0.5f);
            Assert.AreEqual(fresh, after, 1e-4f);
        }

        [Test]
        public void SeededSelectionIsDeterministic()
        {
            var a = new IntentSelector(9); var b = new IntentSelector(9);
            for (int i = 0; i < 50; i++) Assert.AreEqual(a.Pick(true, true, i), b.Pick(true, true, i));
        }
    }
}
