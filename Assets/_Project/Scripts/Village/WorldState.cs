using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;

namespace Mongle.Village
{
    public class PlantState
    {
        public string instanceId, itemId;
        public int waters;
        public long lastWateredUnix;
        public bool bloomRewarded;
    }

    public enum QuestStatus { Locked, Available, Active, ReadyToClaim, Done }
    public class QuestState { public string id; public QuestStatus status; public int progress; }

    public enum BuyResult { Ok, NotEnoughCoins, NotForSale }
    public enum WaterResult { Ok, TooSoon, FullyGrown, Unknown }

    /// <summary>
    /// Player-side village state: coins, owned-but-unplaced items, plant growth, which pieces Mongi has already reacted to.
    /// Pure logic (no Unity objects) so the rules are unit-testable; the clock is injectable.
    /// </summary>
    public class WorldState
    {
        public int Coins { get; private set; }
        public readonly Dictionary<string, int> Inventory = new Dictionary<string, int>();
        /// <summary>Tries at the claw machine (D-014).</summary>
        public readonly ClawHearts Claw = new ClawHearts();
        /// <summary>The claw machine's pile: capsules that were won stay out and come back one per 30 minutes.</summary>
        public readonly ClawStock ClawCaps = new ClawStock();
        public readonly List<PlantState> Plants = new List<PlantState>();
        public readonly HashSet<string> Seen = new HashSet<string>();
        public int InstanceSeq;
        public Func<long> Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public event Action Changed;
        public event Action<QuestDefinition> QuestChanged;
        public readonly Dictionary<string, QuestState> QuestStates = new Dictionary<string, QuestState>();
        QuestDefinition[] questDefs = new QuestDefinition[0];
        /// <summary>Requests from the friends who moved in ("!", D-011); a finished one pays its coins at once.</summary>
        public FriendQuestBook FriendQuests { get; }
        /// <summary>What the player told the friends ("?", D-011).</summary>
        public PlayerProfile Profile { get; }

        public WorldState()
        {
            FriendQuests = new FriendQuestBook(() => Clock());
            FriendQuests.Claimed += q => Earn(q.reward);   // when the friend is told (user 2026-11-27), not the moment it is done
            Profile = new PlayerProfile();
        }

        void Notify() => Changed?.Invoke();

        public void Reset()
        {
            Coins = 0; Inventory.Clear(); Plants.Clear(); Seen.Clear(); QuestStates.Clear(); InstanceSeq = 0;
            FriendQuests.Reset(); Profile.Reset();
            Notify();
        }

        public void Restore(int coins, IEnumerable<ItemSave> inv, IEnumerable<PlantSave> plants, IEnumerable<string> seen, int seq, IEnumerable<QuestSave> quests = null)
        {
            Reset();
            Coins = Math.Max(0, coins);
            if (inv != null) foreach (var i in inv) if (!string.IsNullOrEmpty(i.itemId) && i.count > 0) Inventory[i.itemId] = i.count;
            if (plants != null)
                foreach (var p in plants)
                    if (!string.IsNullOrEmpty(p.instanceId) && Plants.All(x => x.instanceId != p.instanceId))
                        Plants.Add(new PlantState { instanceId = p.instanceId, itemId = p.itemId, waters = Math.Max(0, p.waters), lastWateredUnix = p.lastWateredUnix, bloomRewarded = p.bloomRewarded });
            if (seen != null) foreach (var s in seen) if (!string.IsNullOrEmpty(s)) Seen.Add(s);
            InstanceSeq = Math.Max(0, seq);
            if (quests != null)
                foreach (var q in quests)
                {
                    if (string.IsNullOrEmpty(q.id) || QuestStates.ContainsKey(q.id)) continue;
                    var st = (QuestStatus)Math.Max((int)QuestStatus.Active, Math.Min((int)QuestStatus.Done, q.status)); // only Active..Done are ever stored
                    QuestStates[q.id] = new QuestState { id = q.id, status = st, progress = Math.Max(0, q.progress) };
                }
            Notify();
        }

        // ---------------- coins ----------------
        public void Earn(int amount) { if (amount <= 0) return; Coins += amount; Notify(); }
        public bool TrySpend(int amount) { if (amount < 0 || Coins < amount) return false; Coins -= amount; Notify(); return true; }

        // ---------------- shopping ----------------
        // ---------------- quests ----------------
        public void SetQuestDefinitions(IEnumerable<QuestDefinition> defs) { questDefs = defs == null ? new QuestDefinition[0] : defs.Where(d => d != null).ToArray(); }
        public IReadOnlyList<QuestDefinition> AllQuests => questDefs;
        public IReadOnlyList<QuestDefinition> QuestsOf(string shopId) => questDefs.Where(q => q.giverShopId == shopId).ToList();

        public QuestStatus StatusOf(QuestDefinition d)
        {
            if (d == null) return QuestStatus.Locked;
            if (QuestStates.TryGetValue(d.id, out var s)) return s.status;
            if (!string.IsNullOrEmpty(d.requires) && !(QuestStates.TryGetValue(d.requires, out var pre) && pre.status == QuestStatus.Done)) return QuestStatus.Locked;
            return QuestStatus.Available;
        }

        public int ProgressOf(QuestDefinition d) => d != null && QuestStates.TryGetValue(d.id, out var s) ? s.progress : 0;

        public bool AcceptQuest(QuestDefinition d)
        {
            if (StatusOf(d) != QuestStatus.Available) return false;
            QuestStates[d.id] = new QuestState { id = d.id, status = QuestStatus.Active };
            Notify(); QuestChanged?.Invoke(d); return true;
        }

        /// <summary>Something happened in the village. Counts for every accepted (Active) quest whose condition matches.</summary>
        public void Report(string eventId, string param = "")
        {
            foreach (var d in questDefs)
            {
                if (d.eventId != eventId) continue;
                if (!string.IsNullOrEmpty(d.eventParam) && d.eventParam != param) continue;
                if (!QuestStates.TryGetValue(d.id, out var s) || s.status != QuestStatus.Active) continue;
                s.progress = Math.Min(d.targetCount, s.progress + 1);
                if (s.progress >= d.targetCount) s.status = QuestStatus.ReadyToClaim;
                Notify(); QuestChanged?.Invoke(d);
            }
            FriendQuests.Report(eventId, param); // the friends' requests listen to the same village events (D-011)
        }

        /// <summary>Pay out a finished quest once.</summary>
        public bool ClaimQuest(QuestDefinition d)
        {
            if (StatusOf(d) != QuestStatus.ReadyToClaim) return false;
            QuestStates[d.id].status = QuestStatus.Done;
            Earn(d.rewardCoins); Notify(); QuestChanged?.Invoke(d); return true;
        }

        public BuyResult Buy(FurnitureDefinition def)
        {
            if (def == null || def.price <= 0) return BuyResult.NotForSale;
            if (!TrySpend(def.price)) return BuyResult.NotEnoughCoins;
            AddToInventory(def.id);
            Report("ItemBought", def.id);
            return BuyResult.Ok;
        }

        public int Owned(string itemId) => Inventory.TryGetValue(itemId, out var c) ? c : 0;
        public void AddToInventory(string itemId, int n = 1) { Inventory[itemId] = Owned(itemId) + n; Notify(); }
        public bool TakeFromInventory(string itemId)
        {
            int c = Owned(itemId); if (c <= 0) return false;
            if (c == 1) Inventory.Remove(itemId); else Inventory[itemId] = c - 1;
            Notify(); return true;
        }

        public string NewInstanceId(string itemId) => itemId + "#" + (++InstanceSeq);

        // ---------------- reactions ----------------
        public bool HasSeen(string instanceId) => Seen.Contains(instanceId);
        public void MarkSeen(string instanceId) { if (Seen.Add(instanceId)) Notify(); }

        // ---------------- plants ----------------
        public PlantState Plant(string instanceId) => Plants.FirstOrDefault(p => p.instanceId == instanceId);

        public PlantState EnsurePlant(string instanceId, string itemId)
        {
            var p = Plant(instanceId);
            if (p == null) { p = new PlantState { instanceId = instanceId, itemId = itemId }; Plants.Add(p); Notify(); }
            return p;
        }

        public int StageOf(PlantState p, FurnitureDefinition def)
        {
            if (p == null || def == null || def.stageWaters == null) return 0;
            int stage = 0;
            for (int i = 0; i < def.stageWaters.Length; i++) if (p.waters >= def.stageWaters[i]) stage = i + 1;
            return stage;
        }

        public bool IsFullyGrown(PlantState p, FurnitureDefinition def) => StageOf(p, def) >= def.MaxStage;

        /// <summary>Seconds until this plant can be watered again (0 = thirsty now).</summary>
        public float SecondsUntilThirsty(PlantState p, FurnitureDefinition def)
        {
            if (p == null || p.lastWateredUnix <= 0) return 0f;
            // double: unix seconds (~1.7e9) do not fit a float without losing whole seconds
            double left = (double)p.lastWateredUnix + def.waterCooldownSeconds - Clock();
            return left <= 0.0 ? 0f : (float)left;
        }

        public bool NeedsWater(PlantState p, FurnitureDefinition def) => p != null && !IsFullyGrown(p, def) && SecondsUntilThirsty(p, def) <= 0f;

        public WaterResult Water(string instanceId, FurnitureDefinition def, out bool stageUp, out int bloomCoins)
        {
            stageUp = false; bloomCoins = 0;
            var p = Plant(instanceId);
            if (p == null || def == null) return WaterResult.Unknown;
            if (IsFullyGrown(p, def)) return WaterResult.FullyGrown;
            if (SecondsUntilThirsty(p, def) > 0f) return WaterResult.TooSoon;
            int before = StageOf(p, def);
            p.waters++; p.lastWateredUnix = Clock();
            stageUp = StageOf(p, def) > before;
            if (IsFullyGrown(p, def) && !p.bloomRewarded) { p.bloomRewarded = true; bloomCoins = def.bloomReward; Earn(bloomCoins); }
            Notify();
            Report("PlantWatered", def.id);
            if (bloomCoins > 0) Report("PlantBloomed", def.id);
            return WaterResult.Ok;
        }
    }
}
