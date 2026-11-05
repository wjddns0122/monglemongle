using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Friends' houses (D-012): one lot each in moving-in order, a random but stable look, kept in the save.</summary>
    public class HouseLotsTests
    {
        static ResidentSave R(string kind) => new ResidentSave { kindId = kind, residentId = kind + "-id" };

        [Test]
        public void EveryNewFriendGetsTheNextFreeLot()
        {
            var list = new List<ResidentSave> { R("Bunny_001"), R("Cat_001") };
            Assert.IsTrue(HouseLots.Assign(list));
            Assert.AreEqual(0, list[0].house); Assert.AreEqual(1, list[1].house);
            Assert.AreNotEqual(list[0].houseVariant, list[1].houseVariant, "a fresh look while unused ones are left");
            foreach (var r in list) { Assert.IsTrue(HouseLots.HasHouse(r)); Assert.That(r.houseTint, Is.InRange(0, HouseLots.Tints - 1)); }
            var before = list.Select(r => (r.house, r.houseVariant, r.houseTint)).ToList();
            list.Add(R("Frog_001"));
            Assert.IsTrue(HouseLots.Assign(list));
            Assert.AreEqual(2, list[2].house);
            Assert.AreEqual(before, list.Take(2).Select(r => (r.house, r.houseVariant, r.houseTint)).ToList(), "old houses never change");
            Assert.IsFalse(HouseLots.Assign(list), "nothing left to do");
        }

        [Test]
        public void TheLookIsSeededByTheResident()
        {
            var a = new List<ResidentSave> { R("Dino_001") }; var b = new List<ResidentSave> { R("Dino_001") };
            HouseLots.Assign(a); HouseLots.Assign(b);
            Assert.AreEqual(a[0].houseVariant, b[0].houseVariant); Assert.AreEqual(a[0].houseTint, b[0].houseTint);
            Assert.AreEqual(HouseLots.Turn(a[0]), HouseLots.Turn(b[0]));
            Assert.That(HouseLots.Turn(a[0]), Is.InRange(-12f, 12f));
        }

        [Test]
        public void AClashingOrBrokenSaveIsRepaired()
        {
            var x = R("Bee_001"); x.house = 3; x.houseVariant = 2; x.houseTint = 1;
            var y = R("Car_001"); y.house = 3; y.houseVariant = 5; y.houseTint = 0;    // same lot (hand-edited save)
            var z = R("Pawn_001"); z.house = 99; z.houseVariant = 1;                    // a lot that does not exist
            var list = new List<ResidentSave> { x, y, z };
            HouseLots.Assign(list);
            Assert.AreEqual(3, x.house); Assert.AreEqual(2, x.houseVariant, "the first keeps its house");
            Assert.AreEqual(3, list.Select(r => r.house).Distinct().Count(), "no two friends share a lot");
            Assert.IsTrue(list.All(HouseLots.HasHouse));
        }

        [Test]
        public void LotsDoNotOverlapAndStayInThePark()
        {
            var lots = HouseLots.Lots;
            Assert.GreaterOrEqual(lots.Length, 10, "room for every friend");
            for (int i = 0; i < lots.Length; i++)
            {
                Assert.That(Mathf.Abs(lots[i].pos.x), Is.LessThan(17.9f - HouseLots.LotHalf.x));
                for (int j = i + 1; j < lots.Length; j++)
                {
                    var d = lots[i].pos - lots[j].pos;
                    Assert.IsTrue(Mathf.Abs(d.x) >= HouseLots.LotHalf.x * 2f || Mathf.Abs(d.z) >= HouseLots.LotHalf.y * 2f, "lots " + i + " and " + j + " overlap");
                }
            }
        }

        [Test]
        public void HousesSurviveTheSave()
        {
            var r = R("Bunny_001"); HouseLots.Assign(new List<ResidentSave> { r });
            var s = new SaveData();
            SaveMapper.WriteFriends(s, null, new[] { r });
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s)).residents[0];
            Assert.AreEqual((r.house, r.houseVariant, r.houseTint), (back.house, back.houseVariant, back.houseTint));
            var old = JsonUtility.FromJson<ResidentSave>("{\"kindId\":\"Cat_001\",\"residentId\":\"x\"}");
            Assert.IsFalse(HouseLots.HasHouse(old), "a v7 resident has no house until it is assigned");
        }

        [Test]
        public void LooksAreGivenWithoutAPlaceAndThePlayersChoiceIsSaved()
        {
            var list = new List<ResidentSave> { R("Bunny_001"), R("Cat_001"), R("Frog_001") };
            Assert.IsTrue(HouseLots.AssignLooks(list));
            foreach (var r in list) { Assert.IsTrue(HouseLots.HasLook(r)); Assert.IsFalse(HouseLots.HasSpot(r), "no place until the player chooses"); Assert.AreEqual(-1, r.house); }
            Assert.AreEqual(3, list.Select(r => r.houseVariant).Distinct().Count());
            var looks = list.Select(r => (r.houseVariant, r.houseTint)).ToList();
            Assert.IsFalse(HouseLots.AssignLooks(list), "nothing more to do");
            Assert.AreEqual(looks, list.Select(r => (r.houseVariant, r.houseTint)).ToList());
            var f = list[2]; f.housePlaced = true; f.houseX = 6.4f; f.houseZ = 3.2f; f.houseYaw = 135f;
            Assert.IsTrue(HouseLots.HasSpot(f));
            var s = new SaveData(); SaveMapper.WriteFriends(s, null, list);
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(s)).residents;
            Assert.IsTrue(back[2].housePlaced); Assert.AreEqual(6.4f, back[2].houseX, 1e-4f); Assert.AreEqual(3.2f, back[2].houseZ, 1e-4f); Assert.AreEqual(135f, back[2].houseYaw, 1e-4f);
            Assert.IsFalse(back[0].housePlaced);
            var old = R("Dino_001"); old.house = 4; old.houseVariant = 2; old.houseTint = 1;   // a v8 save: a lot, no choice
            Assert.IsTrue(HouseLots.HasSpot(old));
        }
    }
}
