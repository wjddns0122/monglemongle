using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Photo;
using Mongle.Repair;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// Hospital friends (D-005): clicking 몽글몽글 수선점 brings the next toy friend in for repairs; repaired friends
    /// live in the village as residents with their own (quest-free) life.
    /// </summary>
    public partial class VillageDirector
    {
        public readonly List<ToyAgent> ResidentAgents = new List<ToyAgent>();
        static readonly string[] FriendLines = { "안녕! 고쳐줘서 고마워.", "여기 친구들이 다 좋아요.", "몽이랑 같이 놀았어요!", "이 마을이 제일 좋아요." };
        int friendLine;

        void SpawnResidents()
        {
            int i = 0;
            foreach (var r in root.Residents)
            {
                var def = Catalog.ToyDef(r.kindId); if (def == null || def.prefab == null) continue;
                var spot = Grid.Origin + new Vector3((3.5f + 3f * (i % 3)) * Grid.Cell, 0, (2.5f + 3f * (i / 3)) * Grid.Cell);
                if (NavMesh.SamplePosition(spot, out var h, 1.5f, NavMesh.AllAreas)) spot = h.position;
                var actor = ToyActor.Spawn(Catalog, def, spot, Quaternion.Euler(0, 150 + 35 * i, 0), false);
                // a resident is fully repaired: show every damage as fixed
                var look = new ToyRuntime(def.kindId, def.damageSequence.Where(d => d != null).Select(d => d.id));
                foreach (var d in look.Damages) d.status = DamageStatus.Completed;
                actor.ApplyRepairState(look);
                var worn = new Dictionary<AccessorySlot, string>();
                foreach (var a in r.equipped) if (System.Enum.TryParse(a.slot, out AccessorySlot s)) worn[s] = a.accessoryId;
                actor.SetAccessories(worn, Catalog);
                actor.SetFace(0.5f, 0f);
                var go = actor.gameObject;
                var nav = go.AddComponent<NavMeshAgent>(); nav.baseOffset = 0f; nav.autoBraking = true;
                var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.22f, 0); col.radius = 0.13f; col.height = 0.46f;
                var agent = go.AddComponent<ToyAgent>();
                agent.IsMain = false; agent.WalkSpeed = 0.42f;
                agent.Init(actor, nav, this, new IntentSelector(System.Environment.TickCount + 97 * (i + 1)) { Personality = def.personality, Favorites = def.favoriteTags });
                if (NavMesh.SamplePosition(spot, out var hit, 2f, NavMesh.AllAreas)) nav.Warp(hit.position);
                agent.Enable();
                ResidentAgents.Add(agent);
                i++;
            }
        }

        /// <summary>Welcome the friend who just moved in (set by GameRoot.MovePatientIn).</summary>
        void WelcomeNewResident()
        {
            var kind = root.LastMovedIn; root.LastMovedIn = null;
            if (string.IsNullOrEmpty(kind)) return;
            var agent = ResidentAgents.FirstOrDefault(a => a.Actor != null && a.Actor.name == kind);
            var def = Catalog.ToyDef(kind); if (agent == null || def == null) return;
            agent.Actor.PlayThen("Happy", "Idle");
            Fx.Burst(Catalog, agent.transform.position + Vector3.up * 0.35f, new Color(1f, 0.8f, 0.9f), 30, 0.018f, 0.4f, 1.1f, 0.05f);
            ShowBubble(agent.Actor, "오늘부터 여기서 살아요! 잘 부탁해요", 3.4f);
            root.Say("새 친구 " + Josa.IGa(def.displayName) + " 마을로 이사 왔어요!", 3f);
            root.Audio.Play("success", 0.8f);
            StartCoroutine(PromptHouse(kind, 3.2f)); // D-015: the player chooses where its house stands
        }

        /// <summary>Click on 몽글몽글 수선점: continue with the waiting patient, or call the next friend in.</summary>
        public void ClickHospital(HospitalBuilding h)
        {
            if (h != null) h.Poke();
            root.Audio.Play("chime", 0.7f);
            if (root.Patient != null)
            {
                var waiting = Catalog.ToyDef(root.Patient.KindId);
                root.Say(Josa.IGa(waiting != null ? waiting.displayName : "친구") + " 수선점에서 기다리고 있어요");
                GoTreat();
                return;
            }
            var next = root.NextFriend();
            if (next == null) { root.Say("모든 친구가 마을에서 함께 살고 있어요!"); return; }
            if (!root.AdmitPatient(next)) return;
            root.Say("새 친구 " + Josa.IGa(next.displayName) + " 수선을 받으러 왔어요!", 2.6f);
            GoTreat();
        }

        void GoTreat()
        {
            if (Agent != null) Agent.Disable();
            foreach (var a in ResidentAgents) if (a != null) a.Disable();
            root.GoToScene(SceneNames.RepairShop);
        }

        void GreetResident(ToyAgent a)
        {
            if (a.TryTalk(ChatLine(a, FriendLines[friendLine++ % FriendLines.Length]))) root.Audio.Play("hello", 0.7f, 1.1f);
            OpenDress(a);
        }

        // ---------------- dressing Mongi or a resident with accessories bought at the accessory shop ----------------
        /// <summary>The friend card (11-26 FriendCardView, Figma 88:1912): 프로필 on the left, 꾸며주기 on the right.</summary>
        Mongle.Views.FriendCardView card;
        public ToyAgent Dressing { get; private set; }
        public bool DressOpen => card != null && card.Visible;

        ResidentSave ResidentOf(ToyAgent a) => a == null || a.Actor == null ? null : root.Residents.FirstOrDefault(r => r.kindId == a.Actor.name);

        static Dictionary<AccessorySlot, string> Worn(ResidentSave r)
        {
            var worn = new Dictionary<AccessorySlot, string>();
            foreach (var e in r.equipped ?? new List<AccessorySave>()) if (System.Enum.TryParse(e.slot, out AccessorySlot s) && !string.IsNullOrEmpty(e.accessoryId)) worn[s] = e.accessoryId;
            return worn;
        }

        /// <summary>What this toy wears now (Mongi: its own saved outfit; a friend: its resident record). Null when it cannot be dressed.</summary>
        Dictionary<AccessorySlot, string> WornBy(ToyAgent a)
        {
            if (a == null) return null;
            if (a.IsMain) return state.CustomizationConfirmed ? new Dictionary<AccessorySlot, string>(state.Equipped) : null;
            var r = ResidentOf(a); return r != null ? Worn(r) : null;
        }

        void SetWorn(ToyAgent a, Dictionary<AccessorySlot, string> worn)
        {
            if (a.IsMain)
            {
                state.Equipped.Clear(); state.Preview.Clear();
                foreach (var kv in worn) { state.Equipped[kv.Key] = kv.Value; state.Preview[kv.Key] = kv.Value; }
            }
            else ResidentOf(a).equipped = worn.Select(kv => new AccessorySave { slot = kv.Key.ToString(), accessoryId = kv.Value }).ToList();
            a.Actor.SetAccessories(worn, Catalog);
            root.Persist("dress:" + a.Actor.name);
        }

        string DisplayNameOf(ToyAgent a) => a.IsMain ? (Catalog.toy != null ? Catalog.toy.displayName : "몽이") : NameOf(a);

        public void OpenDress(ToyAgent a)
        {
            if (WornBy(a) == null) return;
            if (card == null)
            {
                card = Mongle.Views.FriendCardView.Get(); if (card == null) return;
                card.OnToggle = id => ToggleWear(Catalog.AllAccessories.FirstOrDefault(acc => acc.id == id));
            }
            Dressing = a;
            card.Show(DisplayNameOf(a), PhotoOf(a), MovedInOf(a), KindOf(a), DressItems());
            if (Hud != null) Hud.Focus(true);   // Figma 88:1912: the 퀘스트 pill and the tiles step aside for the card
        }

        public void CloseDress()
        {
            bool was = DressOpen;
            if (card != null) card.Hide();
            Dressing = null;
            if (was && Hud != null) { Hud.Focus(false); RefreshQuestTracker(); }
        }

        void RefreshDress()
        {
            if (WornBy(Dressing) == null) { CloseDress(); return; }
            card.SetItems(DressItems());
        }

        /// <summary>The accessories this friend can wear: those in the box and the ones it has on (with their pictures, Resources/UI/Accessories).</summary>
        List<Mongle.Views.FriendCardView.Item> DressItems()
        {
            var worn = WornBy(Dressing);
            return Catalog.AllAccessories.Where(acc => World.Owned(acc.id) > 0 || worn.ContainsValue(acc.id)).Select(acc => new Mongle.Views.FriendCardView.Item
            {
                id = acc.id, name = acc.displayName, icon = Resources.Load<Sprite>("UI/Accessories/" + acc.id), color = acc.uiColor,
                count = World.Owned(acc.id), worn = worn.TryGetValue(acc.slot, out var cur) && cur == acc.id,
            }).ToList();
        }

        /// <summary>The friend's first photo (the album's), loaded for the card; null when it has none yet.</summary>
        Texture2D PhotoOf(ToyAgent a)
        {
            var rec = a.IsMain ? state.Photo : SaveMapper.FromPhotoSave(ResidentOf(a)?.photo);
            return rec != null ? PhotoStore.Load(root.Saves, rec) : null;
        }

        /// <summary>The day it moved into the village, as the card writes it (2026.11.12).</summary>
        string MovedInOf(ToyAgent a)
        {
            string d = a.IsMain ? state.AdmissionDate : ResidentOf(a)?.movedIn;
            return string.IsNullOrEmpty(d) ? "" : d.Split(' ')[0].Replace('-', '.');
        }

        /// <summary>What kind of toy it is (the card's paw line).</summary>
        static readonly Dictionary<string, string> Kinds = new Dictionary<string, string>
        {
            { "TeddyBear_001", "곰인형" }, { "Bunny_001", "토끼인형" }, { "Cat_001", "고양이인형" }, { "Robot_001", "로봇" }, { "Car_001", "자동차" },
            { "Dino_001", "공룡인형" }, { "Bee_001", "꿀벌인형" }, { "Plane_001", "비행기" }, { "FireTruck_001", "소방차" }, { "Pawn_001", "나무인형" },
            { "Frog_001", "개구리인형" },
        };
        public static string KindLabel(string kindId) => kindId != null && Kinds.TryGetValue(kindId, out var k) ? k : "인형";
        string KindOf(ToyAgent a) => KindLabel(a.IsMain ? "TeddyBear_001" : a.Actor.name);

        /// <summary>Put an owned accessory on the resident (one is used up) or take it off again (it goes back to the box).</summary>
        public bool ToggleWear(AccessoryDefinition acc)
        {
            var worn = WornBy(Dressing); if (worn == null || acc == null) return false;
            bool takeOff = worn.TryGetValue(acc.slot, out var cur) && cur == acc.id;
            if (takeOff) { worn.Remove(acc.slot); World.AddToInventory(acc.id); root.Audio.Play("click", 0.6f, 0.8f); }
            else
            {
                if (!World.TakeFromInventory(acc.id)) { root.Audio.Play("wrong", 0.4f); return false; }
                if (!string.IsNullOrEmpty(cur)) World.AddToInventory(cur); // swapping: the old one goes back to the box
                worn[acc.slot] = acc.id;
                World.Report("AccessoryWorn", GiverOf(Dressing)); // kind id (Mongi too): a friend's "꾸며주기" request listens for it
                root.Audio.Play("chime", 0.6f, 1.2f);
                Fx.Burst(Catalog, Dressing.Actor.Point("Head", Dressing.transform.position + Vector3.up * 0.4f) + Vector3.up * 0.1f, new Color(1f, 0.8f, 0.9f), 16, 0.016f, 0.25f, 1f, -0.03f);
                ShowBubble(Dressing.Actor, "와, " + acc.displayName + " 마음에 쏙 들어요!", 2.4f);
            }
            SetWorn(Dressing, worn);
            RefreshDress();
            return true;
        }
    }
}
