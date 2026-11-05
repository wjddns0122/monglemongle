using System.IO;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using NUnit.Framework;

namespace Mongle.Tests
{
    public class SaveTests
    {
        static readonly string[] Order = { "Thorn", "Dirt", "Tear", "LowCotton" };
        string dir; SaveService svc;

        [SetUp] public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "mongle_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            svc = new SaveService(Path.Combine(dir, "save.json"));
        }
        [TearDown] public void TearDown() { if (Directory.Exists(dir)) Directory.Delete(dir, true); }

        static ToyRuntime FullToy()
        {
            var t = new ToyRuntime("TeddyBear_001", Order);
            foreach (var id in Order) t.Discover(id);
            foreach (var id in Order) { t.BeginRepair(id); t.CompleteDamage(id); }
            t.CompleteStamp();
            t.SetPreview(AccessorySlot.Neck, "Acc_RedRibbon");
            t.SetPreview(AccessorySlot.Head, "Acc_BlueHat");
            t.ConfirmCustomization();
            t.ConfirmPhoto(new PhotoRecord { photoId = "p1", residentId = "", pose = "Happy", background = "Garden", imageFile = "first_photo.png", takenAt = "2026-10-07", accessoryIds = { "Acc_RedRibbon", "Acc_BlueHat" } });
            t.Admit("res-1", new[] { new FurniturePlacement { furnitureId = "Fur_Bench", cellX = 3, cellY = 2, rotation = 1 }, new FurniturePlacement { furnitureId = "Fur_FlowerPot", cellX = 6, cellY = 4 } });
            return t;
        }

        [Test] public void NoFileIsNone() => Assert.AreEqual(SaveStatus.None, svc.Load().status);

        [Test]
        public void RoundTripRestoresEverything()
        {
            var t = FullToy();
            svc.Save(SaveMapper.ToSave(t));
            var r = svc.Load();
            Assert.AreEqual(SaveStatus.Ok, r.status);
            var t2 = SaveMapper.FromSave(r.data, "TeddyBear_001", Order);
            Assert.IsTrue(t2.Admitted);
            Assert.AreEqual("res-1", t2.ResidentId);
            Assert.AreEqual(t.AdmissionDate, t2.AdmissionDate);
            Assert.IsTrue(t2.Damages.All(d => d.status == DamageStatus.Completed));
            Assert.AreEqual("Acc_RedRibbon", t2.Equipped[AccessorySlot.Neck]);
            Assert.AreEqual("Acc_BlueHat", t2.Equipped[AccessorySlot.Head]);
            Assert.AreEqual(2, t2.Furniture.Count);
            Assert.AreEqual(1, t2.Furniture.First().rotation);
            Assert.AreEqual("p1", t2.Photo.photoId);
            Assert.AreEqual(t.History.Count, t2.History.Count);
            Assert.AreEqual(FlowPhase.Village, FlowResolver.Resolve(t2));
        }

        [Test]
        public void UnfinishedRepairRestartsFromItsStartState()
        {
            var t = new ToyRuntime("TeddyBear_001", Order);
            foreach (var id in Order) t.Discover(id);
            t.BeginRepair("Thorn"); t.CompleteDamage("Thorn");
            t.BeginRepair("Dirt"); t.SetProgress("Dirt", 0.7f); // not finished
            svc.Save(SaveMapper.ToSave(t));
            var t2 = SaveMapper.FromSave(svc.Load().data, "TeddyBear_001", Order);
            Assert.AreEqual(DamageStatus.Completed, t2.Get("Thorn").status);
            Assert.AreEqual(DamageStatus.Available, t2.Get("Dirt").status);
            Assert.AreEqual(0f, t2.Get("Dirt").progress);
            Assert.AreEqual(DamageStatus.Locked, t2.Get("Tear").status);
            Assert.AreEqual(FlowPhase.Repair, FlowResolver.Resolve(t2));
        }

        [Test]
        public void ReloadDoesNotDuplicateHistoryOrAdmission()
        {
            svc.Save(SaveMapper.ToSave(FullToy()));
            var a = SaveMapper.FromSave(svc.Load().data, "TeddyBear_001", Order);
            svc.Save(SaveMapper.ToSave(a));
            var b = SaveMapper.FromSave(svc.Load().data, "TeddyBear_001", Order);
            Assert.AreEqual(a.History.Count, b.History.Count);
            Assert.IsFalse(b.Admit("another", null));
            Assert.AreEqual("res-1", b.ResidentId);
        }

        [Test]
        public void CorruptFileIsReportedAndNotOverwritten()
        {
            File.WriteAllText(svc.Path, "{ this is not json");
            var r = svc.Load();
            Assert.AreEqual(SaveStatus.Corrupt, r.status);
            Assert.AreEqual("{ this is not json", File.ReadAllText(svc.Path)); // untouched by Load
        }

        [Test]
        public void EmptyObjectIsCorrupt()
        {
            File.WriteAllText(svc.Path, "{}");
            Assert.AreEqual(SaveStatus.Corrupt, svc.Load().status);
        }

        [Test]
        public void FutureVersionIsUnsupported()
        {
            File.WriteAllText(svc.Path, "{\"saveVersion\":99,\"toyKindId\":\"TeddyBear_001\"}");
            Assert.AreEqual(SaveStatus.UnsupportedVersion, svc.Load().status);
        }

        [Test]
        public void CorruptMainFallsBackToBackup()
        {
            var t = FullToy();
            svc.Save(SaveMapper.ToSave(t)); // first save
            svc.Save(SaveMapper.ToSave(t)); // second save creates .bak from first
            File.WriteAllText(svc.Path, "garbage");
            var r = svc.Load();
            Assert.AreEqual(SaveStatus.Ok, r.status);
            Assert.IsTrue(r.usedBackup);
        }

        [Test]
        public void InterruptedWriteKeepsPreviousGoodSave()
        {
            svc.Save(SaveMapper.ToSave(FullToy()));
            // Simulate a crash mid-write: a half-written temp file exists, live file untouched.
            File.WriteAllText(svc.Path + ".tmp", "{ \"saveVersion\": 1, \"toyKind");
            var r = svc.Load();
            Assert.AreEqual(SaveStatus.Ok, r.status);
            Assert.AreEqual("res-1", r.data.residentId);
        }

        [Test]
        public void QuarantineMovesCorruptFileAside()
        {
            File.WriteAllText(svc.Path, "garbage");
            string moved = svc.QuarantineCurrent();
            Assert.IsNotNull(moved);
            Assert.IsTrue(File.Exists(moved));
            Assert.IsFalse(File.Exists(svc.Path));
            Assert.AreEqual(SaveStatus.None, svc.Load().status);
        }

        [Test]
        public void QuarantineAlsoMovesTheBackupSoTheOldGameDoesNotComeBack()
        {
            File.WriteAllText(svc.Path, "{}");
            File.WriteAllText(svc.Path + ".bak", "{}");
            File.WriteAllText(svc.Path + ".tmp", "{");
            string moved = svc.QuarantineCurrent();
            Assert.IsTrue(File.Exists(moved + ".bak"));
            Assert.IsFalse(File.Exists(svc.Path + ".bak"));
            Assert.IsFalse(File.Exists(svc.Path + ".tmp"));
            Assert.AreEqual(SaveStatus.None, svc.Load().status, "new game must not restore the old one");
        }
    }
}
