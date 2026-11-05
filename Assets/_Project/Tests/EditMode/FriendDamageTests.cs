using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.EditorTools;
using Mongle.Toy;
using NUnit.Framework;
using UnityEngine;

namespace Mongle.Tests
{
    /// <summary>D-009: every friend hurts in its own places; node names follow the Blender contract; old patients migrate.</summary>
    public class FriendDamageTests
    {
        static readonly string[] MongiIds = { "Thorn", "Dirt", "Tear", "LowCotton" };

        [Test]
        public void EveryFriendHasTwoToFourDamagesWithUniqueIds()
        {
            foreach (var kind in ContentBuilder.FriendKinds)
            {
                int n = ContentBuilder.FriendDamages.Count(s => s.kind == kind);
                Assert.That(n, Is.InRange(2, 4), kind);
            }
            var ids = ContentBuilder.FriendDamages.Select(s => s.id).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count(), "damage ids are global (the catalog finds them by id)");
            Assert.IsFalse(ids.Intersect(MongiIds).Any(), "Mongi's tutorial damages stay his own");
            Assert.IsTrue(ContentBuilder.FriendDamages.All(s => ContentBuilder.FriendKinds.Contains(s.kind)));
        }

        [Test]
        public void EveryFriendHurtsDifferently()
        {
            var sets = ContentBuilder.FriendKinds.Select(k => ContentBuilder.FriendDamages.Where(s => s.kind == k).Select(s => s.type + "@" + s.site).OrderBy(x => x).ToList()).ToList();
            foreach (var set in sets) Assert.AreEqual(set.Count, set.Distinct().Count(), "one friend never has the same hurt twice: " + string.Join(",", set));
            for (int i = 0; i < sets.Count; i++)
                for (int j = i + 1; j < sets.Count; j++)
                    Assert.IsFalse(sets[i].SequenceEqual(sets[j]), "two friends with the same hurts: " + string.Join(",", sets[i]));
        }

        static DamageDefinition Def(string id, out int order)
        {
            var spec = ContentBuilder.FriendDamages.First(s => s.id == id);
            order = ContentBuilder.FriendDamages.Where(s => s.kind == spec.kind).ToList().IndexOf(spec);
            var d = ScriptableObject.CreateInstance<DamageDefinition>();
            ContentBuilder.Fill(d, spec, order);
            return d;
        }

        [Test]
        public void DefinitionsUseTheContractNodeNames()
        {
            var eye = Def("Bunny_EyeR", out _);
            Assert.AreEqual(DamageType.EyeButton, eye.type); Assert.AreEqual("Sew", eye.miniGameKey); Assert.AreEqual("Tool_Needle", eye.requiredToolId);
            Assert.AreEqual("DP_EyeR", eye.damagePointName); Assert.AreEqual("DN_EyeR", eye.normalPointName); Assert.AreEqual("CA_EyeR", eye.cameraAnchorName); Assert.AreEqual("CA_EyeR", eye.repairAnchorName);
            Assert.AreEqual("EyeGone_EyeR", eye.shapeKey); Assert.AreEqual("DMG_EyeLoose_EyeR", eye.visualName); Assert.AreEqual("SP_EyeR_", eye.stitchPrefix); Assert.AreEqual("Head", eye.boneName);

            var arm = Def("Cat_ArmL", out _);
            Assert.AreEqual("DMG_Tear_ArmL", arm.visualName); Assert.AreEqual("Arm_L", arm.limpBone); Assert.AreEqual("Spine", arm.boneName);

            var tail = Def("Dino_Tail", out _);
            Assert.AreEqual("Cotton", tail.miniGameKey); Assert.AreEqual("Deflated_Tail", tail.shapeKey); Assert.AreEqual("DMG_FillOpen_Tail", tail.visualName); Assert.AreEqual("FP_Tail", tail.workPointName);

            var wheel = Def("Car_WheelR", out _);
            Assert.AreEqual("DMG_Thorn_WheelR", wheel.visualName); Assert.AreEqual("GP_THORN_WheelR", wheel.grabPointName); Assert.AreEqual("Hips", wheel.boneName);

            var fuel = Def("Car_Fuel", out _);
            Assert.AreEqual("Fuel", fuel.miniGameKey); Assert.AreEqual("Tool_FuelNozzle", fuel.requiredToolId);
            Assert.AreEqual("DMG_FuelCap_Fuel", fuel.visualName); Assert.AreEqual("DMG_FuelOpen_Fuel", fuel.openName); Assert.AreEqual("FP_Fuel", fuel.workPointName);

            var bat = Def("Robot_Battery", out _);
            Assert.AreEqual("Battery", bat.miniGameKey); Assert.AreEqual("Tool_Battery", bat.requiredToolId);
            Assert.AreEqual("DMG_BatteryOld_Battery", bat.visualName); Assert.AreEqual("DMG_BatteryNew_Battery", bat.visualDoneName); Assert.AreEqual("FP_Battery", bat.workPointName);

            var paint = Def("Pawn_Paint", out _);
            Assert.AreEqual("Paint", paint.miniGameKey); Assert.AreEqual("Tool_Brush", paint.requiredToolId);
            Assert.AreEqual(new Color(0.62f, 0.76f, 0.98f), paint.cleanColor); Assert.AreEqual(new Color(0.86f, 0.72f, 0.55f), paint.stainColor);
            foreach (var d in new[] { eye, arm, tail, wheel, fuel, bat, paint }) { Assert.IsTrue(d.HasSite); Object.DestroyImmediate(d); }
        }

        [Test]
        public void LegacyNodesWhereTheContractKeepsThem()
        {
            var bee = Def("Bee_Belly", out int beeOrder);
            Assert.AreEqual("DP_BELLY", bee.damagePointName); Assert.AreEqual("CA_BELLY", bee.cameraAnchorName);
            Assert.AreEqual(1, bee.hintPriority, "repair order = hint order");
            foreach (var id in new[] { "FireTruck_Siren", "Pawn_Bun" })
            {
                var t = Def(id, out _);
                Assert.AreEqual("DMG_Thorn_EarL", t.visualName); Assert.AreEqual("GP_THORN", t.grabPointName);
                Assert.AreEqual("DP_EAR_L", t.damagePointName); Assert.AreEqual("CA_EAR_L", t.cameraAnchorName); Assert.AreEqual("Ear_L", t.boneName);
                Object.DestroyImmediate(t);
            }
            Object.DestroyImmediate(bee);
        }

        [Test]
        public void EveryDamageHasWordsAndAReward()
        {
            foreach (var s in ContentBuilder.FriendDamages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.name), s.id); Assert.IsFalse(string.IsNullOrEmpty(s.note), s.id);
                var d = ScriptableObject.CreateInstance<DamageDefinition>(); ContentBuilder.Fill(d, s, 0);
                Assert.That(d.rewardCoins, Is.InRange(25, 40), s.id);
                Object.DestroyImmediate(d);
            }
        }

        // ---------- save migration (v6) ----------
        static readonly string[] OldPlush = { "Thorn", "Dirt", "Tear", "LowCotton" };
        static readonly string[] NewBunny = { "Bunny_EyeR", "Bunny_EarL", "Bunny_FootR" };

        [Test]
        public void PatientWithOutdatedDamagesGoesBackToDiagnosis()
        {
            var t = new ToyRuntime("Bunny_001", OldPlush); t.EnsureResidentId("bunny-1");
            foreach (var id in OldPlush) t.Discover(id);
            t.BeginRepair("Thorn"); t.CompleteDamage("Thorn");
            var json = JsonUtility.ToJson(new SaveData { toyKindId = "TeddyBear_001", patient = SaveMapper.ToPatientSave(t) });
            var back = SaveMapper.FromPatientSave(JsonUtility.FromJson<SaveData>(json).patient, NewBunny);
            CollectionAssert.AreEqual(NewBunny, back.Damages.Select(d => d.id).ToArray());
            Assert.IsTrue(back.Damages.All(d => d.status == DamageStatus.Hidden));
            Assert.IsFalse(back.DiagnosisComplete);
            Assert.AreEqual("bunny-1", back.ResidentId, "still the same friend");
            Assert.AreEqual(FlowPhase.CustomerArrival, FlowResolver.ResolvePatient(back));
        }

        [Test]
        public void StampedPatientWithOutdatedDamagesStaysRepaired()
        {
            var t = new ToyRuntime("Bunny_001", OldPlush);
            foreach (var id in OldPlush) t.Discover(id);
            foreach (var id in OldPlush) { t.BeginRepair(id); t.CompleteDamage(id); }
            t.CompleteStamp();
            var back = SaveMapper.FromPatientSave(SaveMapper.ToPatientSave(t), NewBunny);
            Assert.IsTrue(back.AllRepaired);
            Assert.AreEqual(FlowPhase.Customization, FlowResolver.ResolvePatient(back));
        }

        [Test]
        public void PatientWithCurrentDamagesKeepsProgress()
        {
            var t = new ToyRuntime("Bunny_001", NewBunny);
            foreach (var id in NewBunny) t.Discover(id);
            t.BeginRepair("Bunny_EyeR"); t.CompleteDamage("Bunny_EyeR");
            var back = SaveMapper.FromPatientSave(SaveMapper.ToPatientSave(t), NewBunny);
            Assert.AreEqual(DamageStatus.Completed, back.Get("Bunny_EyeR").status);
            Assert.AreEqual(DamageStatus.Available, back.Get("Bunny_EarL").status);
            Assert.AreEqual(FlowPhase.Repair, FlowResolver.ResolvePatient(back));
        }

        [Test]
        public void SaveVersionIsSixOrLater() { Assert.GreaterOrEqual(SaveData.CurrentVersion, 6); } // 7 since D-011 (friends' requests); the v6 damage migration stays
    }
}
