using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
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
        GameObject dressPanel; Text dressTitle; RectTransform dressList;
        public ToyAgent Dressing { get; private set; }
        public bool DressOpen => dressPanel != null && dressPanel.activeSelf;

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
            if (dressPanel == null) BuildDressPanel();
            Dressing = a; dressPanel.SetActive(true);
            RefreshDress();
        }

        void BuildDressPanel()
        {
            var p = UiKit.Panel(canvas.transform, "DressPanel", UiKit.Paper, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(1500, 240), true);
            dressPanel = p.gameObject;
            // one row: the words and the accessories share one column left of the close button (so they line up with each other),
            // the button sits in the middle of the right end
            float right = -(DressPad + DressCloseW + DressGap);
            dressTitle = UiKit.Label(p.transform, "", 36, UiKit.Brown, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(100, 50));
            var trt = dressTitle.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
            trt.offsetMin = new Vector2(DressPad, -78f); trt.offsetMax = new Vector2(right, -28f);
            dressTitle.name = "Title";
            dressList = UiKit.Rect(p.transform, "List", new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(DressPad, 20), new Vector2(right, -84));
            UiKit.MakeButton(p.transform, "닫기", UiKit.Peach, new Vector2(1f, 0.5f), new Vector2(-(DressPad + DressCloseW / 2f), 0f), new Vector2(DressCloseW, 100), CloseDress, 36);
        }

        const float DressPad = 40f, DressCloseW = 240f, DressGap = 40f;

        public void CloseDress() { if (dressPanel != null) dressPanel.SetActive(false); Dressing = null; }

        void RefreshDress()
        {
            var worn = WornBy(Dressing); if (worn == null) { CloseDress(); return; }
            dressTitle.text = DisplayNameOf(Dressing) + " 꾸며주기 · 누르면 입히고, 다시 누르면 벗어요";
            var items = Catalog.AllAccessories.Where(acc => World.Owned(acc.id) > 0 || worn.ContainsValue(acc.id)).ToList();
            LayDress(items, acc => worn.TryGetValue(acc.slot, out var cur) && cur == acc.id, acc => World.Owned(acc.id));
        }

        /// <summary>The dress list: one button per accessory (filled while the friend wears it), then the prefab laid over it (D-035).</summary>
        void LayDress(List<AccessoryDefinition> items, System.Func<AccessoryDefinition, bool> wearing, System.Func<AccessoryDefinition, int> owned)
        {
            for (int i = dressList.childCount - 1; i >= 0; i--)
            {
                var c = dressList.GetChild(i).gameObject; c.transform.SetParent(null);
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            if (items.Count == 0)
            {
                SizeDress(1);
                UiKit.Label(dressList, "가진 악세서리가 없어요. 컵케이크 악세서리점에서 사 와요!", 32, UiKit.Brown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 120), TextAnchor.MiddleCenter, FontStyle.Normal);
                SkinPart(dressPanel.transform);
                return;
            }
            // one row of wide buttons for up to 4, two rows of up to 5 for more (9 accessories since D-010)
            int n = items.Count, rows = n <= 4 ? 1 : 2, cols = Mathf.CeilToInt(n / (float)rows);
            SizeDress(rows);
            float listW = dressList.rect.width > 1f ? dressList.rect.width : 1140f, listH = rows == 1 ? 156f : 266f;
            float step = n <= 3 ? 360f : listW / cols, rowH = listH / rows;
            float bw = Mathf.Min(340f, step - 16f), bh = Mathf.Min(110f, rowH - 14f);
            for (int i = 0; i < n; i++)
            {
                var acc = items[i]; bool on = wearing(acc);
                int row = i / cols, col = i % cols, inRow = Mathf.Min(cols, n - row * cols);
                // each row is centred under the words (a short last row too)
                var b = UiKit.MakeButton(dressList, "", on ? acc.uiColor : Color.Lerp(acc.uiColor, Color.white, 0.45f),
                    new Vector2(0.5f, 1f), new Vector2((col - (inRow - 1) / 2f) * step, -rowH * (row + 0.5f)), new Vector2(bw, bh), () => ToggleWear(acc));
                UiKit.SetButtonText(b, "");
                UiKit.Label(b.transform, (on ? "♥ " : "") + acc.displayName, rows == 1 ? 30 : 28, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(bw - 12f, 44));
                UiKit.Label(b.transform, on ? (rows == 1 ? "입는 중 · 누르면 벗어요" : "입는 중") : "가진 수 " + owned(acc), 22, UiKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(bw - 12f, 32), TextAnchor.MiddleCenter, FontStyle.Normal);
                b.name = "Dress_" + acc.id;
            }
            SkinPart(dressPanel.transform);
        }

        /// <summary>The dress panel grows a second row when there are more than four accessories to show.</summary>
        void SizeDress(int rows)
        {
            float h = rows == 1 ? 240f : 350f;
            var rt = (RectTransform)dressPanel.transform;
            rt.sizeDelta = new Vector2(1500f, h); rt.anchoredPosition = new Vector2(0f, 30f + h / 2f);
        }

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
