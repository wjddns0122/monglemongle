using System.Collections.Generic;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    public class WorldStateTests
    {
        long now;
        WorldState W() { var w = new WorldState(); now = 1000; w.Clock = () => now; return w; }

        static FurnitureDefinition Item(string id, int price, bool plant = false)
        {
            var d = ScriptableObject.CreateInstance<FurnitureDefinition>();
            d.id = id; d.price = price; d.category = plant ? ItemCategory.Plant : ItemCategory.Furniture;
            d.stageWaters = new[] { 2, 5 }; d.waterCooldownSeconds = 25f; d.bloomReward = 15;
            return d;
        }

        [Test] public void EarnAndSpend_NeverGoNegative()
        {
            var w = W(); w.Earn(50); Assert.AreEqual(50, w.Coins);
            Assert.IsFalse(w.TrySpend(51)); Assert.AreEqual(50, w.Coins);
            Assert.IsTrue(w.TrySpend(50)); Assert.AreEqual(0, w.Coins);
            w.Earn(-5); w.Earn(0); Assert.AreEqual(0, w.Coins, "non-positive earnings are ignored");
        }

        [Test] public void Buy_ChargesOnceAndFillsInventory()
        {
            var w = W(); var d = Item("Fur_A", 45); w.Earn(100);
            Assert.AreEqual(BuyResult.Ok, w.Buy(d));
            Assert.AreEqual(55, w.Coins); Assert.AreEqual(1, w.Owned("Fur_A"));
            Assert.AreEqual(BuyResult.Ok, w.Buy(d)); Assert.AreEqual(10, w.Coins); Assert.AreEqual(2, w.Owned("Fur_A"));
            Assert.AreEqual(BuyResult.NotEnoughCoins, w.Buy(d)); Assert.AreEqual(10, w.Coins); Assert.AreEqual(2, w.Owned("Fur_A"), "a failed purchase changes nothing");
        }

        [Test] public void Buy_RejectsStarterAndNull()
        {
            var w = W(); w.Earn(999);
            Assert.AreEqual(BuyResult.NotForSale, w.Buy(Item("Fur_Free", 0)));
            Assert.AreEqual(BuyResult.NotForSale, w.Buy(null));
            Assert.AreEqual(999, w.Coins);
        }

        [Test] public void Inventory_TakeAndReturn()
        {
            var w = W(); Assert.IsFalse(w.TakeFromInventory("X"));
            w.AddToInventory("X"); w.AddToInventory("X");
            Assert.IsTrue(w.TakeFromInventory("X")); Assert.AreEqual(1, w.Owned("X"));
            Assert.IsTrue(w.TakeFromInventory("X")); Assert.AreEqual(0, w.Owned("X")); Assert.IsFalse(w.Inventory.ContainsKey("X"));
        }

        [Test] public void InstanceIds_AreUnique()
        {
            var w = W(); var seen = new HashSet<string>();
            for (int i = 0; i < 50; i++) Assert.IsTrue(seen.Add(w.NewInstanceId("Plant_Tulip")));
        }

        [Test] public void Watering_GrowsInStages_AndHonoursCooldown()
        {
            var w = W(); var d = Item("Plant_T", 15, true); var p = w.EnsurePlant("Plant_T#1", d.id);
            Assert.AreEqual(0, w.StageOf(p, d)); Assert.IsTrue(w.NeedsWater(p, d), "a new plant is thirsty");
            Assert.AreEqual(WaterResult.Ok, w.Water(p.instanceId, d, out bool up, out int coins)); Assert.IsFalse(up); Assert.AreEqual(0, coins);
            Assert.AreEqual(WaterResult.TooSoon, w.Water(p.instanceId, d, out up, out coins), "no double watering inside the cooldown");
            Assert.AreEqual(1, p.waters);
            now += 24; Assert.AreEqual(WaterResult.TooSoon, w.Water(p.instanceId, d, out up, out coins));
            now += 2; Assert.AreEqual(WaterResult.Ok, w.Water(p.instanceId, d, out up, out coins)); Assert.IsTrue(up, "2nd water reaches stage 1"); Assert.AreEqual(1, w.StageOf(p, d));
        }

        [Test] public void Bloom_PaysOnceAndStopsAtFullGrowth()
        {
            var w = W(); var d = Item("Plant_T", 15, true); var p = w.EnsurePlant("Plant_T#1", d.id);
            int paid = 0, calls = 0;
            for (int i = 0; i < 12; i++)
            {
                now += 30;
                var r = w.Water(p.instanceId, d, out bool up, out int coins); paid += coins;
                if (r == WaterResult.Ok) calls++;
            }
            Assert.AreEqual(5, calls, "5 waterings reach full growth");
            Assert.AreEqual(2, w.StageOf(p, d)); Assert.IsTrue(w.IsFullyGrown(p, d));
            Assert.AreEqual(15, paid); Assert.AreEqual(15, w.Coins, "the bloom bonus is paid exactly once");
            Assert.AreEqual(WaterResult.FullyGrown, w.Water(p.instanceId, d, out _, out _));
            Assert.IsFalse(w.NeedsWater(p, d));
        }

        [Test] public void Cooldown_IsExactAtRealUnixTimes()
        {
            // regression: float math lost whole seconds at ~1.7e9 and made the cooldown wrong in the real game
            var w = new WorldState(); long t = 1_791_000_000; w.Clock = () => t;
            var d = Item("Plant_T", 15, true); var p = w.EnsurePlant("P#1", d.id);
            Assert.AreEqual(WaterResult.Ok, w.Water(p.instanceId, d, out _, out _));
            Assert.AreEqual(25f, w.SecondsUntilThirsty(p, d), 0.001f);
            t += 1; Assert.AreEqual(24f, w.SecondsUntilThirsty(p, d), 0.001f);
            t += 23; Assert.AreEqual(WaterResult.TooSoon, w.Water(p.instanceId, d, out _, out _)); Assert.AreEqual(1f, w.SecondsUntilThirsty(p, d), 0.001f);
            t += 1; Assert.AreEqual(WaterResult.Ok, w.Water(p.instanceId, d, out _, out _)); Assert.AreEqual(2, p.waters);
        }

        [Test] public void Water_UnknownPlant() => Assert.AreEqual(WaterResult.Unknown, W().Water("nope", Item("P", 1, true), out _, out _));

        [Test] public void Seen_IsRememberedOnce()
        {
            var w = W(); Assert.IsFalse(w.HasSeen("a")); w.MarkSeen("a"); w.MarkSeen("a"); Assert.IsTrue(w.HasSeen("a")); Assert.AreEqual(1, w.Seen.Count);
        }

        [Test] public void ChangedEvent_FiresOnMutation()
        {
            var w = W(); int n = 0; w.Changed += () => n++;
            w.Earn(10); w.AddToInventory("a"); w.TrySpend(5);
            Assert.GreaterOrEqual(n, 3);
        }

        // ---------------- save round trip ----------------
        [Test] public void SaveRoundTrip_KeepsWorldAndInstanceIds()
        {
            var toy = new ToyRuntime("TeddyBear_001", new[] { "Thorn" });
            toy.EnsureResidentId("r1");
            toy.Furniture.Add(new FurniturePlacement { furnitureId = "Fur_Bench", instanceId = "Fur_Bench", cellX = 8, cellY = 6, rotation = 2 });
            toy.Furniture.Add(new FurniturePlacement { furnitureId = "Plant_Tulip", instanceId = "Plant_Tulip#3", cellX = 2, cellY = 2 });
            var w = W(); w.Earn(77); w.AddToInventory("Fur_Lamp"); w.InstanceSeq = 3; w.MarkSeen("Plant_Tulip#3");
            var p = w.EnsurePlant("Plant_Tulip#3", "Plant_Tulip"); p.waters = 3; p.lastWateredUnix = 990; p.bloomRewarded = false;

            var save = SaveMapper.ToSave(toy, null, w);
            var json = JsonUtility.ToJson(save); var back = JsonUtility.FromJson<SaveData>(json);
            var w2 = W(); SaveMapper.ApplyWorld(back, w2);
            var toy2 = SaveMapper.FromSave(back, "TeddyBear_001", new[] { "Thorn" });

            Assert.AreEqual(77, w2.Coins); Assert.AreEqual(1, w2.Owned("Fur_Lamp")); Assert.AreEqual(3, w2.InstanceSeq);
            Assert.IsTrue(w2.HasSeen("Plant_Tulip#3"));
            var p2 = w2.Plant("Plant_Tulip#3"); Assert.NotNull(p2); Assert.AreEqual(3, p2.waters); Assert.AreEqual(990, p2.lastWateredUnix);
            Assert.AreEqual(2, toy2.Furniture.Count); Assert.AreEqual("Plant_Tulip#3", toy2.Furniture[1].Id);
            Assert.AreEqual(SaveData.CurrentVersion, back.saveVersion);
        }

        [Test] public void LegacyV1Save_LoadsWithDefaults()
        {
            // a v1 file has no inventory/plants/seen/instanceSeq and furniture without instanceId
            var json = "{\"saveVersion\":1,\"toyKindId\":\"TeddyBear_001\",\"coins\":0,\"furniture\":[{\"furnitureId\":\"Fur_Bench\",\"cellX\":8,\"cellY\":6,\"rotation\":2}]}";
            var data = JsonUtility.FromJson<SaveData>(json);
            var w = W(); SaveMapper.ApplyWorld(data, w);
            Assert.AreEqual(0, w.Coins); Assert.AreEqual(0, w.Inventory.Count); Assert.AreEqual(0, w.Plants.Count);
            var toy = SaveMapper.FromSave(data, "TeddyBear_001", new[] { "Thorn" });
            Assert.AreEqual("Fur_Bench", toy.Furniture[0].Id, "legacy pieces use furnitureId as their instance id");
        }

        [Test] public void Restore_DropsBrokenEntries()
        {
            var w = W();
            w.Restore(-5, new[] { new ItemSave { itemId = "", count = 3 }, new ItemSave { itemId = "ok", count = 0 }, new ItemSave { itemId = "good", count = 2 } },
                new[] { new PlantSave { instanceId = "" }, new PlantSave { instanceId = "p1", waters = -4 }, new PlantSave { instanceId = "p1", waters = 9 } }, new[] { "", "s" }, -1);
            Assert.AreEqual(0, w.Coins); Assert.AreEqual(1, w.Inventory.Count); Assert.AreEqual(2, w.Owned("good"));
            Assert.AreEqual(1, w.Plants.Count, "empty ids and duplicates are ignored"); Assert.AreEqual(0, w.Plants[0].waters);
            Assert.IsTrue(w.HasSeen("s")); Assert.AreEqual(0, w.InstanceSeq);
        }


        // ---------------- quests (D-003) ----------------
        static QuestDefinition Quest(string id, string evt, string param, int target, int reward, string requires = "")
        {
            var q = ScriptableObject.CreateInstance<QuestDefinition>();
            q.id = id; q.giverShopId = "S"; q.eventId = evt; q.eventParam = param; q.targetCount = target; q.rewardCoins = reward; q.requires = requires; return q;
        }

        WorldState WQ(params QuestDefinition[] qs) { var w = W(); w.SetQuestDefinitions(qs); return w; }

        [Test] public void Quest_Chain_UnlocksInOrder()
        {
            var a = Quest("A", "ItemPlaced", "X", 1, 10); var b = Quest("B", "ItemPlaced", "X", 1, 20, "A"); var w = WQ(a, b);
            Assert.AreEqual(QuestStatus.Available, w.StatusOf(a)); Assert.AreEqual(QuestStatus.Locked, w.StatusOf(b));
            Assert.IsFalse(w.AcceptQuest(b), "a locked quest cannot be accepted");
            Assert.IsTrue(w.AcceptQuest(a)); Assert.IsFalse(w.AcceptQuest(a), "accepting twice is refused");
            w.Report("ItemPlaced", "X"); Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(a)); Assert.AreEqual(QuestStatus.Locked, w.StatusOf(b), "b waits until a is paid out");
            Assert.IsTrue(w.ClaimQuest(a)); Assert.AreEqual(QuestStatus.Done, w.StatusOf(a)); Assert.AreEqual(QuestStatus.Available, w.StatusOf(b));
        }

        [Test] public void Quest_OnlyCountsAfterAccept_AndMatchingEvents()
        {
            var q = Quest("Q", "MongiSat", "Fur_Armchair", 2, 30); var w = WQ(q);
            w.Report("MongiSat", "Fur_Armchair"); Assert.AreEqual(0, w.ProgressOf(q), "events before accepting are ignored");
            w.AcceptQuest(q);
            w.Report("MongiSat", "Fur_Bench"); w.Report("MongiPlayed", "Fur_Armchair"); Assert.AreEqual(0, w.ProgressOf(q), "wrong parameter / wrong event do not count");
            w.Report("MongiSat", "Fur_Armchair"); Assert.AreEqual(1, w.ProgressOf(q)); Assert.AreEqual(QuestStatus.Active, w.StatusOf(q));
            w.Report("MongiSat", "Fur_Armchair"); Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(q));
            w.Report("MongiSat", "Fur_Armchair"); Assert.AreEqual(2, w.ProgressOf(q), "progress never exceeds the target");
        }

        [Test] public void Quest_EmptyParameter_MatchesAnything()
        {
            var q = Quest("Q", "PlantWatered", "", 2, 5); var w = WQ(q); w.AcceptQuest(q);
            w.Report("PlantWatered", "Plant_Tulip"); w.Report("PlantWatered", "Plant_Cactus");
            Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(q));
        }

        [Test] public void Quest_RewardIsPaidExactlyOnce()
        {
            var q = Quest("Q", "ItemPlaced", "", 1, 25); var w = WQ(q); w.AcceptQuest(q);
            Assert.IsFalse(w.ClaimQuest(q), "cannot claim before it is finished"); Assert.AreEqual(0, w.Coins);
            w.Report("ItemPlaced", "a"); Assert.AreEqual(0, w.Coins, "finishing does not pay by itself");
            Assert.IsTrue(w.ClaimQuest(q)); Assert.AreEqual(25, w.Coins);
            Assert.IsFalse(w.ClaimQuest(q)); Assert.AreEqual(25, w.Coins, "second claim pays nothing");
            w.Report("ItemPlaced", "b"); Assert.AreEqual(QuestStatus.Done, w.StatusOf(q)); Assert.AreEqual(25, w.Coins);
        }

        [Test] public void Quest_FiresForGameEvents_BuyWaterBloom()
        {
            var buy = Quest("B", "ItemBought", "Plant_T", 1, 1); var water = Quest("W", "PlantWatered", "", 1, 1); var bloom = Quest("G", "PlantBloomed", "Plant_T", 1, 1);
            var w = WQ(buy, water, bloom); foreach (var q in new[] { buy, water, bloom }) w.AcceptQuest(q);
            var d = Item("Plant_T", 15, true); d.bloomReward = 5; w.Earn(15);
            w.Buy(d); Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(buy));
            w.EnsurePlant("Plant_T#1", d.id);
            for (int i = 0; i < 5; i++) { now += 30; w.Water("Plant_T#1", d, out _, out _); }
            Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(water)); Assert.AreEqual(QuestStatus.ReadyToClaim, w.StatusOf(bloom), "full growth reports PlantBloomed once");
        }

        [Test] public void Quest_SaveRoundTrip_KeepsStatesAndProgress()
        {
            var a = Quest("A", "E", "", 3, 10); var b = Quest("B", "E", "", 1, 10, "A"); var c = Quest("C", "E", "", 1, 10);
            var w = WQ(a, b, c); w.AcceptQuest(a); w.Report("E"); w.AcceptQuest(c); w.Report("E");
            // a: 2/3 active, c finished
            var toy = new ToyRuntime("TeddyBear_001", new[] { "Thorn" });
            var json = JsonUtility.ToJson(SaveMapper.ToSave(toy, null, w)); var back = JsonUtility.FromJson<SaveData>(json);
            var w2 = WQ(a, b, c); SaveMapper.ApplyWorld(back, w2);
            Assert.AreEqual(QuestStatus.Active, w2.StatusOf(a)); Assert.AreEqual(2, w2.ProgressOf(a));
            Assert.AreEqual(QuestStatus.ReadyToClaim, w2.StatusOf(c)); Assert.AreEqual(QuestStatus.Locked, w2.StatusOf(b));
            Assert.IsTrue(w2.ClaimQuest(c) && !w2.ClaimQuest(c));
        }

        [Test] public void Quest_OldSavesWithoutQuests_Load()
        {
            var json = "{\"saveVersion\":2,\"toyKindId\":\"TeddyBear_001\",\"coins\":40}";
            var data = JsonUtility.FromJson<SaveData>(json); var q = Quest("A", "E", "", 1, 1); var w = WQ(q); SaveMapper.ApplyWorld(data, w);
            Assert.AreEqual(40, w.Coins); Assert.AreEqual(QuestStatus.Available, w.StatusOf(q)); Assert.AreEqual(0, w.QuestStates.Count);
        }

        [Test] public void Quest_BrokenSavedEntries_AreSanitized()
        {
            var w = WQ(Quest("A", "E", "", 1, 1));
            w.Restore(0, null, null, null, 0, new[] { new QuestSave { id = "", status = 3 }, new QuestSave { id = "A", status = 99, progress = -4 }, new QuestSave { id = "A", status = 2, progress = 7 }, new QuestSave { id = "Z", status = 0, progress = 1 } });
            Assert.AreEqual(2, w.QuestStates.Count); Assert.AreEqual(QuestStatus.Done, w.QuestStates["A"].status); Assert.AreEqual(0, w.QuestStates["A"].progress);
            Assert.AreEqual(QuestStatus.Active, w.QuestStates["Z"].status, "a saved state below Active is raised to Active (only Active..Done are stored)");
        }

        [Test] public void Intent_PicksNewTagsOnlyWhenAvailable()
        {
            var s = new IntentSelector(3) { Favorites = InteractionTag.Play };
            for (int i = 0; i < 300; i++) Assert.AreNotEqual(Intent.Play, s.Pick(x => false, 0f));
            int plays = 0; for (int i = 0; i < 2000; i++) if (s.Pick(x => x == Intent.Play, 0f) == Intent.Play) plays++;
            Assert.Greater(plays, 400, "a favored, available intent is chosen often");
            for (int i = 0; i < 300; i++) Assert.AreNotEqual(Intent.Inspect, s.Pick(x => true, 0f), "Inspect is queued by the agent, never weight-picked");
        }
    }
}
