using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// Friends' houses (D-012, D-015): every resident's gingerbread house stands where the PLAYER put it. When a friend moves in
    /// the house follows the pointer as a see-through ghost (green = it fits, red = it does not), R turns it, a click sets it
    /// down; "나중에" leaves it for the next visit. Friends walk home now and then; clicking a house calls its friend home.
    /// </summary>
    public partial class VillageDirector
    {
        readonly Dictionary<string, HouseView> houses = new Dictionary<string, HouseView>();
        Transform housesRoot;
        public bool HousePlacing { get; private set; }
        ResidentSave houseTarget; HouseView ghost; float ghostYaw; Vector3 ghostPos; bool ghostValid;

        readonly HashSet<string> housePrompted = new HashSet<string>();
        const float HouseHalf = 1.0f, HouseSnap = 0.2f;

        public HouseView HouseOf(string kindId) => kindId != null && houses.TryGetValue(kindId, out var h) && h != null ? h : null;
        public HouseView HouseOf(ToyAgent a) => a == null || a.IsMain || a.Actor == null ? null : HouseOf(a.Actor.name);

        void SpawnHouses()
        {
            if (housesRoot == null) housesRoot = new GameObject("Houses").transform;
            BlockTrackInGrid();
            foreach (var r in root.Residents) SpawnHouse(r);
            StartCoroutine(PromptUnplacedHouses());
        }

        /// <summary>The cake train's track is no place for a house: block it in the grid (houses and furniture).</summary>
        void BlockTrackInGrid()
        {
            var train = FindAnyObjectByType<TrainRide>(FindObjectsInactive.Include);
            if (train == null || train.loopLocal == null) return;
            for (int i = 0; i < train.loopLocal.Length; i += 3) Grid.BlockDisc(train.transform.TransformPoint(train.loopLocal[i]), 0.85f);
        }

        /// <summary>Where this resident's house stands (null = not placed yet).</summary>
        static bool SpotOf(ResidentSave r, out Vector3 pos, out float yaw)
        {
            pos = default; yaw = 0f;
            if (r == null) return false;
            if (r.housePlaced) { pos = new Vector3(r.houseX, 0f, r.houseZ); yaw = r.houseYaw; return true; }
            if (r.house >= 0 && r.house < HouseLots.Lots.Length) { var l = HouseLots.Lots[r.house]; pos = l.pos; yaw = l.yaw + HouseLots.Turn(r); return true; }
            return false;
        }

        HouseView SpawnHouse(ResidentSave r)
        {
            if (!HouseLots.HasLook(r) || Catalog.houses == null || Catalog.houses.Length == 0) return null;
            if (!SpotOf(r, out var pos, out var yaw)) return null;
            if (houses.TryGetValue(r.kindId, out var old) && old != null) return old;
            var prefab = Catalog.houses[r.houseVariant % Catalog.houses.Length]; if (prefab == null) return null;
            var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yaw, 0f), housesRoot);
            go.name = "House_" + r.kindId;
            if (!go.TryGetComponent<HouseView>(out var hv)) hv = go.AddComponent<HouseView>();   // (?? misses Unity's fake null in the editor)
            hv.kindId = r.kindId; hv.SetTint(r.houseTint);
            houses[r.kindId] = hv;
            Grid.BlockBox(pos, new Vector2(HouseHalf, HouseHalf), yaw);
            return hv;
        }

        // ---------------- choosing the place (the furniture placing screen, PlaceBarView) ----------------

        /// <summary>A moved-in friend (or one still without a house from before) asks for its place, once things are quiet.</summary>
        IEnumerator PromptHouse(string kind, float delay)
        {
            yield return new WaitForSeconds(delay);
            var r = root.Residents.FirstOrDefault(x => x.kindId == kind);
            while (r != null && !SpotOf(r, out _, out _))
            {
                if (!HousePlacing && !AnyPanelOpen && !QuestUiOpen && !PlacingMode && ViewingRide == null && !ClawOpen && !housePrompted.Contains(kind)) { BeginHousePlacement(r); yield break; }
                if (housePrompted.Contains(kind)) yield break;
                yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator PromptUnplacedHouses()
        {
            yield return new WaitForSeconds(5f);
            foreach (var r in root.Residents.ToList())
            {
                if (SpotOf(r, out _, out _) || housePrompted.Contains(r.kindId)) continue;
                yield return PromptHouse(r.kindId, 0f);
                while (HousePlacing) yield return new WaitForSeconds(0.5f);
            }
        }

        public bool BeginHousePlacement(ResidentSave r)
        {
            if (r == null || HousePlacing || Catalog.houses == null || Catalog.houses.Length == 0 || !HouseLots.HasLook(r)) return false;
            housePrompted.Add(r.kindId);
            if (PlacingMode) ExitPlacing();
            CloseDress(); CloseQuestUi();
            var prefab = Catalog.houses[r.houseVariant % Catalog.houses.Length]; if (prefab == null) return false;
            var go = Instantiate(prefab, housesRoot);
            go.name = "HouseGhost";
            foreach (var o in go.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) Destroy(o);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            if (!go.TryGetComponent<HouseView>(out ghost)) ghost = go.AddComponent<HouseView>();
            houseTarget = r; ghostYaw = 0f;
            var t = Orbit.target; ghostPos = SnapHouse(new Vector3(t.x, 0f, t.z));
            HousePlacing = true; SetHud(false); Hud.Hint(null);
            var def = Catalog.ToyDef(r.kindId); string name = def != null ? def.displayName : "친구";
            Hud.Hint(Josa.IGa(name) + " 이사 왔어요! 집을 놓을 곳을 골라 주세요");   // the village's hint pill, as while moving furniture
            WirePlaceBarForHouse();
            root.Audio.Play("hello", 0.7f, 1.2f);
            UpdateGhost(false);
            return true;
        }

        static Vector3 SnapHouse(Vector3 p) => new Vector3(Mathf.Round(p.x / HouseSnap) * HouseSnap, 0f, Mathf.Round(p.z / HouseSnap) * HouseSnap);

        /// <summary>Does a house with its middle here, turned this way, fit in the park (free ground, not on the track)?</summary>
        bool HouseFits(Vector3 c, float yaw)
        {
            const float margin = 1.2f;
            if (c.x < ParkRect.x + margin || c.x > ParkRect.z - margin || c.z < ParkRect.y + margin || c.z > ParkRect.w - margin) return false;
            var rot = Quaternion.Euler(0f, -yaw, 0f);
            var furnitureCells = new HashSet<Vector2Int>();
            foreach (var o in OthersOf(null)) foreach (var cell in Grid.Cells(o.Item1, o.Item2.cellX, o.Item2.cellY, o.Item2.rotation)) furnitureCells.Add(cell);
            int reach = Mathf.CeilToInt(HouseHalf * 1.5f / Grid.Cell) + 1;
            var mid = Grid.CellAt(c);
            for (int x = mid.x - reach; x <= mid.x + reach; x++)
                for (int y = mid.y - reach; y <= mid.y + reach; y++)
                {
                    var l = rot * (Grid.CellCenter(x, y) - c);
                    if (Mathf.Abs(l.x) < HouseHalf + Grid.Cell * 0.5f && Mathf.Abs(l.z) < HouseHalf + Grid.Cell * 0.5f)
                    {
                        var cell = new Vector2Int(x, y);
                        if (Grid.Blocked.Contains(cell) || furnitureCells.Contains(cell)) return false;
                    }
                }
            return true;
        }

        void UpdateGhost(bool fromPointer)
        {
            if (ghost == null) return;
            if (fromPointer && Pointer.current != null && cam != null)
            {
                var ray = cam.ScreenPointToRay(Pointer.current.position.ReadValue());
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) ghostPos = SnapHouse(ray.GetPoint(d));
            }
            ghostValid = HouseFits(ghostPos, ghostYaw);
            ghost.transform.SetPositionAndRotation(ghostPos, Quaternion.Euler(0f, ghostYaw, 0f));
            ghost.SetGhost(ghostValid ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.5f, 0.5f));
            if (PlaceUi != null) PlaceUi.SetPutReady(ghostValid);
        }

        void UpdateHousePlacing()
        {
            if (!HousePlacing) return;
            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame) HouseRotate();
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            UpdateGhost(!overUi && Pointer.current != null && !(Pointer.current is Touchscreen && !Pointer.current.press.isPressed));
            if (!overUi && Pointer.current != null && Pointer.current.press.wasReleasedThisFrame) HouseConfirm();
        }

        void HouseRotate() { if (!HousePlacing) return; ghostYaw = Mathf.Repeat(ghostYaw + 45f, 360f); UpdateGhost(false); root.Audio.Play("click", 0.4f, 1.3f); }

        void HouseConfirm()
        {
            if (!HousePlacing || houseTarget == null) return;
            if (!ghostValid) { root.Say("여기에는 놓을 수 없어요", 1.8f); root.Audio.Play("wrong", 0.5f); return; }
            var r = houseTarget;
            r.housePlaced = true; r.houseX = ghostPos.x; r.houseZ = ghostPos.z; r.houseYaw = ghostYaw; r.house = -1;
            Destroy(ghost.gameObject); ghost = null; houseTarget = null; HousePlacing = false;
            EndHousePlaceBar(); SetHud(true);
            var hv = SpawnHouse(r);
            root.Persist("house-placed:" + r.kindId);
            if (hv != null)
            {
                hv.Pop();
                Fx.Burst(Catalog, hv.TopWorld, new Color(1f, 0.85f, 0.6f), 36, 0.02f, 0.5f, 1.2f, 0.04f);
                root.Audio.Play("chime", 0.7f, 1.1f);
                var def = Catalog.ToyDef(r.kindId);
                root.Say(Josa.IGa((def != null ? def.displayName : "친구") + "의 집") + " 생겼어요!", 3f);
                StartCoroutine(SendHome(r.kindId));
            }
        }

        IEnumerator SendHome(string kind)
        {
            yield return new WaitForSeconds(1.2f);
            var agent = ResidentAgents.FirstOrDefault(a => a != null && a.Actor != null && a.Actor.name == kind);
            if (agent != null && !agent.OnRide) { ShowBubble(agent.Actor, "와! 내 집이다!", 2.4f); agent.ForceHome(); }
        }

        /// <summary>
        /// Not now: the house waits in the storage box (사용하기 sets it down any time) and the friend asks again the next time
        /// the village opens (user 2026-11-27: after 나중에 the house seemed gone).
        /// </summary>
        void HouseLater()
        {
            if (!HousePlacing) return;
            var r = houseTarget;
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null; houseTarget = null; HousePlacing = false;
            EndHousePlaceBar(); SetHud(true);
            if (r != null && !newInBox.Contains(HouseItemPrefix + r.kindId)) { newInBox.Add(HouseItemPrefix + r.kindId); RefreshStorageBadge(); }
            root.Say("집은 보관함에 넣어 뒀어요. 보관함에서 언제든 놓을 수 있어요", 3f);
        }

        void EndHousePlaceBar()
        {
            if (PlaceUi != null) PlaceUi.Show(false);
            WirePlaceBarForFurniture();
            Hud.Hint(null);
        }

        const string HouseItemPrefix = "house:";

        /// <summary>The houses not set down yet, as storage-box entries.</summary>
        System.Collections.Generic.IEnumerable<Mongle.Views.StorageView.Item> HousesInBox()
        {
            if (Catalog.houses == null || Catalog.houses.Length == 0) yield break;
            foreach (var r in root.Residents)
            {
                if (r == null || SpotOf(r, out _, out _) || !HouseLots.HasLook(r)) continue;
                var def = Catalog.ToyDef(r.kindId); string name = def != null ? def.displayName : "친구";
                var prefab = Catalog.houses[r.houseVariant % Catalog.houses.Length];
                yield return new Mongle.Views.StorageView.Item
                {
                    id = HouseItemPrefix + r.kindId, name = name + "의 집", count = 1, kind = Mongle.Views.StorageView.Kind.Furniture,
                    description = Josa.IGa(name) + " 살 집이에요. 사용하기를 누르면 마을에 놓아요.",
                    icon = prefab != null ? IconBaker.Get(HouseItemPrefix + r.houseVariant, prefab) : null,
                };
            }
        }

        /// <summary>Still waiting in the box: a bought piece, or a house not set down yet.</summary>
        bool InBox(string id) => id != null && id.StartsWith(HouseItemPrefix)
            ? root.Residents.Any(r => r != null && HouseItemPrefix + r.kindId == id && !SpotOf(r, out _, out _))
            : World.Owned(id) > 0;

        /// <summary>사용하기 on a house: the box closes and the house follows the pointer to be set down.</summary>
        bool UseHouseFromBox(string id)
        {
            if (id == null || !id.StartsWith(HouseItemPrefix)) return false;
            var r = root.Residents.FirstOrDefault(x => x != null && HouseItemPrefix + x.kindId == id);
            if (StorageOpen) CloseStorage();
            if (r != null && !SpotOf(r, out _, out _)) { housePrompted.Remove(r.kindId); BeginHousePlacement(r); }
            return true;
        }

        public bool QaBoxHas(string id) => StorageItems().Any(i => i.id == id);
        public void QaUseFromBox(string id) => UseFromBox(id);

        // ---------------- living in it ----------------
        public void ClickHouse(HouseView h)
        {
            if (h == null) return;
            var def = Catalog.ToyDef(h.kindId); string name = def != null ? def.displayName : "친구";
            root.Audio.Play("click", 0.5f, 1.05f);
            var agent = ResidentAgents.FirstOrDefault(a => a != null && a.Actor != null && a.Actor.name == h.kindId);
            if (agent != null && agent.AtHome) { root.Say(Josa.EunNeun(name) + " 지금 집 안에서 쉬고 있어요", 2.4f); return; }
            if (agent != null && !agent.OnRide && agent.State != AgentState.Disabled && agent.ForceHome())
            {
                ShowBubble(agent.Actor, "집에 갈게요!", 2f);
                root.Say(name + "네 집이에요. " + Josa.IGa(name) + " 집에 가요", 2.6f);
            }
            else root.Say(name + "네 집이에요", 2.2f);
        }
    }
}
