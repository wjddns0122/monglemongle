using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;

namespace Mongle.Village
{
    /// <summary>What a moved-in friend asks the player for (D-011). Saved by name; append only anyway.</summary>
    public enum FriendQuestKind { Fruit, Stars, Ride, Dress, Water }
    public enum FriendQuestStatus { Active, Done }
    /// <summary>The bubble over a friend's head: "!" a request, "?" a question about the player.</summary>
    public enum OfferType { Quest, Question }

    /// <summary>An accepted request. Pays its reward the moment it is done (no claiming at a shop).</summary>
    public class FriendQuest
    {
        public string id, giver, param;      // giver = the friend's kind id (Mongi too); param = ride id for Ride
        public FriendQuestKind kind;
        public int target = 1, progress, reward;
        public FriendQuestStatus status;
        public long acceptedUnix, doneUnix;
        public bool Done => status == FriendQuestStatus.Done;
        public int Left => Math.Max(0, target - progress);
    }

    /// <summary>A bubble waiting over a friend's head until it is clicked or fades.</summary>
    public class FriendOffer
    {
        public string giver;
        public OfferType type;
        public FriendQuestKind kind; public string param; public int target = 1, reward;   // "!"
        public string question;                                                             // "?" (a FriendQuestions key)
        public long createdUnix, expiresUnix;
    }

    public struct OfferCandidate
    {
        public string giver; public Personality personality;
        public OfferCandidate(string giver, Personality personality) { this.giver = giver; this.personality = personality; }
    }

    /// <summary>What the park offers right now, so a friend only asks for something the player can do.</summary>
    public class OfferContext
    {
        public bool trees = true;
        public string[] rides = new string[0];
        public int waterLeft;                         // waterings the placed, still growing plants can take
        public bool hasAccessories = true;            // owned or affordable
        public string[] questions = new string[0];    // question keys nobody has asked yet
    }

    /// <summary>
    /// Friends' requests (D-011): when a "!" or "?" bubble appears (at most two at a time, a gap between bubbles, a few minutes'
    /// rest per friend), what was accepted, and when it is done. Pure logic with an injectable clock and dice.
    /// </summary>
    public class FriendQuestBook
    {
        public const string FruitPicked = "FruitPicked", StarsFound = "StarsFound", FriendRode = "FriendRode";
        public const int StarCount = 5;
        // seconds
        public int FirstDelay = 15, OfferGap = 40, OfferLife = 240, PerToyCooldown = 240, ExpiredCooldown = 120;
        public int MaxOpenOffers = 2, MaxActive = 3, KeepDone = 50;
        public double QuestionChance = 0.4;
        public Func<long> Clock;
        public Random Rng = new Random();
        public readonly List<FriendQuest> Quests = new List<FriendQuest>();
        public readonly List<FriendOffer> Offers = new List<FriendOffer>();
        public int Seq;
        public event Action Changed;
        /// <summary>Accepted, progressed or done.</summary>
        public event Action<FriendQuest> QuestChanged;
        /// <summary>Done just now (the world pays the reward).</summary>
        public event Action<FriendQuest> Completed;
        readonly Dictionary<string, long> nextFrom = new Dictionary<string, long>();
        readonly Dictionary<string, FriendQuestKind> lastKind = new Dictionary<string, FriendQuestKind>();
        long nextAny;

        public FriendQuestBook(Func<long> clock = null) { Clock = clock ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds()); }
        long Now => Clock();

        public IEnumerable<FriendQuest> Active => Quests.Where(q => !q.Done);
        public IEnumerable<FriendQuest> Finished => Quests.Where(q => q.Done).OrderByDescending(q => q.doneUnix);
        public FriendQuest ActiveOf(string giver) => Quests.FirstOrDefault(q => !q.Done && q.giver == giver);
        public FriendQuest Find(string id) => Quests.FirstOrDefault(q => q.id == id);
        public FriendOffer OfferOf(string giver) => Offers.FirstOrDefault(o => o.giver == giver);
        public long NextOfferFrom(string giver) => giver != null && nextFrom.TryGetValue(giver, out var t) ? t : 0;

        public void Reset() { Quests.Clear(); Offers.Clear(); nextFrom.Clear(); lastKind.Clear(); nextAny = 0; Seq = 0; Changed?.Invoke(); }

        /// <summary>No new bubble for a little while (entering the village).</summary>
        public void HoldOffers(int seconds) { nextAny = Math.Max(nextAny, Now + Math.Max(0, seconds)); }

        // ---------------- bubbles ----------------
        /// <summary>Lets old bubbles fade and maybe puts up a new one. Returns the new offer, or null.</summary>
        public FriendOffer Tick(IEnumerable<OfferCandidate> candidates, OfferContext ctx)
        {
            long now = Now; bool changed = false;
            // a bubble nobody clicked fades; that friend waits a little before asking again
            for (int i = Offers.Count - 1; i >= 0; i--)
                if (now >= Offers[i].expiresUnix) { nextFrom[Offers[i].giver] = now + ExpiredCooldown; Offers.RemoveAt(i); changed = true; }
            if (changed) Changed?.Invoke();
            if (Offers.Count >= MaxOpenOffers || now < nextAny || candidates == null) return null;
            ctx = ctx ?? new OfferContext();
            var free = candidates.Where(c => !string.IsNullOrEmpty(c.giver) && OfferOf(c.giver) == null && ActiveOf(c.giver) == null && now >= NextOfferFrom(c.giver))
                .GroupBy(c => c.giver).Select(g => g.First()).ToList();
            if (free.Count == 0) return null;
            var who = free[Rng.Next(free.Count)];
            var openQuestions = (ctx.questions ?? new string[0]).Where(k => !Offers.Any(o => o.type == OfferType.Question && o.question == k)).ToList();
            var kinds = KindsFor(who.giver, ctx);
            bool canQuest = Active.Count() < MaxActive && kinds.Count > 0;
            bool ask = openQuestions.Count > 0 && (!canQuest || Rng.NextDouble() < QuestionChance);
            if (!ask && !canQuest) return null;
            var o = new FriendOffer { giver = who.giver, createdUnix = now, expiresUnix = now + OfferLife };
            if (ask) { o.type = OfferType.Question; o.question = PickQuestion(openQuestions); }
            else Fill(o, PickKind(who, kinds), ctx);
            Offers.Add(o); nextAny = now + OfferGap;
            Changed?.Invoke();
            return o;
        }

        /// <summary>Kinds this friend may ask for now: one of each kind at a time, and not the same as its last one when it can help it.</summary>
        public List<FriendQuestKind> KindsFor(string giver, OfferContext ctx)
        {
            var list = new List<FriendQuestKind>();
            foreach (FriendQuestKind k in Enum.GetValues(typeof(FriendQuestKind)))
            {
                if (k == FriendQuestKind.Fruit && !ctx.trees) continue;
                if (k == FriendQuestKind.Ride && (ctx.rides == null || ctx.rides.Length == 0)) continue;
                if (k == FriendQuestKind.Water && ctx.waterLeft <= 0) continue;
                if (k == FriendQuestKind.Dress && !ctx.hasAccessories) continue;
                if (Active.Any(q => q.kind == k) || Offers.Any(o => o.type == OfferType.Quest && o.kind == k)) continue;
                list.Add(k);
            }
            if (list.Count > 1 && giver != null && lastKind.TryGetValue(giver, out var last)) list.Remove(last);
            return list;
        }

        FriendQuestKind PickKind(OfferCandidate who, List<FriendQuestKind> kinds)
        {
            // 개굴이 likes everything wet: watering a little more than the others
            var w = kinds.Select(k => Weight(k, who.personality) * (k == FriendQuestKind.Water && who.giver != null && who.giver.StartsWith("Frog") ? 1.5 : 1.0)).ToArray();
            double r = Rng.NextDouble() * w.Sum();
            for (int i = 0; i < kinds.Count; i++) { r -= w[i]; if (r <= 0) return kinds[i]; }
            return kinds[kinds.Count - 1];
        }

        /// <summary>Lively friends like shaking trees and rides, sleepy and gentle ones like flowers and dressing up.</summary>
        public static double Weight(FriendQuestKind k, Personality p)
        {
            switch (k)
            {
                case FriendQuestKind.Fruit: return p == Personality.Energetic || p == Personality.Mischievous ? 1.4 : p == Personality.Gentle ? 1.2 : 1.0;
                case FriendQuestKind.Stars: return p == Personality.Curious ? 1.5 : p == Personality.Energetic ? 1.3 : p == Personality.Sleepy ? 0.7 : 1.0;
                case FriendQuestKind.Ride: return p == Personality.Mischievous || p == Personality.Energetic ? 1.4 : p == Personality.Shy ? 0.8 : 1.0;
                case FriendQuestKind.Dress: return p == Personality.Shy || p == Personality.Sleepy ? 1.3 : 0.9;
                default: return p == Personality.Gentle || p == Personality.Shy ? 1.3 : 0.9;
            }
        }

        void Fill(FriendOffer o, FriendQuestKind k, OfferContext ctx)
        {
            o.type = OfferType.Quest; o.kind = k; o.target = 1;
            switch (k)
            {
                case FriendQuestKind.Fruit: o.target = 3 + Rng.Next(3); o.reward = 10 + 4 * o.target; break;
                case FriendQuestKind.Stars: o.reward = 30; break;
                case FriendQuestKind.Ride: o.param = ctx.rides[Rng.Next(ctx.rides.Length)]; o.reward = 20; break;
                case FriendQuestKind.Dress: o.reward = 20; break;
                case FriendQuestKind.Water: o.target = Math.Max(1, Math.Min(3, ctx.waterLeft)); o.reward = 8 + 4 * o.target; break;
            }
        }

        string PickQuestion(List<string> keys)
        {
            // the birthday is the one friends are most curious about
            var w = keys.Select(k => k == PlayerProfile.Birthday ? 2.0 : 1.0).ToArray();
            double r = Rng.NextDouble() * w.Sum();
            for (int i = 0; i < keys.Count; i++) { r -= w[i]; if (r <= 0) return keys[i]; }
            return keys[keys.Count - 1];
        }

        /// <summary>The player said yes: the request goes into the quest window.</summary>
        public FriendQuest Accept(FriendOffer o)
        {
            if (o == null || o.type != OfferType.Quest || !Offers.Contains(o)) return null;
            Offers.Remove(o);
            var q = new FriendQuest { id = "FQ" + (++Seq), giver = o.giver, kind = o.kind, param = o.param, target = Math.Max(1, o.target), reward = Math.Max(0, o.reward), acceptedUnix = Now };
            Quests.Add(q); lastKind[o.giver] = o.kind;
            Changed?.Invoke(); QuestChanged?.Invoke(q);
            return q;
        }

        /// <summary>The player answered a "?" (the answer itself goes to the PlayerProfile).</summary>
        public bool Answered(FriendOffer o)
        {
            if (o == null || !Offers.Remove(o)) return false;
            nextFrom[o.giver] = Now + PerToyCooldown;
            Changed?.Invoke(); return true;
        }

        /// <summary>Takes a bubble down without an answer (its question was answered elsewhere, the friend left).</summary>
        public bool Withdraw(FriendOffer o)
        {
            if (o == null || !Offers.Remove(o)) return false;
            nextFrom[o.giver] = Now + ExpiredCooldown;
            Changed?.Invoke(); return true;
        }

        // ---------------- progress ----------------
        /// <summary>
        /// Something happened in the village. Fruit and waterings count for the oldest matching request; stars, rides and
        /// dressing up count only for the friend named in <paramref name="param"/> (a ride as "giver@rideId").
        /// </summary>
        public bool Report(string eventId, string param)
        {
            FriendQuest hit = null;
            switch (eventId)
            {
                case FruitPicked: hit = Active.FirstOrDefault(q => q.kind == FriendQuestKind.Fruit); break;
                case "PlantWatered": hit = Active.FirstOrDefault(q => q.kind == FriendQuestKind.Water); break;
                case StarsFound: hit = Active.FirstOrDefault(q => q.kind == FriendQuestKind.Stars && q.giver == param); break;
                case "AccessoryWorn": hit = Active.FirstOrDefault(q => q.kind == FriendQuestKind.Dress && q.giver == param); break;
                case FriendRode: hit = Active.FirstOrDefault(q => q.kind == FriendQuestKind.Ride && param == q.giver + "@" + q.param); break;
            }
            if (hit == null) return false;
            hit.progress = Math.Min(hit.target, hit.progress + 1);
            if (hit.progress >= hit.target)
            {
                hit.status = FriendQuestStatus.Done; hit.doneUnix = Now;
                nextFrom[hit.giver] = Now + PerToyCooldown;
                Completed?.Invoke(hit);
                TrimDone();
            }
            Changed?.Invoke(); QuestChanged?.Invoke(hit);
            return true;
        }

        void TrimDone()
        {
            var done = Quests.Where(q => q.Done).OrderBy(q => q.doneUnix).ToList();
            for (int i = 0; i < done.Count - KeepDone; i++) Quests.Remove(done[i]);
        }

        // ---------------- save ----------------
        public List<FriendQuestSave> ToSave() => Quests.Select(q => new FriendQuestSave
        {
            id = q.id, giver = q.giver, kind = q.kind.ToString(), param = q.param ?? "", target = q.target, progress = q.progress, reward = q.reward,
            status = q.Done ? 1 : 0, accepted = q.acceptedUnix, done = q.doneUnix
        }).ToList();

        /// <summary>Back from a save; unknown kinds (a newer build) and duplicates are skipped. Bubbles are never saved.</summary>
        public void Restore(IEnumerable<FriendQuestSave> saved, int seq)
        {
            Quests.Clear(); Offers.Clear(); nextFrom.Clear(); lastKind.Clear(); nextAny = 0;
            int maxSeq = 0;
            if (saved != null)
                foreach (var s in saved)
                {
                    if (s == null || string.IsNullOrEmpty(s.id) || string.IsNullOrEmpty(s.giver) || Quests.Any(q => q.id == s.id)) continue;
                    if (!Enum.TryParse(s.kind, out FriendQuestKind k) || !Enum.IsDefined(typeof(FriendQuestKind), k)) continue;
                    int target = Math.Max(1, s.target);
                    bool done = s.status == 1;
                    // an unfinished request never comes back full: one more step finishes it and pays
                    int progress = Math.Max(0, Math.Min(done ? target : target - 1, s.progress));
                    Quests.Add(new FriendQuest { id = s.id, giver = s.giver, kind = k, param = s.param, target = target, progress = progress, reward = Math.Max(0, s.reward),
                        status = done ? FriendQuestStatus.Done : FriendQuestStatus.Active, acceptedUnix = s.accepted, doneUnix = s.done });
                    if (s.id.StartsWith("FQ") && int.TryParse(s.id.Substring(2), out var n)) maxSeq = Math.Max(maxSeq, n);
                }
            Seq = Math.Max(Math.Max(0, seq), maxSeq);
            Changed?.Invoke();
        }
    }

    /// <summary>What the player told the friends (D-011): birthday, favourite food, toy, colour… Each question is asked once.</summary>
    public class PlayerProfile
    {
        public const string Birthday = "Birthday";
        readonly Dictionary<string, AnswerSave> answers = new Dictionary<string, AnswerSave>();
        public int BirthdayGiftYear;
        public event Action Changed;

        public int Count => answers.Count;
        public IEnumerable<string> Keys => answers.Keys;
        public bool Has(string key) => key != null && answers.ContainsKey(key);
        public string Get(string key) => key != null && answers.TryGetValue(key, out var a) ? a.value : null;
        public string AskedBy(string key) => key != null && answers.TryGetValue(key, out var a) ? a.askedBy : null;
        public IEnumerable<string> Unasked(IEnumerable<string> keys) => keys.Where(k => !Has(k));

        /// <summary>Stores an answer once (false when already answered, empty, or not a real date for the birthday).</summary>
        public bool Answer(string key, string value, string askedBy)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || Has(key)) return false;
            if (key == Birthday && !ParseBirthday(value, out _, out _)) return false;
            answers[key] = new AnswerSave { key = key, value = value, askedBy = askedBy ?? "" };
            Changed?.Invoke(); return true;
        }

        // ---------------- birthday ----------------
        public static string BirthdayValue(int month, int day) => month + "-" + day;
        public static int DaysIn(int month) => month == 2 ? 29 : month == 4 || month == 6 || month == 9 || month == 11 ? 30 : 31;

        public static bool ParseBirthday(string value, out int month, out int day)
        {
            month = day = 0;
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('-');
            if (parts.Length != 2 || !int.TryParse(parts[0], out month) || !int.TryParse(parts[1], out day)) { month = day = 0; return false; }
            if (month < 1 || month > 12 || day < 1 || day > DaysIn(month)) { month = day = 0; return false; }
            return true;
        }

        public bool TryBirthday(out int month, out int day) => ParseBirthday(Get(Birthday), out month, out day);

        /// <summary>A 29 February birthday is celebrated on the 28th in common years.</summary>
        public static bool IsBirthday(int month, int day, DateTime date)
        {
            if (month == date.Month && day == date.Day) return true;
            return month == 2 && day == 29 && !DateTime.IsLeapYear(date.Year) && date.Month == 2 && date.Day == 28;
        }

        public bool IsBirthdayToday(DateTime date) => TryBirthday(out var m, out var d) && IsBirthday(m, d, date);

        /// <summary>The friends' birthday present, once a year on the day.</summary>
        public bool TryClaimBirthdayGift(DateTime date)
        {
            if (!IsBirthdayToday(date) || BirthdayGiftYear >= date.Year) return false;
            BirthdayGiftYear = date.Year; Changed?.Invoke(); return true;
        }

        // ---------------- save ----------------
        public void Reset() { answers.Clear(); BirthdayGiftYear = 0; Changed?.Invoke(); }

        public List<AnswerSave> ToSave() => answers.Values.Select(a => new AnswerSave { key = a.key, value = a.value, askedBy = a.askedBy }).ToList();

        public void Restore(IEnumerable<AnswerSave> saved, int giftYear)
        {
            answers.Clear();
            if (saved != null)
                foreach (var a in saved)
                {
                    if (a == null || string.IsNullOrEmpty(a.key) || string.IsNullOrEmpty(a.value) || answers.ContainsKey(a.key)) continue;
                    if (a.key == Birthday && !ParseBirthday(a.value, out _, out _)) continue;
                    answers[a.key] = new AnswerSave { key = a.key, value = a.value, askedBy = a.askedBy ?? "" };
                }
            BirthdayGiftYear = Math.Max(0, giftYear);
            Changed?.Invoke();
        }
    }
}
