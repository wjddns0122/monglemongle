using System.Linq;
using Mongle.Data;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// Furniture placing's own screen (Figma 115:1398, <see cref="Mongle.Views.PlaceBarView"/>): ◀ leaves placing, the bar's four
    /// buttons act on the picked piece, and 되돌리기 (Z) / 다시 하기 (Y) take back or do again the changes confirmed since placing
    /// began (moved or turned, put out of the box, put away). Leaving placing forgets them.
    /// </summary>
    public partial class VillageDirector
    {
        /// <summary>The placing screen (Assets/_UI수정/3_마을/PlaceBar.prefab).</summary>
        public Mongle.Views.PlaceBarView PlaceUi { get; private set; }
        readonly PlacementHistory placeHistory = new PlacementHistory();
        public PlacementHistory PlaceHistory => placeHistory;

        void BindPlaceBar()
        {
            if (!Application.isPlaying) return;   // (the editor's screen-prefab baker builds the old canvas too: nothing in the open scene)
            PlaceUi = FindAnyObjectByType<Mongle.Views.PlaceBarView>(FindObjectsInactive.Include);
            if (PlaceUi == null)
            {
                var prefab = Mongle.Views.ViewIndex.Prefab("PlaceBar");
                if (prefab == null) { Debug.LogWarning("[Village] PlaceBar is not listed in ViewIndex"); return; }
                PlaceUi = Instantiate(prefab).GetComponent<Mongle.Views.PlaceBarView>();
                PlaceUi.name = "PlaceBar";
            }
            WirePlaceBarForFurniture();
            PlaceUi.Show(false);
        }

        void WirePlaceBarForFurniture()
        {
            if (PlaceUi == null) return;
            PlaceUi.OnBack = ExitPlacing; PlaceUi.OnUndo = UndoPlacement; PlaceUi.OnRedo = RedoPlacement;
            PlaceUi.OnRotate = RotateSelected; PlaceUi.OnStore = StoreSelected; PlaceUi.OnPut = () => ConfirmPlacement(); PlaceUi.OnCancel = CancelOrExitPlacing;
            PlaceUi.SetUndoShown(true); PlaceUi.SetPutReady(true);
        }

        /// <summary>A friend's house is set down with the same screen (user 2026-11-27: "건물 옮기는거 … 가구랑 똑같이"): ◀, 보관함에
        /// 넣기 and ✕ leave it in the box for later, 돌리기 turns it, 여기에 두기 sets it down; nothing to take back.</summary>
        void WirePlaceBarForHouse()
        {
            if (PlaceUi == null) return;
            PlaceUi.OnBack = HouseLater; PlaceUi.OnStore = HouseLater; PlaceUi.OnCancel = HouseLater;
            PlaceUi.OnRotate = HouseRotate; PlaceUi.OnPut = HouseConfirm; PlaceUi.OnUndo = null; PlaceUi.OnRedo = null;
            PlaceUi.SetUndoShown(false);
            PlaceUi.Show(true);
            PlaceUi.Refresh(true, false, false);   // 여기에 두기's tip over its button
        }

        void RefreshPlaceBar()
        {
            if (PlacingMode && PlaceUi != null) PlaceUi.Refresh(Selected != null, placeHistory.CanUndo, placeHistory.CanRedo);
        }

        void RecordPlacement(PlacementChange kind, FurnitureObject f, FurniturePlacement from, int toX, int toY, int toRot)
        {
            placeHistory.Record(new PlacementStep
            {
                kind = kind, furnitureId = f.Def.id, instanceId = f.Placement.Id,
                fromX = from != null ? from.cellX : 0, fromY = from != null ? from.cellY : 0, fromRot = from != null ? from.rotation : 0,
                toX = toX, toY = toY, toRot = toRot,
            });
        }

        /// <summary>되돌리기: the piece being moved goes back first (it was never confirmed); then the newest confirmed change.</summary>
        public void UndoPlacement()
        {
            if (!PlacingMode) return;
            if (Selected != null) { CancelPlacement(); return; }
            if (!placeHistory.PeekUndo(out var s)) { root.Say("되돌릴 게 없어요"); root.Audio.Play("wrong", 0.3f); return; }
            string why = s.kind == PlacementChange.Move ? MovePiece(s.instanceId, s.fromX, s.fromY, s.fromRot)
                : s.kind == PlacementChange.Place ? PutPieceAway(s.instanceId)
                : PutPieceOut(s.furnitureId, s.instanceId, s.fromX, s.fromY, s.fromRot);
            if (why != null) { root.Say(why); root.Audio.Play("wrong", 0.4f); return; }
            placeHistory.CommitUndo();
            root.Persist("undo placing");
            root.Audio.Play("click", 0.5f, 0.9f);
        }

        /// <summary>다시 하기: the change taken back last, done again (a piece being moved is put back first).</summary>
        public void RedoPlacement()
        {
            if (!PlacingMode) return;
            if (!placeHistory.PeekRedo(out var s)) { root.Say("다시 할 게 없어요"); root.Audio.Play("wrong", 0.3f); return; }
            if (Selected != null) CancelPlacement();
            string why = s.kind == PlacementChange.Move ? MovePiece(s.instanceId, s.toX, s.toY, s.toRot)
                : s.kind == PlacementChange.Place ? PutPieceOut(s.furnitureId, s.instanceId, s.toX, s.toY, s.toRot)
                : PutPieceAway(s.instanceId);
            if (why != null) { root.Say(why); root.Audio.Play("wrong", 0.4f); return; }
            placeHistory.CommitRedo();
            root.Persist("redo placing");
            root.Audio.Play("click", 0.5f, 1.1f);
        }

        FurnitureObject PieceById(string id) => Furniture.FirstOrDefault(x => !x.Pending && x.Placement.Id == id);

        /// <summary>A piece in the park to another cell / turn; null when done, else why not.</summary>
        string MovePiece(string id, int x, int y, int rot)
        {
            var f = PieceById(id);
            if (f == null) return "그 가구가 마을에 없어요";
            if (f.InUse) return "몽이가 쓰고 있어요. 조금 이따 해요";
            if (!Grid.IsValid(OthersOf(f), f.Def.footprint, x, y, rot)) return "그 자리에 다른 게 있어요";
            f.Placement.cellX = x; f.Placement.cellY = y; f.Placement.rotation = rot;
            f.Apply(Grid); f.SetTint(null); f.Wobble();
            var entry = state.Furniture.FirstOrDefault(e => e.Id == id);
            if (entry != null) { entry.cellX = x; entry.cellY = y; entry.rotation = rot; }
            else state.Furniture.Add(new FurniturePlacement { furnitureId = f.Placement.furnitureId, instanceId = id, cellX = x, cellY = y, rotation = rot });
            Agent.OnFurnitureChanged(f);
            return null;
        }

        /// <summary>A piece in the park into the box (as 보관함에 넣기); null when done, else why not.</summary>
        string PutPieceAway(string id)
        {
            var f = PieceById(id);
            if (f == null) return "그 가구가 마을에 없어요";
            if (!f.Def.Storable) return f.Def.IsPlant ? "식물은 보관함에 넣을 수 없어요" : "이 가구는 마을에 계속 있어요";
            if (f.InUse) return "몽이가 쓰고 있어요. 조금 이따 해요";
            state.Furniture.RemoveAll(e => e.Id == id);
            World.AddToInventory(f.Def.id);
            Agent.OnFurnitureChanged(f);
            Furniture.Remove(f); Destroy(f.gameObject);
            return null;
        }

        /// <summary>A piece from the box back onto its cell, under its old id (its "seen" and plant state stay its own).</summary>
        string PutPieceOut(string furnitureId, string id, int x, int y, int rot)
        {
            var def = Catalog.Furniture(furnitureId);
            if (def == null || World.Owned(def.id) <= 0) return "보관함에 없는 물건이에요";
            if (!Grid.IsValid(Furniture.Select(o => (o.Def.footprint, o.Placement)), def.footprint, x, y, rot)) return "그 자리에 다른 게 있어요";
            if (!World.TakeFromInventory(def.id)) return "보관함에 없는 물건이에요";
            var p = new FurniturePlacement { furnitureId = def.id, instanceId = id, cellX = x, cellY = y, rotation = rot };
            var f = FurnitureObject.Spawn(def, p, Grid);
            Furniture.Add(f);
            state.Furniture.Add(new FurniturePlacement { furnitureId = def.id, instanceId = id, cellX = x, cellY = y, rotation = rot });
            if (f.Plant != null) { World.EnsurePlant(p.Id, def.id); f.Plant.Init(World, def, p.Id); f.Plant.Refresh(false); }
            f.Wobble();
            Agent.OnFurnitureChanged(f);
            return null;
        }

        // test hooks
        public bool QaUndo() { int u = placeHistory.UndoCount; UndoPlacement(); return placeHistory.UndoCount < u; }
        public bool QaRedo() { int r = placeHistory.RedoCount; RedoPlacement(); return placeHistory.RedoCount < r; }
    }
}
