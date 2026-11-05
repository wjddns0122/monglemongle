using System.Collections.Generic;
using Mongle.Data;
using Mongle.Toy;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>The bigger park and its rides (D-006): grid that grows around the old garden, ride footprints, balloon trips, wheel stops.</summary>
    public class RidesTests
    {
        static readonly Vector2Int Bench = new Vector2Int(2, 1), Pot = new Vector2Int(1, 1);
        static readonly List<(Vector2Int, FurniturePlacement)> None = new List<(Vector2Int, FurniturePlacement)>();

        [Test]
        public void ParkGridKeepsOldCellsWhereTheyWere()
        {
            var park = VillageDirector.ParkGrid();
            var old = new VillageGrid(14, 10, 0.4f, new Vector3(-2.8f, 0, -2.0f));
            // a saved placement in the old garden lands on exactly the same spot
            Assert.AreEqual(old.CenterOf(Bench, 2, 6, 1), park.CenterOf(Bench, 2, 6, 1));
            Assert.AreEqual(old.CenterOf(Pot, 13, 9, 0), park.CenterOf(Pot, 13, 9, 0));
            // and the park reaches well beyond it on every side but the north
            Assert.IsTrue(park.IsValid(None, Pot, park.MinX, park.MinY, 0));
            Assert.IsTrue(park.IsValid(None, Pot, park.MaxX - 1, park.MaxY - 1, 0));
            Assert.IsFalse(park.IsValid(None, Pot, park.MinX - 1, 0, 0));
            Assert.IsFalse(park.IsValid(None, Bench, park.MaxX - 1, 0, 0)); // 2 wide sticks out
            Assert.IsFalse(park.IsValid(None, Pot, 0, park.MinY - 1, 0));
            // the grid stays inside the walkable park
            var r = VillageDirector.ParkRect;
            Assert.GreaterOrEqual(park.CellCenter(park.MinX, park.MinY).x - park.Cell * 0.5f, r.x);
            Assert.GreaterOrEqual(park.CellCenter(park.MinX, park.MinY).z - park.Cell * 0.5f, r.y);
            Assert.LessOrEqual(park.CellCenter(park.MaxX - 1, park.MaxY - 1).x + park.Cell * 0.5f, r.z);
            Assert.LessOrEqual(park.CellCenter(park.MaxX - 1, park.MaxY - 1).z + park.Cell * 0.5f, r.w);
        }

        [Test]
        public void SnapClampsInsideTheParkGrid()
        {
            var park = VillageDirector.ParkGrid();
            Assert.AreEqual(new Vector2Int(park.MinX, park.MinY), park.SnapOrigin(Pot, 0, new Vector3(-100, 0, -100)));
            Assert.AreEqual(new Vector2Int(park.MaxX - 2, park.MaxY - 1), park.SnapOrigin(Bench, 0, new Vector3(100, 0, 100)));
        }

        [Test]
        public void BlockBoxCoversOnlyTheTurnedFootprint()
        {
            var g = VillageDirector.ParkGrid();
            var c = new Vector3(5.6f, 0, 1.4f);
            g.BlockBox(c, new Vector2(2.35f, 0.75f), -12f);
            Assert.IsTrue(g.Blocked.Contains(g.CellAt(c)));
            Assert.IsTrue(g.Blocked.Contains(g.CellAt(c + Quaternion.Euler(0, -12f, 0) * new Vector3(1.8f, 0, 0))));   // along the wheel (still inside the grid)
            Assert.IsFalse(g.Blocked.Contains(g.CellAt(c + Quaternion.Euler(0, -12f, 0) * new Vector3(0, 0, 1.6f))));  // in front of it
            Assert.IsFalse(g.IsValid(None, Pot, g.CellAt(c).x, g.CellAt(c).y, 0));
            Assert.IsTrue(g.IsValid(None, Pot, 5, 5, 0), "the old garden is untouched");
        }

        [Test]
        public void BalloonWaitsOnTheGroundUntilItMayLift()
        {
            var b = new BalloonCycle();
            for (int i = 0; i < 100; i++) b.Tick(0.1f, false);
            Assert.AreEqual(BalloonPhase.Grounded, b.Phase);
            Assert.AreEqual(0f, b.Altitude);
            Assert.AreEqual(0, b.Trips);
        }

        [Test]
        public void BalloonTripGoesUpFloatsAndComesBack()
        {
            var b = new BalloonCycle { RiseTime = 2f, FloatTime = 3f, DescendTime = 2f, Height = 4f };
            b.Tick(0.1f, true);
            Assert.AreEqual(BalloonPhase.Rising, b.Phase);
            float last = -1f;
            // one extra tick per phase: 0.1 s steps do not add up to exactly 2.0 s in floats
            for (int i = 0; i < 21; i++) { b.Tick(0.1f, false); if (b.Phase == BalloonPhase.Rising) { Assert.GreaterOrEqual(b.Altitude, last); last = b.Altitude; } }
            Assert.AreEqual(BalloonPhase.Floating, b.Phase);
            Assert.AreEqual(4f, b.Altitude, 0.3f);
            for (int i = 0; i < 31; i++) b.Tick(0.1f, false);
            Assert.AreEqual(BalloonPhase.Descending, b.Phase);
            for (int i = 0; i < 21; i++) b.Tick(0.1f, false);
            Assert.AreEqual(BalloonPhase.Grounded, b.Phase);
            Assert.AreEqual(1, b.Trips, "one finished trip");
            Assert.AreEqual(0f, b.Altitude);
            b.Tick(0.1f, false);
            Assert.AreEqual(BalloonPhase.Grounded, b.Phase, "and it stays down until it may lift again");
        }

        [Test]
        public void GondolaCrossesTheBottomOnlyOnceAndNotAtTheTop()
        {
            Assert.IsTrue(WheelMath.CrossedBottom(-3f, 2f));
            Assert.IsTrue(WheelMath.CrossedBottom(3f, -2f), "either turning direction");
            Assert.IsTrue(WheelMath.CrossedBottom(-1f, 0f));
            Assert.IsFalse(WheelMath.CrossedBottom(0f, 1f), "landing on 0 already counted");
            Assert.IsFalse(WheelMath.CrossedBottom(-10f, -5f));
            Assert.IsFalse(WheelMath.CrossedBottom(179f, -179f), "the wrap at the top is not the bottom");
            Assert.AreEqual(0f, WheelMath.FromBottom(Vector3.down * 2f, Vector3.forward), 0.01f);
            Assert.AreEqual(90f, Mathf.Abs(WheelMath.FromBottom(Vector3.right * 2f, Vector3.forward)), 0.01f);
        }

        [Test]
        public void CarouselBoardingSpotStaysOutsideItsHole()
        {
            // the scene's carousel (4.5 m, yaw 95) and the sizes it had before
            foreach (var (height, yaw) in new[] { (4.5f, 95f), (3f, 95f), (2f, 0f), (4.5f, -30f) })
            {
                float radius = CarouselMath.Footprint * height, stairsYaw = CarouselMath.StairsYaw(yaw);
                var board = Quaternion.Euler(0f, yaw, 0f) * CarouselMath.BoardLocal(height);
                // toys stand on the walkable ground: clear of the hole by more than the agent radius plus the sampling slack
                Assert.IsFalse(CarouselMath.InHole(board, radius, stairsYaw, 0.25f), "boarding spot is walkable at h=" + height + " yaw=" + yaw);
                // ... right in front of the stairs
                var stairs = Quaternion.Euler(0f, yaw, 0f) * CarouselMath.StairsDir;
                Assert.Greater(Vector3.Dot(board.normalized, stairs), 0.999f);
                // while the deck and the stairs (the whole footprint) are cut out of the walkable ground
                for (int i = 0; i < 64; i++)
                {
                    var d = Quaternion.Euler(0f, i * 360f / 64f, 0f) * Vector3.forward;
                    Assert.IsTrue(CarouselMath.InHole(d * radius * 0.98f, radius, stairsYaw), "footprint edge at " + (i * 360f / 64f) + "°");
                }
            }
            // the old axis-aligned square hole had the 4.5 m carousel's boarding spot in its corner
            var old = Quaternion.Euler(0f, 95f, 0f) * (CarouselMath.StairsDir * (CarouselMath.Footprint * 4.5f + 0.35f));
            Assert.IsTrue(Mathf.Abs(old.x) < CarouselMath.Footprint * 4.5f && Mathf.Abs(old.z) < CarouselMath.Footprint * 4.5f);
        }

        [Test]
        public void CarouselHorsesComeWithinReachOfTheStairs()
        {
            // horses ride a ring at ~0.375 of the height; toys wait half a metre past the footprint
            foreach (float h in new[] { 2f, 3f, 4.5f })
            {
                float board = CarouselMath.BoardLocal(h).magnitude, ring = 0.375f * h;
                float reach = CarouselMath.Reach(board, ring, 35f);
                Assert.Greater(reach, board - ring, "the horse right by the stairs can be boarded");
                Assert.LessOrEqual(CarouselMath.Reach(board, ring, 30f), reach);
                Assert.Greater(CarouselMath.Reach(board, ring, 60f), reach, "a horse a sixth of a turn away is not");
            }
            Assert.AreEqual(1f, CarouselMath.Reach(3f, 2f, 0f), 1e-4f);
        }

        [Test]
        public void BoardingSpotCellsCanBeKeptFree()
        {
            var g = VillageDirector.ParkGrid();
            var board = new Vector3(-8f, 0f, 9.5f) + Quaternion.Euler(0f, 95f, 0f) * CarouselMath.BoardLocal(4.5f);
            g.BlockDisc(board, 0.45f);
            Assert.IsTrue(g.Blocked.Contains(g.CellAt(board)));
            Assert.IsFalse(g.IsValid(None, Pot, g.CellAt(board).x, g.CellAt(board).y, 0), "no furniture on the queue spot");
        }

        [Test]
        public void RidesAreLikedButOnlyWhenOneHasRoom()
        {
            var s = new IntentSelector(1) { Personality = Personality.Energetic };
            Assert.AreEqual(0f, s.Weight(Intent.Ride, false, 0f));
            float lively = s.Weight(Intent.Ride, true, 0f);
            var sleepy = new IntentSelector(1) { Personality = Personality.Sleepy };
            Assert.Greater(lively, sleepy.Weight(Intent.Ride, true, 0f));
            s.StartCooldown(Intent.Ride, 0f);
            Assert.Less(s.Weight(Intent.Ride, true, 10f), lively, "a toy that just rode waits a while");
            Assert.AreEqual(lively, s.Weight(Intent.Ride, true, 60f), 1e-4f);
        }
    }
}
