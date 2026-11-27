using System.Collections;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>D-002: shops, storage box, placing bought pieces, watering plants, and the pieces Mongi reacts to.</summary>
    public partial class VillageDirector
    {
        /// <summary>The shop window (11-26 "상점" 88:1143, <see cref="Mongle.Views.ShopView"/>).</summary>
        public Mongle.Views.ShopView Shop { get; private set; }
        /// <summary>The 보관함 (11-26 "보관함" 88:825, <see cref="Mongle.Views.StorageView"/>).</summary>
        public Mongle.Views.StorageView Storage { get; private set; }
        public ShopBuilding OpenedShop { get; private set; }
        public readonly System.Collections.Generic.List<ShopBuilding> Shops = new System.Collections.Generic.List<ShopBuilding>();
        public bool AnyPanelOpen => ShopOpen || StorageOpen || ClawOpen || HousePlacing;
        /// <summary>A window over the village — everything but the friend card: the HUD steps away (VillageHudView.SetWindowOpen).</summary>
        public bool WindowOpen => ShopOpen || StorageOpen || ClawOpen || QuestBoardOpen || FriendDialogOpen
            || Mongle.Views.AlbumView.IsOpen || Mongle.Views.DialogView.IsOpen || Mongle.UI2.UiFront.SettingsOpen;
        public bool HudHidden => Hud != null && Hud.WindowHidden;
        bool ShopOpen => Shop != null && Shop.Visible;
        bool StorageOpen => Storage != null && Storage.Visible;
        static readonly Color Water = new Color(0.55f, 0.8f, 1f, 1f);

        // ---------------- setup ----------------
        void SetupShops()
        {
            foreach (var b in FindObjectsByType<ShopBuilding>(FindObjectsInactive.Exclude))
            {
                b.Def = Catalog.Shop(b.shopId);
                if (b.Def == null) { Debug.LogWarning("[Village] no ShopDefinition for " + b.shopId); continue; }
                Shops.Add(b);
            }
        }

        void BuildShopUi()
        {
            if (Application.isPlaying) Shop = Mongle.Views.ShopView.Get();
            if (Shop != null)
            {
                Shop.OnBuy = id => TryBuy(Catalog.Furniture(id));
                Shop.OnAccept = id => AcceptQuest(QuestById(id));
                Shop.OnClaim = id => ClaimQuest(QuestById(id));
                Shop.OnGo = _ => CloseShop();   // 찾으러 가기: back to the village, the 퀘스트 pill shows how far it got
                Shop.OnClose = CloseShop;
            }
            if (Application.isPlaying) Storage = Mongle.Views.StorageView.Get();
            if (Storage != null) { Storage.OnUse = UseFromBox; Storage.OnClose = CloseStorage; }
        }

        public string CoinsShown => Hud != null ? Hud.CoinsText : "";

        // ---------------- quests (D-003) ----------------
        public string TrackerShown => Hud != null ? Hud.TrackerText : "";

        void RefreshQuestTracker()
        {
            if (Hud == null) return;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var q in World.AllQuests)
            {
                var st = World.StatusOf(q);
                // (only glyphs the title font has: no ▸ ✓ —, D-026)
                if (st == QuestStatus.Active) lines.Add(q.title + "  " + World.ProgressOf(q) + "/" + q.targetCount);
                else if (st == QuestStatus.ReadyToClaim) lines.Add(q.title + " · 가게에서 보상 받기!");
            }
            // the friends' requests (D-011)
            foreach (var fq in World.FriendQuests.Active)
            {
                var giver = Catalog.ToyDef(fq.giver);
                lines.Add((giver != null ? giver.displayName : fq.giver) + " · " + FriendQuestText.Title(fq.kind) + (fq.target > 1 ? "  " + fq.progress + "/" + fq.target : ""));
            }
            foreach (var fq in World.FriendQuests.Waiting)
            {
                var giver = Catalog.ToyDef(fq.giver); string n = giver != null ? giver.displayName : fq.giver;
                lines.Add(n + " · 다 했어요! 가서 알려 주기");
            }
            Hud.SetTracker(lines);   // under the 퀘스트 pill
        }

        void OnQuestChanged(QuestDefinition q)
        {
            RefreshQuestTracker();
            var news = World.StatusOf(q);
            if ((news == QuestStatus.Active && World.ProgressOf(q) == 0) || news == QuestStatus.ReadyToClaim) NoteQuestNews(); // accepted or finished
            if (QuestBoardOpen) RefreshQuestBoard();
            root.Persist("quest:" + q.id);
            if (World.StatusOf(q) == QuestStatus.ReadyToClaim)
            {
                var shop = Catalog.Shop(q.giverShopId);
                root.Say("부탁 완료! " + (shop != null ? shop.keeperName : "가게") + "에게 가서 보상을 받아요", 3.5f);
                root.Audio.Play("chime", 0.7f);
            }
        }

        QuestDefinition QuestById(string id) => World.AllQuests.FirstOrDefault(q => q != null && q.id == id);

        public void AcceptQuest(QuestDefinition q)
        {
            if (q == null || !World.AcceptQuest(q)) return;
            if (ShopOpen) Shop.SetLine(q.acceptLine);
            root.Audio.Play("hello", 0.8f, 1.1f);
            if (OpenedShop != null && OpenedShop.Keeper != null) OpenedShop.Keeper.Hop();
            RefreshShop();
        }

        public void ClaimQuest(QuestDefinition q)
        {
            if (q == null || !World.ClaimQuest(q)) return;
            if (ShopOpen) Shop.SetLine(q.doneLine);
            root.Audio.Play("success", 0.8f);
            if (OpenedShop != null && OpenedShop.Keeper != null) OpenedShop.Keeper.Hop();
            root.Say("부탁 완료! 몽글 코인 +" + q.rewardCoins, 2.6f);
            RefreshShop();
        }

        void RefreshCoins() { if (Hud != null) Hud.SetCoins(World.Coins); RefreshStorageBadge(); RefreshAlbumBadge(); }

        void SetHud(bool on) { if (Hud != null) Hud.ShowHud(on); }

        // ---------------- clicking in the world ----------------
        public bool TryClickWorld(Vector2 screen)
        {
            if (!state.Admitted || Agent == null || Agent.State == AgentState.Disabled || AnyPanelOpen || QuestUiOpen) return false;
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 40f).OrderBy(x => x.distance).ToList();
            // the friend card closes on a click anywhere outside it (user 2026-11-26: "카드 밖에 클릭하면 나갈 수 있게");
            // a click on another friend goes on to open that friend's card
            if (DressOpen)
            {
                var was = Dressing;
                var toy = hits.Select(x => x.collider.GetComponentInParent<ToyAgent>()).FirstOrDefault(t => t != null);
                CloseDress();
                if (toy == null || toy == was) return true;
            }
            // a fallen fruit or a hunt star wins even when a tree or a toy is in front of it (D-011)
            var pickup = hits.Select(x => x.collider.GetComponentInParent<QuestPickup>()).FirstOrDefault(p => p != null && !p.Collected);
            if (pickup != null) { ClickPickup(pickup); return true; }
            foreach (var h in hits)
            {
                var toyHit = h.collider.GetComponentInParent<ToyAgent>();
                if (toyHit != null && ClaimDoneOf(toyHit)) return true; // its request is done: telling it pays (user 2026-11-27)
                if (toyHit != null && OpenOfferOf(toyHit)) return true; // a "!" or "?" over its head: the request comes first
                if (toyHit == Agent) return TryClickToy(screen);
                if (toyHit != null && !toyHit.IsMain) { GreetResident(toyHit); return true; }
                var ride = h.collider.GetComponentInParent<Ride>();
                if (ride != null) { if (!CallQuestRider(ride)) ClickRide(ride); return true; }
                var clawM = h.collider.GetComponentInParent<ClawMachine>();
                if (clawM != null) { ClickClaw(clawM); return true; }
                var house = h.collider.GetComponentInParent<HouseView>();
                if (house != null) { ClickHouse(house); return true; }
                var hospital = h.collider.GetComponentInParent<HospitalBuilding>();
                if (hospital != null) { ClickHospital(hospital); return true; }
                var f = h.collider.GetComponentInParent<FurnitureObject>();
                if (f != null && !f.Pending) { ClickFurniture(f); return true; }
                var shop = h.collider.GetComponentInParent<ShopBuilding>();
                if (shop != null) { OpenShop(shop); return true; }
                var tree = h.collider.GetComponentInParent<FruitTree>();
                if (tree != null) { ShakeTree(tree); return true; }
                // the ground does nothing: Mongi is one of the residents and lives on its own (no player steering)
                if (h.collider.gameObject.name == "WalkArea") return false;
            }
            return false;
        }

        public void ClickFurniture(FurnitureObject f)
        {
            if (f.Def.IsPlant) { WaterPlant(f); return; }
            f.Poke();
            root.Audio.Play((f.Tags & InteractionTag.Play) != 0 ? "pop" : "click", 0.5f, 1.2f);
        }

        // ---------------- plants ----------------
        public WaterResult WaterPlant(FurnitureObject f)
        {
            var id = f.Placement.Id;
            var res = World.Water(id, f.Def, out bool stageUp, out int bloom);
            switch (res)
            {
                case WaterResult.TooSoon:
                    var left = Mathf.CeilToInt(World.SecondsUntilThirsty(World.Plant(id), f.Def));
                    root.Say("아직 촉촉해요. " + left + "초 뒤에 또 줘요", 1.6f); f.Wobble(); root.Audio.Play("click", 0.4f, 0.9f);
                    break;
                case WaterResult.FullyGrown:
                    root.Say("활짝 피었어요!", 1.6f); if (f.Plant != null) f.Plant.Pop(); root.Audio.Play("sparkle", 0.5f);
                    break;
                case WaterResult.Ok:
                    root.Persist("water");
                    StartCoroutine(WaterRoutine(f, stageUp, bloom));
                    break;
            }
            return res;
        }

        IEnumerator WaterRoutine(FurnitureObject f, bool stageUp, int bloom)
        {
            GameObject can = Catalog.wateringCan != null ? Instantiate(Catalog.wateringCan) : null;
            ParticleSystem stream = null;
            var top = f.transform.position + Vector3.up * 0.42f + f.transform.right * -0.16f;
            if (can != null)
            {
                can.transform.position = top; can.transform.rotation = Quaternion.Euler(0, 90f, 0);
                var spout = can.transform.Find("Spout");
                stream = Fx.Stream(Catalog, spout != null ? spout : can.transform, Water, 70f, 0.012f, 0.05f);
                stream.transform.rotation = Quaternion.Euler(90f, 0, 0);
                var main = stream.main; main.gravityModifier = 1.2f; main.startLifetime = 0.45f;
            }
            root.Audio.Play("water", 0.7f);
            float t = 0f, dur = 1.25f;
            while (t < dur && can != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.35f) * Mathf.Clamp01((dur - t) / 0.3f);
                can.transform.rotation = Quaternion.Euler(0, 90f, 0) * Quaternion.Euler(0, 0, -28f * k);
                if (stream != null && !stream.isPlaying && k > 0.5f) stream.Play();
                yield return null;
            }
            if (stream != null) stream.Stop();
            if (can != null) Destroy(can, 0.6f);
            if (f != null && f.Plant != null)
            {
                f.Plant.Refresh(true);
                if (stageUp)
                {
                    root.Audio.Play("chime", 0.7f);
                    Fx.Burst(Catalog, f.transform.position + Vector3.up * 0.2f, new Color(1f, 0.9f, 0.55f), 22, 0.016f, 0.3f, 0.9f, -0.02f);
                    root.Say("쑥쑥 자랐어요!", 1.8f);
                }
                if (bloom > 0)
                {
                    root.Audio.Play("success", 0.8f);
                    Fx.Burst(Catalog, f.transform.position + Vector3.up * 0.3f, new Color(1f, 0.7f, 0.85f), 36, 0.02f, 0.45f, 1.2f, -0.03f);
                    root.Say("활짝 피었어요! 몽글 코인 +" + bloom, 2.4f);
                }
            }
        }

        // ---------------- shop ----------------
        public bool OpenShop(ShopBuilding b)
        {
            if (b == null || b.Def == null || Shop == null || AnyPanelOpen) return false;
            if (PlacingMode) { root.Say("가구 옮기기를 먼저 끝내요"); return false; }
            CloseDress(); CloseQuestUi(); // the dress panel would cover the shop's buttons
            OpenedShop = b;
            var key = "shop:" + b.shopId;
            string line;
            if (!World.HasSeen(key)) { World.MarkSeen(key); line = b.Def.firstVisitLine; root.Persist("shop-first-visit"); }
            else line = Pick(b.Def.greetings);
            Orbit.FocusOn(b.CameraSpot != null ? b.CameraSpot.position : b.transform.position + new Vector3(0, 1.1f, 2.4f), b.KeeperHead.position + Vector3.down * 0.02f - b.transform.right * 0.62f);
            // Figma 88:1143: only the coins, the gear and the zoom pair stay over the village; the sheet's ◀ leads back
            if (Hud != null) { Hud.Hint(null); Hud.Focus(true, false); }
            var quests = World.QuestsOf(b.shopId);
            bool claim = quests.Any(q => World.StatusOf(q) == QuestStatus.ReadyToClaim);
            if (claim) line = "부탁 들어줘서 고마워요! 보상을 받아 가요.";
            Shop.Show(b.Def.displayName, b.Def.keeperName, line, ShopItems(b.Def), ShopAsks(b.Def), claim);
            if (b.Keeper != null) b.Keeper.Hop();
            root.Audio.Play("hello", 0.8f, 1.1f);
            return true;
        }

        public void CloseShop()
        {
            if (OpenedShop == null) return;
            Shop.Hide(); OpenedShop = null; Orbit.ReleaseFocus();
            if (Hud != null) { Hud.Focus(false); RefreshQuestTracker(); }
            root.Audio.Play("click", 0.4f, 0.9f);
        }

        /// <summary>The cards of a shop: what it sells, at what price, and whether the coins reach.</summary>
        System.Collections.Generic.List<Mongle.Views.ShopView.Item> ShopItems(ShopDefinition shop) =>
            shop.items.Where(i => i != null).Select(i => new Mongle.Views.ShopView.Item
            {
                id = i.id, name = i.displayName, price = i.price, icon = IconBaker.Get(i), afford = World.Coins >= i.price,
            }).ToList();

        /// <summary>The keeper's requests that are open to the player (a chain's later ones show once the earlier is done).</summary>
        System.Collections.Generic.List<Mongle.Views.Ask> ShopAsks(ShopDefinition shop) =>
            World.QuestsOf(shop.id).Where(q => q != null && World.StatusOf(q) != QuestStatus.Locked).Select(q =>
            {
                var st = World.StatusOf(q);
                return new Mongle.Views.Ask
                {
                    id = q.id, title = q.title, giver = shop.keeperName, reward = q.rewardCoins, picture = QuestPicture(q),
                    line = st == QuestStatus.Done ? q.doneLine : q.request,
                    progress = st == QuestStatus.Active ? World.ProgressOf(q) + "/" + q.targetCount : null,
                    state = st == QuestStatus.Available ? Mongle.Views.AskState.Open : st == QuestStatus.Active ? Mongle.Views.AskState.Doing
                        : st == QuestStatus.ReadyToClaim ? Mongle.Views.AskState.Ready : Mongle.Views.AskState.Done,
                };
            }).ToList();

        /// <summary>The picture of a request: a hand-made or baked one (Resources/UI/QuestIcons/&lt;id&gt;, QuestIconBaker) or the icon of
        /// the item it names (the chair, the ferris wheel, the tulip pot).</summary>
        Sprite QuestPicture(QuestDefinition q)
        {
            var own = Resources.Load<Sprite>("UI/QuestIcons/" + q.id);
            if (own != null) return own;
            return string.IsNullOrEmpty(q.eventParam) ? null : IconBaker.Get(Catalog.Furniture(q.eventParam));
        }

        /// <summary>The open shop again after coins, the box or a request changed (the page and the tab stay).</summary>
        void RefreshShop()
        {
            if (!ShopOpen || OpenedShop == null) return;
            Shop.SetItems(ShopItems(OpenedShop.Def));
            Shop.SetAsks(ShopAsks(OpenedShop.Def));
        }

        public BuyResult TryBuy(FurnitureDefinition def)
        {
            if (def == null) return BuyResult.NotForSale;
            var res = World.Buy(def);
            var keeper = OpenedShop != null ? OpenedShop.Def : null;
            switch (res)
            {
                case BuyResult.Ok:
                    root.Persist("buy:" + def.id);
                    root.Audio.Play("success", 0.8f);
                    if (OpenedShop != null && OpenedShop.Keeper != null) OpenedShop.Keeper.Hop();
                    // say where it went and offer the next step right there (the user could not tell where purchases went)
                    string said = keeper != null ? string.Format(Pick(keeper.buyLines), def.displayName) + "\n" : "";
                    if (def.IsAccessory)
                    {
                        if (ShopOpen) Shop.SetLine(said + "보관함의 악세서리 상자에 넣었어요. 친구에게 입혀 볼까요?", "입혀 보기", () => DressFromBox(def));
                        root.Say(Josa.EulReul(def.displayName) + " 보관함에 넣었어요! 친구를 누르면 '꾸며주기'로 입혀요", 3.6f);
                    }
                    else
                    {
                        if (ShopOpen) Shop.SetLine(said + "보관함에 넣어 뒀어요. 지금 마을에 놓아 볼까요?", "지금 놓기", () => BeginPlaceFromInventory(def));
                        root.Say(Josa.EulReul(def.displayName) + " 보관함에 넣었어요! 오른쪽 위 '보관함'에서도 놓을 수 있어요", 3.6f);
                    }
                    if (!newInBox.Contains(def.id)) newInBox.Add(def.id);
                    RefreshStorageBadge();
                    break;
                case BuyResult.NotEnoughCoins:
                    root.Audio.Play("wrong", 0.5f);
                    if (keeper != null && ShopOpen) Shop.SetLine(string.Format(Pick(keeper.poorLines), def.displayName));
                    break;
            }
            RefreshShop();
            return res;
        }

        string Pick(string[] lines) => lines == null || lines.Length == 0 ? "" : lines[Random.Range(0, lines.Length)];

        // ---------------- storage + placing bought pieces ----------------
        public void ShowStorage()
        {
            if (PlacingMode) { root.Say("가구 옮기기 중이에요. '나가기'를 누르고 열어요"); return; }
            if (AnyPanelOpen) return;
            CloseDress(); CloseQuestUi();
            SetHud(false);
            newInBox.Clear(); RefreshStorageBadge();
            // everything bought waits here: pieces to place, and accessories to put on a friend
            if (Storage != null) Storage.Show(StorageItems());
            root.Audio.Play("click", 0.5f);
        }

        /// <summary>What is in the box: furniture, then plants, then accessories (the 전체 / 가구 / 악세서리 tabs read the same way),
        /// and a friend's house that was left for later.</summary>
        System.Collections.Generic.List<Mongle.Views.StorageView.Item> StorageItems()
        {
            var list = Catalog.furniture.Where(f => f != null && (f.Placeable || f.IsAccessory) && World.Owned(f.id) > 0).OrderBy(f => (int)f.category)
                .Select(f => new Mongle.Views.StorageView.Item
                {
                    id = f.id, name = f.displayName, description = f.description, icon = IconBaker.Get(f), count = World.Owned(f.id),
                    kind = f.IsAccessory ? Mongle.Views.StorageView.Kind.Accessory : Mongle.Views.StorageView.Kind.Furniture,
                }).ToList();
            list.AddRange(HousesInBox());
            return list;
        }

        /// <summary>사용하기: a piece goes into the village to be placed, an accessory onto a friend.</summary>
        void UseFromBox(string id)
        {
            if (UseHouseFromBox(id)) return;
            var def = Catalog.Furniture(id);
            if (def == null) return;
            if (def.IsAccessory) DressFromBox(def); else BeginPlaceFromInventory(def);
        }

        // ---------------- "where did it go": the box badge and the one-tap next step after buying ----------------
        /// <summary>Things bought since the box was last opened; the badge counts those still waiting in it.</summary>
        readonly System.Collections.Generic.List<string> newInBox = new System.Collections.Generic.List<string>();

        void RefreshStorageBadge()
        {
            if (Hud == null || root == null) return;
            Hud.SetStorageNews(newInBox.Count(InBox));
        }

        /// <summary>An accessory from the box: open Mongi's dress-up right away (other friends: click them in the park).</summary>
        public bool DressFromBox(FurnitureDefinition def)
        {
            if (Agent == null || !state.Admitted) return false;
            if (ShopOpen) CloseShop();
            if (StorageOpen) CloseStorage();
            if (PlacingMode) ExitPlacing();
            LookAt(Agent.transform.position);
            OpenDress(Agent);
            if (!DressOpen) return false;
            ShowBubble(Agent.Actor, "저도 입어 볼래요!", 2.4f);
            root.Say((def != null ? Josa.EulReul(def.displayName) + " 눌러서 입혀요. " : "") + "다른 친구는 마을에서 친구를 누르면 꾸밀 수 있어요", 4f);
            return true;
        }

        public void CloseStorage() { if (Storage != null) Storage.Hide(); SetHud(true); root.Audio.Play("click", 0.4f, 0.9f); }

        public bool BeginPlaceFromInventory(FurnitureDefinition def)
        {
            if (def == null || World.Owned(def.id) <= 0 || !def.Placeable) return false;
            if (ShopOpen) CloseShop();
            if (StorageOpen) CloseStorage();
            if (!PlacingMode) SetPlacing(true);
            else if (Selected != null) CancelPlacement();
            // the new piece appears where the player is looking (the shops), and the view glides onto it
            if (!FindFreeCell(def, Orbit.target, out int cx, out int cy)) { root.Say("놓을 자리가 없어요. 가구를 정리해 봐요"); SetPlacing(false); return false; }
            var p = new FurniturePlacement { furnitureId = def.id, instanceId = World.NewInstanceId(def.id), cellX = cx, cellY = cy, rotation = 0 };
            var f = FurnitureObject.Spawn(def, p, Grid);
            f.Pending = true; f.IsNew = true;
            Furniture.Add(f);
            if (f.Plant != null) f.Plant.Init(World, def, p.Id);
            Select(f);
            LookAt(f.transform.position);
            Fx.Burst(Catalog, f.transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.5f), 18, 0.02f, 0.3f, 0.9f, -0.02f);
            Hud.Hint("새 " + def.displayName + "! 끌어서 자리를 고르고 '여기에 두기'를 눌러요");
            root.Audio.Play("pop", 0.6f);
            return true;
        }

        bool FindFreeCell(FurnitureDefinition def, Vector3 near, out int cx, out int cy)
        {
            cx = cy = 0; int best = int.MaxValue; bool found = false;
            var others = Furniture.Select(x => (x.Def.footprint, x.Placement));
            var mid = Grid.CellAt(near);
            for (int y = Grid.MinY; y < Grid.MaxY; y++)
                for (int x = Grid.MinX; x < Grid.MaxX; x++)
                {
                    if (!Grid.IsValid(others, def.footprint, x, y, 0)) continue;
                    int d = Mathf.Abs(x - mid.x) + Mathf.Abs(y - mid.y);
                    if (d < best) { best = d; cx = x; cy = y; found = true; }
                }
            return found;
        }

        void DiscardNew(FurnitureObject f, string message)
        {
            Furniture.Remove(f);
            if (Selected == f) Selected = null;
            Destroy(f.gameObject);
            if (!string.IsNullOrEmpty(message)) root.Say(message);
            root.Audio.Play("click", 0.4f, 0.8f);
        }

        public void StoreSelected()
        {
            if (Selected == null) { root.Say("먼저 옮길 가구를 눌러요"); return; }
            var f = Selected;
            if (f.IsNew) { DiscardNew(f, "보관함으로 돌아갔어요"); return; }
            if (!f.Def.Storable) { root.Say(f.Def.IsPlant ? "식물은 보관함에 넣을 수 없어요" : "이 가구는 마을에 계속 있어요"); root.Audio.Play("wrong", 0.4f); return; }
            if (f.InUse) { root.Say("몽이가 쓰고 있어요"); return; }
            if (PlacingMode) RecordPlacement(PlacementChange.Store, f, f.Placement, 0, 0, 0);   // 되돌리기 puts it back where it stood
            state.Furniture.RemoveAll(x => x.Id == f.Placement.Id);
            World.AddToInventory(f.Def.id);
            Agent.OnFurnitureChanged(f);
            Furniture.Remove(f); Selected = null; Destroy(f.gameObject);
            root.Persist("store");
            root.Say("보관함에 넣었어요"); root.Audio.Play("pop", 0.5f, 0.8f);
        }

        // ---------------- queries for the agent ----------------
        public FurnitureObject PickTarget(InteractionTag tag, ToyAgent agent)
        {
            var c = Furniture.Where(f => (f.Tags & tag) != 0 && agent.Available(f)).ToList();
            return c.Count == 0 ? null : c[Random.Range(0, c.Count)];
        }

        public FurnitureObject NextUnseen(ToyAgent agent) =>
            Furniture.FirstOrDefault(f => !f.Pending && !World.HasSeen(f.Placement.Id) && agent.Available(f));

        public void SaveWorld() => root.Persist("world");

        // ---------------- test hooks ----------------
        public bool QaOpenShop(string id) => OpenShop(Shops.FirstOrDefault(s => s.shopId == id));
        public BuyResult QaBuy(string itemId) => TryBuy(Catalog.Furniture(itemId));
        public bool QaPlace(string itemId, int cx, int cy, int rot)
        {
            if (!BeginPlaceFromInventory(Catalog.Furniture(itemId))) return false;
            pendingX = cx; pendingY = cy; pendingRot = rot; Preview();
            return ConfirmPlacement();
        }
        public bool QaStore(string furnitureId)
        {
            var f = Furniture.FirstOrDefault(x => x.Def.id == furnitureId && !x.Pending); if (f == null) return false;
            if (!PlacingMode) SetPlacing(true);
            Select(f); int before = Furniture.Count; StoreSelected();
            return Furniture.Count < before;
        }
        public FurnitureObject QaInstance(string furnitureId) => Furniture.FirstOrDefault(f => f.Def.id == furnitureId && !f.Pending);
    }
}
