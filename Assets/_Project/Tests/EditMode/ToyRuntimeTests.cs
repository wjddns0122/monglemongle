using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using NUnit.Framework;

namespace Mongle.Tests
{
    public class ToyRuntimeTests
    {
        static readonly string[] Order = { "Thorn", "Dirt", "Tear", "LowCotton" };
        ToyRuntime NewToy() => new ToyRuntime("TeddyBear_001", Order);

        static void DiscoverAll(ToyRuntime t) { foreach (var id in Order) t.Discover(id); }

        static void RepairAll(ToyRuntime t)
        {
            foreach (var id in Order) { Assert.IsTrue(t.BeginRepair(id), id); Assert.IsTrue(t.CompleteDamage(id), id); }
        }

        [Test]
        public void AllDamagesStartHidden()
        {
            var t = NewToy();
            Assert.IsTrue(t.Damages.All(d => d.status == DamageStatus.Hidden));
            Assert.AreEqual(FlowPhase.CustomerArrival, FlowResolver.Resolve(t));
        }

        [Test]
        public void DiscoverOnlyAffectsThatDamageAndFiresOnce()
        {
            var t = NewToy(); int count = 0; t.DamageDiscovered += _ => count++;
            Assert.IsTrue(t.Discover("Dirt"));
            Assert.IsFalse(t.Discover("Dirt"));
            Assert.AreEqual(1, count);
            Assert.AreEqual(DamageStatus.Discovered, t.Get("Dirt").status);
            Assert.AreEqual(DamageStatus.Hidden, t.Get("Thorn").status);
            Assert.IsFalse(t.DiagnosisComplete);
        }

        [Test]
        public void DiagnosisCompletesOnceAndUnlocksFirstInOrder()
        {
            var t = NewToy(); int done = 0; t.DiagnosisCompleted += () => done++;
            DiscoverAll(t);
            t.Discover("Thorn");
            Assert.AreEqual(1, done);
            Assert.AreEqual(DamageStatus.Available, t.Get("Thorn").status);
            Assert.AreEqual(DamageStatus.Locked, t.Get("Dirt").status);
            Assert.AreEqual(FlowPhase.Repair, FlowResolver.Resolve(t));
        }

        [Test]
        public void CannotRepairLockedOrUndiscovered()
        {
            var t = NewToy();
            Assert.IsFalse(t.BeginRepair("Thorn"));
            DiscoverAll(t);
            Assert.IsFalse(t.BeginRepair("Dirt")); // locked behind Thorn
            Assert.IsTrue(t.BeginRepair("Thorn"));
        }

        [Test]
        public void CompleteFiresExactlyOnceAndUnlocksNext()
        {
            var t = NewToy(); var done = new List<string>(); t.DamageCompleted += done.Add;
            DiscoverAll(t);
            t.BeginRepair("Thorn");
            Assert.IsTrue(t.CompleteDamage("Thorn"));
            Assert.IsFalse(t.CompleteDamage("Thorn"));
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(DamageStatus.Available, t.Get("Dirt").status);
            Assert.AreEqual(1, t.History.Count(h => h.key == "Damage:Thorn"));
        }

        [Test]
        public void AllRepairsCompletedFiresOnceAndPhaseIsStamp()
        {
            var t = NewToy(); int all = 0; t.AllRepairsCompleted += () => all++;
            DiscoverAll(t); RepairAll(t);
            Assert.AreEqual(1, all);
            Assert.AreEqual(FlowPhase.CompletionStamp, FlowResolver.Resolve(t));
        }

        [Test]
        public void ProgressIsClampedAndOnlyForActive()
        {
            var t = NewToy(); DiscoverAll(t);
            Assert.AreEqual(0f, t.SetProgress("Thorn", 0.5f)); // not active
            t.BeginRepair("Thorn");
            Assert.AreEqual(1f, t.SetProgress("Thorn", 5f));
            Assert.AreEqual(0f, t.SetProgress("Thorn", -3f));
        }

        [Test]
        public void CancelReturnsToStartState()
        {
            var t = NewToy(); DiscoverAll(t);
            t.BeginRepair("Thorn"); t.SetProgress("Thorn", 0.6f);
            t.CancelRepair("Thorn");
            Assert.AreEqual(DamageStatus.Available, t.Get("Thorn").status);
            Assert.AreEqual(0f, t.Get("Thorn").progress);
        }

        [Test]
        public void StampRequiresAllRepairsAndIsOnce()
        {
            var t = NewToy(); int s = 0; t.StampCompleted += () => s++;
            DiscoverAll(t);
            Assert.IsFalse(t.CompleteStamp());
            RepairAll(t);
            Assert.IsTrue(t.CompleteStamp());
            Assert.IsFalse(t.CompleteStamp());
            Assert.AreEqual(1, s);
            Assert.AreEqual(FlowPhase.Customization, FlowResolver.Resolve(t));
        }

        [Test]
        public void CustomizationPreviewConfirmAndSlots()
        {
            var t = NewToy(); DiscoverAll(t); RepairAll(t);
            Assert.IsFalse(t.SetPreview(AccessorySlot.Neck, "Acc_RedRibbon")); // before stamp
            t.CompleteStamp();
            Assert.IsTrue(t.SetPreview(AccessorySlot.Neck, "Acc_RedRibbon"));
            Assert.IsTrue(t.SetPreview(AccessorySlot.Head, "Acc_BlueHat"));
            Assert.IsTrue(t.SetPreview(AccessorySlot.Face, "Acc_RoundGlasses"));
            Assert.IsTrue(t.SetPreview(AccessorySlot.Head, null)); // remove
            Assert.IsFalse(t.SetPreview(AccessorySlot.Head, null)); // nothing to remove
            Assert.AreEqual(2, t.Visible.Count);
            Assert.IsTrue(t.ConfirmCustomization());
            Assert.IsFalse(t.ConfirmCustomization());
            Assert.AreEqual(2, t.Equipped.Count);
            Assert.AreEqual(FlowPhase.PhotoStudio, FlowResolver.Resolve(t));
        }

        [Test]
        public void PhotoAndAdmissionAreOnceAndOrdered()
        {
            var t = NewToy(); DiscoverAll(t); RepairAll(t); t.CompleteStamp();
            var photo = new PhotoRecord { photoId = "p1", pose = "Happy", imageFile = "first_photo.png" };
            Assert.IsFalse(t.ConfirmPhoto(photo)); // customization not confirmed
            t.ConfirmCustomization();
            Assert.IsFalse(t.Admit("r1", null));   // no photo yet
            Assert.IsTrue(t.ConfirmPhoto(photo));
            Assert.IsFalse(t.ConfirmPhoto(new PhotoRecord { photoId = "p2" }));
            Assert.AreEqual("p1", t.Photo.photoId);
            Assert.AreEqual(FlowPhase.VillageAdmission, FlowResolver.Resolve(t));
            int n = 0; t.ToyAdmitted += () => n++;
            Assert.IsTrue(t.Admit("r1", new[] { new FurniturePlacement { furnitureId = "Fur_Bench" } }));
            Assert.IsFalse(t.Admit("r2", null));
            Assert.AreEqual(1, n);
            Assert.AreEqual("r1", t.ResidentId);
            Assert.AreEqual(1, t.Furniture.Count);
            Assert.AreEqual(1, t.History.Count(h => h.key == "Admitted"));
            Assert.AreEqual(FlowPhase.Village, FlowResolver.Resolve(t));
        }

        [Test]
        public void HistoryKeysNeverDuplicate()
        {
            var t = NewToy();
            Assert.IsTrue(t.AddHistory("k", "a"));
            Assert.IsFalse(t.AddHistory("k", "b"));
            Assert.AreEqual(1, t.History.Count);
        }
    }
}
