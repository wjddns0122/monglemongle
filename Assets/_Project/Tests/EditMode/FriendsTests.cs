using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>Hospital friends (D-005): patient journey, save round trip of patient and residents, old saves.</summary>
    public class FriendsTests
    {
        static readonly string[] Plush = { "Thorn", "Dirt", "Tear", "LowCotton" };
        static readonly string[] Hard = { "Thorn", "Dirt" };

        static ToyRuntime Repaired(string kind, string[] ids)
        {
            var t = new ToyRuntime(kind, ids); t.EnsureResidentId("r-" + kind);
            foreach (var id in ids) t.Discover(id);
            foreach (var id in ids) { t.BeginRepair(id); t.CompleteDamage(id); }
            return t;
        }

        [Test]
        public void PatientJourneyHasAPhotoButNoAdmission()
        {
            var t = new ToyRuntime("Robot_001", Hard);
            Assert.AreEqual(FlowPhase.CustomerArrival, FlowResolver.ResolvePatient(t));
            foreach (var id in Hard) t.Discover(id);
            Assert.AreEqual(FlowPhase.Repair, FlowResolver.ResolvePatient(t));
            foreach (var id in Hard) { t.BeginRepair(id); t.CompleteDamage(id); }
            Assert.AreEqual(FlowPhase.CompletionStamp, FlowResolver.ResolvePatient(t));
            Assert.IsTrue(t.CompleteStamp());
            Assert.AreEqual(FlowPhase.Customization, FlowResolver.ResolvePatient(t));
            Assert.IsTrue(t.ConfirmCustomization());
            Assert.AreEqual(FlowPhase.PhotoStudio, FlowResolver.ResolvePatient(t), "after the dress-up the friend gets a photo for the album");
            Assert.IsTrue(t.ConfirmPhoto(new PhotoRecord { photoId = "p1", residentId = "r", pose = "Happy", imageFile = "photo_r.png" }));
            Assert.AreEqual(FlowPhase.Village, FlowResolver.ResolvePatient(t), "then it moves into the village (no admission step)");
        }

        [Test]
        public void PatientPhotoSurvivesSaveRoundTrip()
        {
            var t = Repaired("Bunny_001", Plush); t.CompleteStamp(); t.ConfirmCustomization();
            t.ConfirmPhoto(new PhotoRecord { photoId = "p9", residentId = t.ResidentId, pose = "Wave", imageFile = "photo_r-Bunny_001.png", takenAt = "2026-10-09 20:00" });
            var json = JsonUtility.ToJson(new SaveData { toyKindId = "TeddyBear_001", patient = SaveMapper.ToPatientSave(t) });
            var back = SaveMapper.FromPatientSave(JsonUtility.FromJson<SaveData>(json).patient, Plush);
            Assert.IsTrue(back.PhotoConfirmed);
            Assert.AreEqual("photo_r-Bunny_001.png", back.Photo.imageFile);
            Assert.AreEqual(FlowPhase.Village, FlowResolver.ResolvePatient(back));
        }

        [Test]
        public void ResidentKeepsItsPhotoForTheAlbum()
        {
            var data = new SaveData { toyKindId = "TeddyBear_001" };
            var photo = SaveMapper.ToPhotoSave(new PhotoRecord { photoId = "p2", residentId = "c", pose = "Happy", imageFile = "photo_c.png", takenAt = "2026-10-09 20:10" });
            SaveMapper.WriteFriends(data, null, new List<ResidentSave> { new ResidentSave { kindId = "Cat_001", residentId = "c", photo = photo }, new ResidentSave { kindId = "Robot_001", residentId = "b" } });
            var back = SaveMapper.ReadResidents(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data)), k => true);
            Assert.AreEqual("photo_c.png", SaveMapper.FromPhotoSave(back[0].photo).imageFile);
            Assert.IsNull(SaveMapper.FromPhotoSave(back[1].photo), "a friend who moved in before photos has none");
        }

        [Test]
        public void AccessoriesAreWornNotPlaced()
        {
            var acc = ScriptableObject.CreateInstance<FurnitureDefinition>(); acc.id = "Acc_RedRibbon"; acc.category = ItemCategory.Accessory; acc.price = 20;
            var chair = ScriptableObject.CreateInstance<FurnitureDefinition>(); chair.id = "Fur_Armchair"; chair.category = ItemCategory.Furniture; chair.price = 45;
            Assert.IsFalse(acc.Placeable); Assert.IsFalse(acc.Storable);
            Assert.IsTrue(chair.Placeable); Assert.IsTrue(chair.Storable);
            var w = new Mongle.Village.WorldState(); w.Earn(30);
            Assert.AreEqual(Mongle.Village.BuyResult.Ok, w.Buy(acc));
            Assert.AreEqual(1, w.Owned("Acc_RedRibbon"));
            Assert.IsTrue(w.TakeFromInventory("Acc_RedRibbon"), "dressing a friend uses one up");
            Assert.AreEqual(0, w.Owned("Acc_RedRibbon"));
            Object.DestroyImmediate(acc); Object.DestroyImmediate(chair);
        }

        [Test]
        public void PatientMidRepairSurvivesSaveRoundTrip()
        {
            var t = new ToyRuntime("Bunny_001", Plush); t.EnsureResidentId("bunny-1");
            foreach (var id in Plush) t.Discover(id);
            t.BeginRepair("Thorn"); t.CompleteDamage("Thorn");
            t.BeginRepair("Dirt"); t.SetProgress("Dirt", 0.6f); // unfinished: dropped on save (D-001-9)
            var json = JsonUtility.ToJson(new SaveData { toyKindId = "TeddyBear_001", patient = SaveMapper.ToPatientSave(t) });
            var back = SaveMapper.FromPatientSave(JsonUtility.FromJson<SaveData>(json).patient, Plush);
            Assert.AreEqual("Bunny_001", back.KindId);
            Assert.AreEqual("bunny-1", back.ResidentId);
            Assert.IsTrue(back.DiagnosisComplete);
            Assert.AreEqual(DamageStatus.Completed, back.Get("Thorn").status);
            Assert.AreEqual(DamageStatus.Available, back.Get("Dirt").status, "unfinished repair restarts");
            Assert.AreEqual(FlowPhase.Repair, FlowResolver.ResolvePatient(back));
        }

        [Test]
        public void GiftPreviewIsNotEquippedUntilConfirmed()
        {
            var t = Repaired("Cat_001", Plush); t.CompleteStamp();
            t.SetPreview(AccessorySlot.Neck, "Acc_RedRibbon");
            var back = SaveMapper.FromPatientSave(SaveMapper.ToPatientSave(t), Plush);
            Assert.AreEqual(0, back.Equipped.Count);
            Assert.AreEqual("Acc_RedRibbon", back.Visible[AccessorySlot.Neck]);
        }

        [Test]
        public void NoPatientWhenKindIsEmpty()
        {
            // JsonUtility always creates nested objects, so an empty kind id means "nobody at the hospital"
            var data = JsonUtility.FromJson<SaveData>("{\"saveVersion\":3,\"toyKindId\":\"TeddyBear_001\"}");
            Assert.IsNull(SaveMapper.FromPatientSave(data.patient, Plush));
            Assert.AreEqual(0, SaveMapper.ReadResidents(data, k => true).Count, "a v3 save has no residents");
        }

        [Test]
        public void ResidentsRoundTripAndDeduplicate()
        {
            var data = new SaveData { toyKindId = "TeddyBear_001" };
            var list = new List<ResidentSave>
            {
                new ResidentSave { kindId = "Bunny_001", residentId = "a", movedIn = "2026-10-08", equipped = new List<AccessorySave> { new AccessorySave { slot = "Neck", accessoryId = "Acc_RedRibbon" } } },
                new ResidentSave { kindId = "Robot_001", residentId = "b", movedIn = "2026-10-08" },
                new ResidentSave { kindId = "Bunny_001", residentId = "dup" },
                new ResidentSave { kindId = "Ghost_001", residentId = "c" },
            };
            SaveMapper.WriteFriends(data, null, list);
            var back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            var residents = SaveMapper.ReadResidents(back, k => k != "Ghost_001");
            CollectionAssert.AreEqual(new[] { "Bunny_001", "Robot_001" }, residents.Select(r => r.kindId).ToArray());
            Assert.AreEqual("a", residents[0].residentId, "the first copy wins");
            Assert.AreEqual("Acc_RedRibbon", residents[0].equipped[0].accessoryId);
            Assert.IsTrue(string.IsNullOrEmpty(back.patient.toyKindId));
        }

        [Test]
        public void JosaFollowsTheLastSyllable()
        {
            Assert.AreEqual("몽이가", Josa.IGa("몽이"));
            Assert.AreEqual("부릉이를", Josa.EulReul("부릉이"));
            Assert.AreEqual("보리가", Josa.IGa("보리"));
            Assert.AreEqual("삐삐를", Josa.EulReul("삐삐"));
            Assert.AreEqual("치즈는", Josa.EunNeun("치즈"));
            Assert.AreEqual("몽글몽글 수선점은", Josa.EunNeun("몽글몽글 수선점"));
        }
    }
}
