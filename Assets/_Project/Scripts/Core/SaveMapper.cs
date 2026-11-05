using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Data;
using Mongle.Toy;

namespace Mongle.Core
{
    /// <summary>Maps runtime state to/from save data. Unfinished repair progress is intentionally dropped.</summary>
    public static class SaveMapper
    {
        public static SaveData ToSave(ToyRuntime t, SettingsSave settings = null, Mongle.Village.WorldState world = null)
        {
            var s = new SaveData
            {
                toyKindId = t.KindId,
                residentId = t.ResidentId,
                diagnosisComplete = t.DiagnosisComplete,
                stampDone = t.StampDone,
                customizationConfirmed = t.CustomizationConfirmed,
                admitted = t.Admitted,
                admissionDate = t.AdmissionDate,
                settings = settings ?? new SettingsSave()
            };
            foreach (var d in t.Damages) s.damages.Add(new DamageSave { id = d.id, completed = d.status == DamageStatus.Completed });
            if (t.CustomizationConfirmed)
                foreach (var kv in t.Equipped) s.equipped.Add(new AccessorySave { slot = kv.Key.ToString(), accessoryId = kv.Value });
            foreach (var h in t.History) s.history.Add(new HistorySave { key = h.key, date = h.date, text = h.text });
            if (t.Photo != null) s.photo = ToPhotoSave(t.Photo);
            foreach (var f in t.Furniture) s.furniture.Add(new FurnitureSave { furnitureId = f.furnitureId, instanceId = f.Id, cellX = f.cellX, cellY = f.cellY, rotation = f.rotation });
            if (world != null)
            {
                s.coins = world.Coins; s.instanceSeq = world.InstanceSeq;
                foreach (var kv in world.Inventory) s.inventory.Add(new ItemSave { itemId = kv.Key, count = kv.Value });
                foreach (var p in world.Plants) s.plants.Add(new PlantSave { instanceId = p.instanceId, itemId = p.itemId, waters = p.waters, lastWateredUnix = p.lastWateredUnix, bloomRewarded = p.bloomRewarded });
                s.seenInstances.AddRange(world.Seen);
                foreach (var q in world.QuestStates.Values) s.quests.Add(new QuestSave { id = q.id, status = (int)q.status, progress = q.progress });
                s.friendQuests = world.FriendQuests.ToSave(); s.friendQuestSeq = world.FriendQuests.Seq;
                s.answers = world.Profile.ToSave(); s.birthdayGiftYear = world.Profile.BirthdayGiftYear;
                world.Claw.Refresh(); s.clawHearts = world.Claw.Count; s.clawLastUnix = world.Claw.LastUnix;
                world.ClawCaps.Refresh(); s.clawRemoved = world.ClawCaps.Serialize(); s.clawCapUnix = world.ClawCaps.LastUnix;
            }
            return s;
        }

        public static void ApplyWorld(SaveData s, Mongle.Village.WorldState world)
        {
            world.Restore(s.coins, s.inventory, s.plants, s.seenInstances, s.instanceSeq, s.quests);
            // v7 (D-011): friends' requests and the player's answers; older saves have none (empty lists)
            world.FriendQuests.Restore(s.friendQuests, s.friendQuestSeq);
            world.Profile.Restore(s.answers, s.birthdayGiftYear);
            world.Claw.Restore(s.clawHearts, s.clawLastUnix);
            world.ClawCaps.Restore(s.clawRemoved, s.clawCapUnix);   // v9; older saves: full hearts
        }

        // ---------------- hospital patient and resident friends (D-005) ----------------
        public static PhotoSave ToPhotoSave(PhotoRecord p) => p == null ? null : new PhotoSave
        {
            photoId = p.photoId, residentId = p.residentId, pose = p.pose, background = p.background, imageFile = p.imageFile, takenAt = p.takenAt,
            accessoryIds = new List<string>(p.accessoryIds ?? new List<string>())
        };

        /// <summary>Null for a missing or empty record (JsonUtility creates empty nested objects).</summary>
        public static PhotoRecord FromPhotoSave(PhotoSave s) => s == null || string.IsNullOrEmpty(s.photoId) ? null : new PhotoRecord
        {
            photoId = s.photoId, residentId = s.residentId, pose = s.pose, background = s.background, imageFile = s.imageFile, takenAt = s.takenAt,
            accessoryIds = new List<string>(s.accessoryIds ?? new List<string>())
        };

        public static PatientSave ToPatientSave(ToyRuntime p)
        {
            var s = new PatientSave();
            if (p == null) return s;
            s.toyKindId = p.KindId; s.residentId = p.ResidentId;
            s.diagnosisComplete = p.DiagnosisComplete; s.stampDone = p.StampDone; s.customizationConfirmed = p.CustomizationConfirmed;
            foreach (var d in p.Damages) s.damages.Add(new DamageSave { id = d.id, completed = d.status == DamageStatus.Completed });
            foreach (var kv in p.Visible) s.equipped.Add(new AccessorySave { slot = kv.Key.ToString(), accessoryId = kv.Value });
            foreach (var h in p.History) s.history.Add(new HistorySave { key = h.key, date = h.date, text = h.text });
            s.photo = ToPhotoSave(p.Photo);
            return s;
        }

        /// <summary>Null when there is no patient (JsonUtility always creates the object, so an empty kind id means nobody).</summary>
        public static ToyRuntime FromPatientSave(PatientSave p, IEnumerable<string> orderedDamageIds)
        {
            if (p == null || string.IsNullOrEmpty(p.toyKindId)) return null;
            var ids = orderedDamageIds.ToList();
            var flat = new SaveData
            {
                toyKindId = p.toyKindId, residentId = p.residentId, diagnosisComplete = p.diagnosisComplete, stampDone = p.stampDone,
                customizationConfirmed = p.customizationConfirmed, damages = p.damages ?? new List<DamageSave>(), history = p.history ?? new List<HistorySave>(),
                equipped = p.equipped ?? new List<AccessorySave>(), photo = p.photo
            };
            if (!SameIds(flat.damages, ids))
            {
                // v6 (D-009): the friend's hurts changed since this save. Still being treated → back to the diagnosis with the new list;
                // already stamped → every new damage counts as repaired.
                flat.damages = p.stampDone ? ids.Select(id => new DamageSave { id = id, completed = true }).ToList() : new List<DamageSave>();
                if (!p.stampDone) flat.diagnosisComplete = false;
            }
            var t = FromSave(flat, p.toyKindId, ids);
            if (!t.CustomizationConfirmed) t.Equipped.Clear(); // still choosing: the saved picks are only a preview
            return t;
        }

        /// <summary>True when the saved damage list holds exactly the definition's damage ids (order aside).</summary>
        public static bool SameIds(List<DamageSave> saved, IList<string> ids)
        {
            if (saved == null) return ids.Count == 0;
            var have = saved.Where(d => d != null).Select(d => d.id).ToList();
            return have.Count == ids.Count && have.Distinct().Count() == have.Count && ids.All(have.Contains);
        }

        public static void WriteFriends(SaveData s, ToyRuntime patient, IEnumerable<ResidentSave> residents)
        {
            s.patient = ToPatientSave(patient);
            s.residents = residents != null ? residents.Select(r => new ResidentSave { kindId = r.kindId, residentId = r.residentId, movedIn = r.movedIn, equipped = new List<AccessorySave>(r.equipped ?? new List<AccessorySave>()), photo = r.photo, house = r.house, houseVariant = r.houseVariant, houseTint = r.houseTint, housePlaced = r.housePlaced, houseX = r.houseX, houseZ = r.houseZ, houseYaw = r.houseYaw }).ToList() : new List<ResidentSave>();
        }

        /// <summary>Residents with a known kind, at most one per kind (repairs a hand-edited or duplicated save).</summary>
        public static List<ResidentSave> ReadResidents(SaveData s, System.Func<string, bool> knownKind)
        {
            var list = new List<ResidentSave>();
            foreach (var r in s.residents ?? new List<ResidentSave>())
                if (r != null && !string.IsNullOrEmpty(r.kindId) && knownKind(r.kindId) && !list.Any(x => x.kindId == r.kindId)) list.Add(r);
            return list;
        }

        public static ToyRuntime FromSave(SaveData s, string kindId, IEnumerable<string> orderedDamageIds)
        {
            var ids = orderedDamageIds.ToList();
            var t = new ToyRuntime(kindId, ids);
            foreach (var d in s.damages)
            {
                var rt = t.Get(d.id);
                if (rt != null && d.completed) rt.status = DamageStatus.Completed;
            }
            t.RestoreFlags(s.diagnosisComplete, s.stampDone, s.customizationConfirmed, s.admitted, s.residentId, s.admissionDate);
            // A completed damage implies the diagnosis happened; keep order consistent.
            if (t.Damages.Any(d => d.status == DamageStatus.Completed) && !t.DiagnosisComplete)
                t.RestoreFlags(true, s.stampDone, s.customizationConfirmed, s.admitted, s.residentId, s.admissionDate);
            foreach (var d in t.Damages)
                if (d.status == DamageStatus.Hidden && t.DiagnosisComplete) d.status = DamageStatus.Discovered;
            t.RestoreAfterDamages();
            foreach (var a in s.equipped)
                if (Enum.TryParse(a.slot, out AccessorySlot slot)) { t.Equipped[slot] = a.accessoryId; t.Preview[slot] = a.accessoryId; }
            foreach (var h in s.history)
                if (!t.History.Any(x => x.key == h.key)) t.History.Add(new HistoryEntry { key = h.key, date = h.date, text = h.text });
            t.Photo = FromPhotoSave(s.photo);
            foreach (var f in s.furniture) t.Furniture.Add(new FurniturePlacement { furnitureId = f.furnitureId, instanceId = string.IsNullOrEmpty(f.instanceId) ? f.furnitureId : f.instanceId, cellX = f.cellX, cellY = f.cellY, rotation = f.rotation });
            return t;
        }
    }
}
