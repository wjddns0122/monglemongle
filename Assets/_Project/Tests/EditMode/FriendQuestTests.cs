using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using Mongle.Village;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Friends' requests and questions (D-011): bubble scheduling, progress and rewards, answers, birthday, save v7.</summary>
    public class FriendQuestTests
    {
        long now;
        WorldState W() { var w = new WorldState(); now = 10_000; w.Clock = () => now; w.FriendQuests.Rng = new System.Random(7); return w; }
        static readonly OfferCandidate Bori = new OfferCandidate("Bunny_001", Personality.Energetic), Cheese = new OfferCandidate("Cat_001", Personality.Sleepy),
            Pip = new OfferCandidate("Robot_001", Personality.Curious);
        static OfferContext Park(params string[] questions) => new OfferContext { trees = true, rides = new[] { "Ride_Carousel", "Ride_Wheel" }, waterLeft = 5, hasAccessories = true, questions = questions };
        static OfferContext Bare() => new OfferContext { trees = false, rides = new string[0], waterLeft = 0, hasAccessories = false };

        FriendQuest Give(WorldState w, string giver, FriendQuestKind kind, int target = 1, int reward = 20, string param = null)
        {
            var o = new FriendOffer { giver = giver, type = OfferType.Quest, kind = kind, target = target, reward = reward, param = param, expiresUnix = now + 999 };
            w.FriendQuests.Offers.Add(o);
            return w.FriendQuests.Accept(o);
        }

        // ---------------- scheduling ----------------
        [Test] public void Offers_WaitForTheFirstDelay_KeepAGap_AndNeverMoreThanTwo()
        {
            var w = W(); var b = w.FriendQuests; b.QuestionChance = 0;
            var all = new[] { Bori, Cheese, Pip };
            b.HoldOffers(b.FirstDelay);
            Assert.IsNull(b.Tick(all, Park()), "nothing right after entering the village");
            now += b.FirstDelay;
            var first = b.Tick(all, Park()); Assert.IsNotNull(first);
            Assert.IsNull(b.Tick(all, Park()), "a gap between two bubbles");
            now += b.OfferGap; var second = b.Tick(all, Park()); Assert.IsNotNull(second);
            Assert.AreNotEqual(first.giver, second.giver, "one bubble per friend");
            now += b.OfferGap; Assert.IsNull(b.Tick(all, Park()), "at most two bubbles at a time");
            Assert.AreEqual(b.MaxOpenOffers, b.Offers.Count);
            Assert.AreNotEqual(first.kind, second.kind, "never two of the same request at once");
        }

        [Test] public void Offers_FadeWhenNobodyClicks_AndThatFriendRests()
        {
            var w = W(); var b = w.FriendQuests; b.QuestionChance = 0;
            var o = b.Tick(new[] { Bori }, Park()); Assert.IsNotNull(o);
            now += b.OfferLife - 1; b.Tick(new[] { Bori }, Park()); Assert.AreEqual(1, b.Offers.Count, "still up just before it fades");
            now += 1; Assert.IsNull(b.Tick(new[] { Bori }, Park())); Assert.AreEqual(0, b.Offers.Count, "faded");
            Assert.AreEqual(now + b.ExpiredCooldown, b.NextOfferFrom(Bori.giver));
            now += b.ExpiredCooldown; Assert.IsNotNull(b.Tick(new[] { Bori }, Park()), "asks again after resting");
        }

        [Test] public void Offers_NoneWhileTheFriendHasARequest_ThenACooldownOfMinutes()
        {
            var w = W(); var b = w.FriendQuests; b.QuestionChance = 0;
            var o = b.Tick(new[] { Bori }, Park()); var q = b.Accept(o); Assert.IsNotNull(q);
            Assert.IsNull(b.OfferOf(Bori.giver)); Assert.AreSame(q, b.ActiveOf(Bori.giver));
            for (int i = 0; i < 5; i++) { now += b.OfferGap; Assert.IsNull(b.Tick(new[] { Bori }, Park()), "busy with its own request"); }
            while (!q.Done) Assert.IsTrue(b.Report(Event(q), Param(q)));
            // done but not told yet (user 2026-11-27): no new request from that friend, however long it waits
            Assert.IsTrue(q.waiting);
            now += b.PerToyCooldown + 600; Assert.IsNull(b.Tick(new[] { Bori }, Park()), "it waits to be told first");
            now -= b.PerToyCooldown + 600;
            Assert.IsNotNull(b.Claim(Bori.giver));
            Assert.GreaterOrEqual(b.PerToyCooldown, 120, "a few minutes between requests");
            now += b.PerToyCooldown - 1; Assert.IsNull(b.Tick(new[] { Bori }, Park()), "resting after a finished request");
            now += 1; Assert.IsNotNull(b.Tick(new[] { Bori }, Park()));
        }

        static string Event(FriendQuest q) => q.kind == FriendQuestKind.Fruit ? FriendQuestBook.FruitPicked : q.kind == FriendQuestKind.Water ? "PlantWatered"
            : q.kind == FriendQuestKind.Stars ? FriendQuestBook.StarsFound : q.kind == FriendQuestKind.Dress ? "AccessoryWorn" : FriendQuestBook.FriendRode;
        static string Param(FriendQuest q) => q.kind == FriendQuestKind.Ride ? q.giver + "@" + q.param : q.kind == FriendQuestKind.Fruit || q.kind == FriendQuestKind.Water ? "" : q.giver;

        [Test] public void Offers_OnlyAskForWhatTheParkAllows()
        {
            var b = W().FriendQuests;
            CollectionAssert.AreEquivalent(new[] { FriendQuestKind.Stars }, b.KindsFor("x", Bare()), "no trees, rides, plants or accessories: only the star hunt");
            var k = b.KindsFor("x", Park()); Assert.AreEqual(5, k.Count);
            var o = b.Tick(new[] { Bori }, Bare()); Assert.AreEqual(FriendQuestKind.Stars, o.kind);
            Assert.AreEqual(1, o.target); Assert.Greater(o.reward, 0);
            var water = new OfferContext { trees = false, waterLeft = 2, hasAccessories = false };
            now += b.OfferGap; b.Accept(o);
            var w2 = b.Tick(new[] { Cheese }, water); Assert.AreEqual(FriendQuestKind.Water, w2.kind, "stars are taken: only the plants are left");
            Assert.AreEqual(2, w2.target, "never more waterings than the plants can take");
        }

        [Test] public void Questions_AreAskedOnce_AndNeverTwiceAtTheSameTime()
        {
            var w = W(); var b = w.FriendQuests; b.QuestionChance = 1;
            var q1 = b.Tick(new[] { Bori, Cheese }, Park("Food"));
            Assert.AreEqual(OfferType.Question, q1.type); Assert.AreEqual("Food", q1.question);
            now += b.OfferGap;
            var q2 = b.Tick(new[] { Bori, Cheese }, Park("Food"));
            Assert.AreEqual(OfferType.Quest, q2.type, "the same question is not up twice: the other friend asks for something instead");
            Assert.IsTrue(w.Profile.Answer("Food", "딸기 케이크", q1.giver)); Assert.IsTrue(b.Answered(q1));
            Assert.IsFalse(w.Profile.Answer("Food", "쿠키", "Cat_001"), "an answer is kept, not overwritten");
            CollectionAssert.DoesNotContain(w.Profile.Unasked(FriendQuestions.Keys).ToList(), "Food");
            Assert.AreEqual(now + b.PerToyCooldown, b.NextOfferFrom(q1.giver));
            Assert.AreEqual(FriendQuestions.All.Length - 1, w.Profile.Unasked(FriendQuestions.Keys).Count());
        }

        // ---------------- progress and rewards ----------------
        [Test] public void Fruit_CountsForTheOldestRequest_AndPaysOnce()
        {
            var w = W(); int done = 0; w.FriendQuests.Completed += _ => done++;
            var q = Give(w, "Bunny_001", FriendQuestKind.Fruit, 3, 22);
            w.Report(FriendQuestBook.FruitPicked); w.Report(FriendQuestBook.FruitPicked);
            Assert.AreEqual(2, q.progress); Assert.IsFalse(q.Done); Assert.AreEqual(0, w.Coins);
            w.Report(FriendQuestBook.FruitPicked);
            Assert.IsTrue(q.Done); Assert.AreEqual(1, done);
            // done, but the friend has to be told first (user 2026-11-27): the reward waits for that
            Assert.IsTrue(q.waiting); Assert.AreEqual(0, w.Coins); Assert.AreEqual(q, w.FriendQuests.WaitingOf("Bunny_001"));
            Assert.IsNull(w.FriendQuests.Claim("Cat_001"), "another friend has nothing to be told");
            Assert.AreEqual(q, w.FriendQuests.Claim("Bunny_001")); Assert.AreEqual(22, w.Coins); Assert.IsFalse(q.waiting);
            Assert.IsNull(w.FriendQuests.Claim("Bunny_001"), "told once, paid once"); Assert.AreEqual(22, w.Coins);
            Assert.IsFalse(w.FriendQuests.Report(FriendQuestBook.FruitPicked, ""), "nothing left to count");
            Assert.AreEqual(22, w.Coins); Assert.AreEqual(3, q.progress);
            CollectionAssert.Contains(w.FriendQuests.Finished.ToList(), q); Assert.AreEqual(0, w.FriendQuests.Active.Count());
        }

        [Test] public void RideDressAndStars_CountOnlyForTheirFriend()
        {
            var w = W();
            var ride = Give(w, "Bunny_001", FriendQuestKind.Ride, param: "Ride_Wheel");
            var dress = Give(w, "Cat_001", FriendQuestKind.Dress);
            var stars = Give(w, "TeddyBear_001", FriendQuestKind.Stars, reward: 30);
            w.Report(FriendQuestBook.FriendRode, "Cat_001@Ride_Wheel"); w.Report(FriendQuestBook.FriendRode, "Bunny_001@Ride_Carousel");
            Assert.IsFalse(ride.Done, "another friend or another ride does not count");
            w.Report(FriendQuestBook.FriendRode, "Bunny_001@Ride_Wheel"); Assert.IsTrue(ride.Done);
            w.Report("AccessoryWorn", "Bunny_001"); Assert.IsFalse(dress.Done);
            w.Report("AccessoryWorn", "Cat_001"); Assert.IsTrue(dress.Done);
            w.Report(FriendQuestBook.StarsFound, "Cat_001"); Assert.IsFalse(stars.Done);
            w.Report(FriendQuestBook.StarsFound, "TeddyBear_001"); Assert.IsTrue(stars.Done);
            Assert.AreEqual(0, w.Coins, "nothing until the friends are told");
            foreach (var g in new[] { "Bunny_001", "Cat_001", "TeddyBear_001" }) Assert.IsNotNull(w.FriendQuests.Claim(g), g);
            Assert.AreEqual(20 + 20 + 30, w.Coins);
        }

        [Test] public void Watering_CountsTowardsTheRequest_ThroughTheVillageEvent()
        {
            var w = W(); var q = Give(w, "Pawn_001", FriendQuestKind.Water, 2, 16);
            var plant = ScriptableObject.CreateInstance<FurnitureDefinition>();
            plant.id = "Plant_T"; plant.category = ItemCategory.Plant; plant.stageWaters = new[] { 2, 5 }; plant.waterCooldownSeconds = 25f;
            w.EnsurePlant("Plant_T#1", plant.id);
            Assert.AreEqual(WaterResult.Ok, w.Water("Plant_T#1", plant, out _, out _)); Assert.AreEqual(1, q.progress);
            now += 30; Assert.AreEqual(WaterResult.Ok, w.Water("Plant_T#1", plant, out _, out _));
            Assert.IsTrue(q.Done); Assert.AreEqual(0, w.Coins);
            w.FriendQuests.Claim("Pawn_001"); Assert.AreEqual(16, w.Coins);
            UnityEngine.Object.DestroyImmediate(plant);
        }

        [Test] public void Accept_OnlyAnOpenRequest()
        {
            var w = W(); var b = w.FriendQuests;
            Assert.IsNull(b.Accept(null));
            Assert.IsNull(b.Accept(new FriendOffer { giver = "Bunny_001", type = OfferType.Quest }), "not a bubble that is up");
            var o = new FriendOffer { giver = "Bunny_001", type = OfferType.Question, question = "Food" }; b.Offers.Add(o);
            Assert.IsNull(b.Accept(o), "a question is answered, not accepted");
            var q1 = Give(w, "Bunny_001", FriendQuestKind.Stars); var q2 = Give(w, "Cat_001", FriendQuestKind.Dress);
            Assert.AreNotEqual(q1.id, q2.id);
        }

        // ---------------- answers and the birthday ----------------
        [Test] public void Birthday_OnTheDay_LeapDay_AndOneGiftAYear()
        {
            Assert.IsTrue(PlayerProfile.IsBirthday(3, 15, new DateTime(2026, 3, 15)));
            Assert.IsFalse(PlayerProfile.IsBirthday(3, 15, new DateTime(2026, 3, 16)));
            Assert.IsTrue(PlayerProfile.IsBirthday(2, 29, new DateTime(2027, 2, 28)), "a leap-day birthday is celebrated on the 28th in common years");
            Assert.IsFalse(PlayerProfile.IsBirthday(2, 29, new DateTime(2028, 2, 28)));
            Assert.IsTrue(PlayerProfile.IsBirthday(2, 29, new DateTime(2028, 2, 29)));
            var p = new PlayerProfile();
            Assert.IsFalse(p.Answer(PlayerProfile.Birthday, "13-1", "x")); Assert.IsFalse(p.Answer(PlayerProfile.Birthday, "4-31", "x")); Assert.IsFalse(p.Answer(PlayerProfile.Birthday, "봄", "x"));
            Assert.IsTrue(p.Answer(PlayerProfile.Birthday, PlayerProfile.BirthdayValue(10, 11), "Bunny_001"));
            Assert.IsTrue(p.TryBirthday(out int m, out int d)); Assert.AreEqual(10, m); Assert.AreEqual(11, d);
            Assert.IsFalse(p.TryClaimBirthdayGift(new DateTime(2026, 10, 10)), "not the day");
            Assert.IsTrue(p.TryClaimBirthdayGift(new DateTime(2026, 10, 11)));
            Assert.IsFalse(p.TryClaimBirthdayGift(new DateTime(2026, 10, 11)), "once a year");
            Assert.IsTrue(p.TryClaimBirthdayGift(new DateTime(2027, 10, 11)));
        }

        [Test] public void Lines_UseTheRightParticles()
        {
            var food = FriendQuestions.Get("Food"); var season = FriendQuestions.Get("Season"); var toy = FriendQuestions.Get("Toy");
            StringAssert.StartsWith("딸기 케이크를 좋아하는구나!", FriendQuestText.Reaction(food, "딸기 케이크", "Bunny_001", new DateTime(2026, 1, 1)));
            StringAssert.StartsWith("봄을 좋아하는구나!", FriendQuestText.Reaction(season, "봄", "Bunny_001", new DateTime(2026, 1, 1)));
            StringAssert.StartsWith("나 같은 로봇?!", FriendQuestText.Reaction(toy, "로봇", "Robot_001", new DateTime(2026, 1, 1)));
            StringAssert.StartsWith("나 같은 개구리?!", FriendQuestText.Reaction(FriendQuestions.Get("Animal"), "개구리", "Frog_001", new DateTime(2026, 1, 1)), "개굴이 (D-011 follow-up)");
            Assert.AreEqual("인형", FriendQuestions.ToyWordOf("Frog_001"));
            StringAssert.Contains("개굴", FriendQuestText.Request(FriendQuestKind.Water, Personality.Gentle, 3, "", "Frog_001"));
            StringAssert.Contains("개굴", FriendQuestText.KindLine("Frog_001"));
            StringAssert.Contains("생일 축하해", FriendQuestText.Reaction(FriendQuestions.Get(PlayerProfile.Birthday), "1-1", "x", new DateTime(2026, 1, 1)));
            StringAssert.Contains("관람차를 누르면 보리가", FriendQuestText.Detail(FriendQuestKind.Ride, 1, "관람차", "보리"));
            StringAssert.Contains("열매 4개", FriendQuestText.Request(FriendQuestKind.Fruit, Personality.Energetic, 4, ""));
            var p = new PlayerProfile(); p.Answer("Color", "분홍색", "Cat_001");
            StringAssert.StartsWith("분홍색을 좋아한다고 했지?", FriendQuestText.Remember(p, new System.Random(1), new DateTime(2026, 1, 1)));
            foreach (var q in FriendQuestions.All.Where(x => x.choices != null)) Assert.AreEqual(q.choices.Length, q.comments.Length, q.key);
        }

        // ---------------- save v7 ----------------
        [Test] public void Save_RoundTripKeepsRequestsAndAnswers()
        {
            var w = W();
            var fruit = Give(w, "Bunny_001", FriendQuestKind.Fruit, 4, 26); w.Report(FriendQuestBook.FruitPicked);
            var ride = Give(w, "Cat_001", FriendQuestKind.Ride, param: "Ride_Carousel"); w.Report(FriendQuestBook.FriendRode, "Cat_001@Ride_Carousel");
            w.Profile.Answer(PlayerProfile.Birthday, "3-15", "Bunny_001"); w.Profile.Answer("Food", "푸딩", "Cat_001");
            w.Profile.TryClaimBirthdayGift(new DateTime(2026, 3, 15));
            var json = JsonUtility.ToJson(SaveMapper.ToSave(new ToyRuntime("TeddyBear_001", new[] { "Thorn" }), null, w));
            var back = new WorldState(); SaveMapper.ApplyWorld(JsonUtility.FromJson<SaveData>(json), back);
            var f2 = back.FriendQuests.Find(fruit.id); var r2 = back.FriendQuests.Find(ride.id);
            Assert.AreEqual(FriendQuestKind.Fruit, f2.kind); Assert.AreEqual(1, f2.progress); Assert.AreEqual(4, f2.target); Assert.AreEqual(26, f2.reward); Assert.IsFalse(f2.Done);
            Assert.IsTrue(r2.Done); Assert.AreEqual("Ride_Carousel", r2.param); Assert.AreEqual("Cat_001", r2.giver);
            Assert.IsTrue(r2.waiting, "still waiting to be told after a restart");
            Assert.AreEqual("3-15", back.Profile.Get(PlayerProfile.Birthday)); Assert.AreEqual("푸딩", back.Profile.Get("Food")); Assert.AreEqual("Cat_001", back.Profile.AskedBy("Food"));
            Assert.AreEqual(2026, back.Profile.BirthdayGiftYear);
            Assert.AreEqual(w.Coins, back.Coins);
            Assert.IsNotNull(back.FriendQuests.Claim("Cat_001")); Assert.AreEqual(w.Coins + 20, back.Coins, "told after the restart: paid then");
            var next = Give(back, "Robot_001", FriendQuestKind.Stars);
            Assert.AreNotEqual(fruit.id, next.id); Assert.AreNotEqual(ride.id, next.id);
            Assert.AreEqual(0, back.FriendQuests.Offers.Count, "bubbles are never saved");
        }

        [Test] public void Save_Version6StillLoads_WithNoRequestsOrAnswers()
        {
            Assert.GreaterOrEqual(SaveData.CurrentVersion, 7);
            var dir = Path.Combine(Path.GetTempPath(), "mongle_fq_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                var svc = new SaveService(Path.Combine(dir, "save.json"));
                File.WriteAllText(svc.Path, "{\"saveVersion\":6,\"toyKindId\":\"TeddyBear_001\",\"coins\":42,\"admitted\":true,\"quests\":[{\"id\":\"Q_Tulip\",\"status\":4,\"progress\":1}]}");
                var r = svc.Load(); Assert.AreEqual(SaveStatus.Ok, r.status);
                var w = new WorldState(); SaveMapper.ApplyWorld(r.data, w);
                Assert.AreEqual(42, w.Coins); Assert.AreEqual(0, w.FriendQuests.Quests.Count); Assert.AreEqual(0, w.Profile.Count);
                Assert.AreEqual(QuestStatus.Done, w.QuestStates["Q_Tulip"].status, "shop quests untouched");
                w.Profile.Answer("Food", "쿠키", "Bunny_001");
                svc.Save(SaveMapper.ToSave(new ToyRuntime("TeddyBear_001", new[] { "Thorn" }), null, w));
                var again = svc.Load(); Assert.AreEqual(SaveData.CurrentVersion, again.data.saveVersion); Assert.AreEqual("쿠키", again.data.answers.Single().value);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test] public void Restore_SkipsBrokenEntries_AndNeverBringsBackAFullUnfinishedRequest()
        {
            var b = new FriendQuestBook(() => 5);
            b.Restore(new List<FriendQuestSave>
            {
                new FriendQuestSave { id = "FQ3", giver = "Bunny_001", kind = "Fruit", target = 3, progress = 9, status = 0 },
                new FriendQuestSave { id = "FQ3", giver = "Cat_001", kind = "Dress", target = 1, status = 0 },          // duplicate id
                new FriendQuestSave { id = "FQ4", giver = "Cat_001", kind = "Teleport", target = 1, status = 0 },       // unknown kind (newer build)
                new FriendQuestSave { id = "FQ5", giver = "", kind = "Stars", target = 1, status = 0 },                 // no giver
                new FriendQuestSave { id = "FQ9", giver = "Robot_001", kind = "Stars", target = 1, progress = 1, status = 1 },
                null,
            }, 2);
            Assert.AreEqual(2, b.Quests.Count);
            Assert.AreEqual(2, b.Find("FQ3").progress, "one more fruit finishes it (and pays)");
            Assert.IsTrue(b.Find("FQ9").Done);
            Assert.AreEqual(9, b.Seq, "new ids continue after the highest saved one");
        }
    }
}
