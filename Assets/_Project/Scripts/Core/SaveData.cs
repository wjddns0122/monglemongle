using System;
using System.Collections.Generic;

namespace Mongle.Core
{
    [Serializable] public class DamageSave { public string id; public bool completed; }
    [Serializable] public class HistorySave { public string key; public string date; public string text; }
    [Serializable] public class AccessorySave { public string slot; public string accessoryId; }
    [Serializable] public class FurnitureSave { public string furnitureId; public string instanceId; public int cellX; public int cellY; public int rotation; }
    [Serializable]
    public class PhotoSave
    {
        public string photoId, residentId, pose, background, imageFile, takenAt;
        public List<string> accessoryIds = new List<string>();
    }
    [Serializable] public class ItemSave { public string itemId; public int count; }
    [Serializable] public class PlantSave { public string instanceId, itemId; public int waters; public long lastWateredUnix; public bool bloomRewarded; }
    [Serializable] public class QuestSave { public string id; public int status; public int progress; }
    [Serializable] public class SettingsSave { public float masterVolume = 0.8f; }
    /// <summary>A request a moved-in friend gave (D-011). kind is the FriendQuestKind name, status 0 = active, 1 = done.</summary>
    [Serializable] public class FriendQuestSave { public string id, giver, kind, param; public int target, progress, reward, status; public long accepted, done; public int waiting; }   // waiting 1: done, the friend not told yet (older saves: 0, already paid)
    /// <summary>What the player told a friend who asked "?" (D-011): key = question (Birthday = "month-day"), askedBy = the friend's kind id.</summary>
    [Serializable] public class AnswerSave { public string key, value, askedBy; }

    /// <summary>A toy friend being treated at the hospital right now (D-005). Empty toyKindId = nobody.</summary>
    [Serializable]
    public class PatientSave
    {
        public string toyKindId, residentId;
        public bool diagnosisComplete, stampDone, customizationConfirmed;
        public List<DamageSave> damages = new List<DamageSave>();
        public List<AccessorySave> equipped = new List<AccessorySave>();
        public List<HistorySave> history = new List<HistorySave>();
        public PhotoSave photo;   // taken after the dress-up, before moving in (v5)
    }
    /// <summary>A repaired toy friend who lives in the village with Mongi (D-005).</summary>
    [Serializable]
    public class ResidentSave
    {
        public string kindId, residentId, movedIn;
        public List<AccessorySave> equipped = new List<AccessorySave>();
        public PhotoSave photo;   // first photo, shown in the album (v5; friends who moved in earlier have none)
        // v8 (D-012): the friend's own house: lot index (HouseLots), gingerbread variant, roof icing colour; -1 = none yet
        public int house = -1, houseVariant = -1, houseTint = -1;
        // v11: the player places the house anywhere on the lawn (D-015)
        public bool housePlaced; public float houseX, houseZ, houseYaw;
    }

    /// <summary>Local JSON schema (05 §8). Static content is never stored here; only player state.</summary>
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 11; // 11: the player chooses where each friend's house stands (D-015), 10: 10: capsules taken from the claw machine (D-014), 9: claw machine hearts (D-014), 8: 8: friends' houses (lot, variant, roof colour per resident, D-012), 7: friends' requests ("!") and the player's answers ("?", D-011), 6: every friend has its own damages (D-009; a patient with outdated damage ids goes back to the diagnosis), 5: friends' photos + accessory shop (cupcake house is no longer furniture), 4: hospital patient + resident friends (D-005), 3: quests (D-003), 2: shops/inventory/plants (D-002); older versions still load
        public int saveVersion = CurrentVersion;
        public int repairShopLevel = 1;
        public int coins = 0;
        public string toyKindId;
        public string residentId;
        public bool diagnosisComplete;
        public bool stampDone;
        public bool customizationConfirmed;
        public bool admitted;
        public string admissionDate;
        public List<DamageSave> damages = new List<DamageSave>();
        public List<AccessorySave> equipped = new List<AccessorySave>();
        public List<HistorySave> history = new List<HistorySave>();
        public PhotoSave photo;
        public List<FurnitureSave> furniture = new List<FurnitureSave>();
        public List<ItemSave> inventory = new List<ItemSave>();
        public List<PlantSave> plants = new List<PlantSave>();
        public List<string> seenInstances = new List<string>();
        public int instanceSeq;
        public List<QuestSave> quests = new List<QuestSave>();
        public PatientSave patient = new PatientSave();
        public List<ResidentSave> residents = new List<ResidentSave>();
        public List<FriendQuestSave> friendQuests = new List<FriendQuestSave>();   // v7
        public int friendQuestSeq;
        public List<AnswerSave> answers = new List<AnswerSave>();                  // v7
        public int birthdayGiftYear;
        public int clawHearts = -1;        // v9: -1 = never played (full)
        public long clawLastUnix;
        public string clawRemoved = "";   // v10: ids of capsules taken out of the pile, oldest first
        public long clawCapUnix;
        public SettingsSave settings = new SettingsSave();
    }
}
