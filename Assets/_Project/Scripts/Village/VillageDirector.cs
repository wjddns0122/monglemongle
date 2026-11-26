using System.Collections;
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
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// Village scene: places furniture on a small grid, admits the toy as a resident once, observes its autonomous life.
    /// Placement edits are pending until confirmed; furniture in use cannot be moved (D-001-8).
    /// </summary>
    public partial class VillageDirector : MonoBehaviour
    {
        public static VillageDirector Current { get; private set; }
        // default orbit (D-006): a close look at the middle of the big park (the user found 24 m too wide);
        // right-drag or W/A/S/D slides the view around, scroll zooms out
        public const float DefaultOrbitYaw = 18f, DefaultOrbitPitch = 38f, DefaultOrbitDistance = 13f;
        public static readonly Vector3 DefaultOrbitTarget = new Vector3(0.3f, 0.1f, -0.8f); // the shops, the hospital and the old garden
        /// <summary>Walkable park rectangle (x min, z min, x max, z max) — keep in sync with SceneBuilder.</summary>
        public static readonly Vector4 ParkRect = new Vector4(-17.9f, -9.5f, 17.9f, 15.3f);
        /// <summary>The furniture grid: the old garden's cells (0..13, 0..9) keep their numbers, the park adds cells all around them.</summary>
        public static VillageGrid ParkGrid() => new VillageGrid(-37, -18, 51, 43, 0.4f, new Vector3(-2.8f, 0f, -2.0f));
        public WorldState World => root.World;
        public ContentCatalog Catalog { get; private set; }
        public Sfx Audio => root.Audio;
        public ToyActor Toy { get; private set; }
        public ToyAgent Agent { get; private set; }
        public VillageGrid Grid { get; private set; }
        public OrbitCamera Orbit { get; private set; }
        public readonly List<FurnitureObject> Furniture = new List<FurnitureObject>();
        public bool PlacingMode { get; private set; }
        public FurnitureObject Selected { get; private set; }
        public bool PendingValid { get; private set; } = true;
        public bool Ready { get; private set; }
        GameRoot root; ToyRuntime state; Camera cam;
        ShopUI baseUi; Canvas canvas;
        Text bubbleText; GameObject bubble; Transform bubbleAnchor; float bubbleUntil;
        GameObject placeBar;
        /// <summary>The 11-26 HUD (VillageHudView, Figma 88:231 / 88:510 / 88:2367): a Village scene object.</summary>
        public Mongle.Views.VillageHudView Hud { get; private set; }
        FurniturePlacement pendingOriginal; int pendingRot, pendingX, pendingY;
        bool dragging;
        static readonly string[] TalkLines = { "안녕! 와줬구나.", "여기 정말 포근해요.", "고쳐줘서 고마워요!", "오늘은 꽃 향기가 좋아요." };
        int talkIndex;

        System.Func<bool> windowOpen;
        System.Func<Rect> topFree;


        void OnDestroy() { if (GameRoot.WindowOpen == windowOpen) GameRoot.WindowOpen = null; if (GameRoot.TopFree == topFree) GameRoot.TopFree = null; if (Current == this) Current = null; if (root != null) { root.World.Changed -= RefreshCoins; root.World.QuestChanged -= OnQuestChanged; } UnhookFriendQuests(); }

        IEnumerator Start()
        {
            Current = this;
            // notices go below a big window (shop, storage, quest board) instead of over its title (the bottom bars keep them on top)
            // (a destroyed director compares equal to null in Unity: "this != null" keeps a stale one from being asked)
            windowOpen = () => this != null && Current == this && ((Shop != null && Shop.Visible) || (Storage != null && Storage.Visible) || QuestBoardOpen || Mongle.Views.AlbumView.IsOpen);
            GameRoot.WindowOpen = windowOpen;
            topFree = () => this != null && Current == this ? TopFree() : Rect.zero;
            GameRoot.TopFree = topFree;
            while ((root = GameRoot.Instance) == null) yield return null;
            Catalog = root.Catalog; state = root.Toy;
            var phase = FlowResolver.Resolve(state);
            if (state == null || (phase != FlowPhase.VillageAdmission && phase != FlowPhase.Village)) { root.GoToCurrentPhase(); yield break; }
            cam = Camera.main;
            Grid = ParkGrid();
            Orbit = cam.gameObject.GetOrAdd<OrbitCamera>();
            Orbit.target = DefaultOrbitTarget; Orbit.minDistance = 2.2f; Orbit.maxDistance = 24f; Orbit.minPitch = -38f; Orbit.maxPitch = 72f;
            Orbit.lookUpBelow = 6f; // drag down past a low angle and the view tilts up to the sky, balloons and clouds
            Orbit.allowPan = true; Orbit.panMin = new Vector2(ParkRect.x, ParkRect.y); Orbit.panMax = new Vector2(ParkRect.z, ParkRect.w);
            foreach (var o in FindObjectsByType<ParkObstacle>(FindObjectsInactive.Exclude)) Grid.BlockBox(o.center, o.halfSize, 0f);
            Orbit.SetOrbit(DefaultOrbitYaw, DefaultOrbitPitch, DefaultOrbitDistance); Orbit.degreesPerPixel = 0.2f; Orbit.zoomPerScroll = 0.003f;
            BuildUi();
            SpawnFurniture();
            SpawnToy();
            if (state.Admitted) { SpawnHouses(); SpawnResidents(); }
            root.Pointer.Mode = PointerMode.Orbit;
            root.Audio.StartBgm();
            SetupShops();
            SetupRides();
            SetupClaw();
            root.World.Changed += RefreshCoins; RefreshCoins();
            root.World.QuestChanged += OnQuestChanged; RefreshQuestTracker();
            SetupFriendQuests(); // D-011: friends' "!" requests and "?" questions
            Ready = true;
            if (!state.Admitted) BeginAdmission(); else { EnterLife(false); WelcomeNewResident(); }
        }

        // ---------------- setup ----------------
        List<FurniturePlacement> Defaults()
        {
            return Catalog.furniture.Where(d => d.starter).Select(d => new FurniturePlacement { furnitureId = d.id, instanceId = d.id, cellX = d.defaultCell.x, cellY = d.defaultCell.y, rotation = d.id == "Fur_Bench" ? 2 : 0 }).ToList();
        }

        void SpawnFurniture()
        {
            var layout = state.Furniture.Count > 0 ? state.Furniture : Defaults();
            foreach (var p in layout)
            {
                var def = Catalog.Furniture(p.furnitureId); if (def == null) continue;
                var fo = FurnitureObject.Spawn(def, p, Grid);
                Furniture.Add(fo);
                if (def.starter) World.MarkSeen(p.Id); // starter pieces were here before Mongi: no "new!" reaction
                if (fo.Plant != null) fo.Plant.Init(World, def, p.Id);
            }
        }

        void SpawnToy()
        {
            Toy = ToyActor.Spawn(Catalog, SafePoint(), Quaternion.Euler(0, 160, 0), false);
            Toy.ApplyRepairState(state);
            Toy.SetAccessories(state.Equipped.ToDictionary(k => k.Key, v => v.Value), Catalog);
            Toy.SetFace(0.5f, 0f);
            var go = Toy.gameObject;
            var nav = go.AddComponent<NavMeshAgent>();
            nav.baseOffset = 0f; nav.autoBraking = true;
            var col = go.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.22f, 0); col.radius = 0.13f; col.height = 0.46f;
            Agent = go.AddComponent<ToyAgent>();
            Agent.Init(Toy, nav, this, new IntentSelector(System.Environment.TickCount) { Personality = Catalog.toy.personality, Favorites = Catalog.toy.favoriteTags });
            if (NavMesh.SamplePosition(SafePoint(), out var hit, 2f, NavMesh.AllAreas)) nav.Warp(hit.position);
        }

        public Vector3 SafePoint() => Grid.Origin + new Vector3(2.5f * Grid.Cell, 0, 4.5f * Grid.Cell);

        // ---------------- queries for the agent ----------------
        public FurnitureObject Find(InteractionTag tag) => Furniture.FirstOrDefault(f => (f.Tags & tag) != 0);

        public bool RandomPoint(out Vector3 p) => RandomPoint(Grid.Middle, 100f, out p);

        /// <summary>A walkable spot on the grid, preferably within <paramref name="radius"/> of <paramref name="near"/> (a stroll, not a march across the park).</summary>
        public bool RandomPoint(Vector3 near, float radius, out Vector3 p)
        {
            float x0 = (Grid.MinX + 1) * Grid.Cell, x1 = (Grid.MaxX - 1) * Grid.Cell, z0 = (Grid.MinY + 1) * Grid.Cell, z1 = (Grid.MaxY - 1) * Grid.Cell;
            for (int i = 0; i < 16; i++)
            {
                var c = Grid.Origin + new Vector3(Random.Range(x0, x1), 0, Random.Range(z0, z1));
                if (i < 12 && (c - near).sqrMagnitude > radius * radius) continue;
                if (NavMesh.SamplePosition(c, out var hit, 0.4f, NavMesh.AllAreas)) { p = hit.position; return true; }
            }
            p = SafePoint(); return false;
        }

        // ---------------- admission ----------------
        void BeginAdmission()
        {
            Agent.Disable();
            Hud.Hint("몽이가 새 집을 기다리고 있어요");
            Hud.ShowToolbar(false);
            Mongle.Views.DialogView.Open("몽이의 새 집", "고쳐진 몽이를 마을로 초대해요. 벤치와 꽃이 기다리고 있어요.", "마을로 초대하기", () => StartCoroutine(Admit()));
            Toy.Play("Happy", 0.1f);
        }

        IEnumerator Admit()
        {
            if (state.Admit(state.ResidentId ?? System.Guid.NewGuid().ToString("N"), Defaults()))
            {
                // keep the same objects; just store the layout now in the toy state
                Debug.Log("[Village] admitted " + state.ResidentId);
            }
            root.Audio.Play("success", 0.9f);
            Fx.Burst(Catalog, Toy.transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.5f), 36, 0.02f, 0.45f, 1.1f, 0.05f);
            Hud.Hint("몽이는 이제 마을 주민이에요!");
            yield return new WaitForSeconds(1.8f);
            Hud.Hint(null);
            EnterLife(true);
        }

        /// <summary>
        /// Life in the village begins: the tiles come on. Just moved in (Figma 88:510): how to look around in the hint pill and
        /// the three tiles' tips for a while; on later visits the pill greets with the village's name and the day Mongi moved in.
        /// </summary>
        void EnterLife(bool justMovedIn)
        {
            Hud.ShowToolbar(true);
            if (justMovedIn) { Hud.Hint("오른쪽 버튼으로 끌거나 W.A.S.D로 공원을 둘러봐요.\n휠로 멀리 볼 수 있어요.", 7f); Hud.ShowTips(7f); }
            else Hud.Hint("몽이의 마을 · " + state.AdmissionDate + " 입주", 4f);
            Agent.Enable();
        }

        /// <summary>Where notices may rise to while the hint pill is away (GameRoot): the HUD knows its own top row.</summary>
        Rect TopFree() => Ready && Hud != null ? Hud.FreeTopBand() : Rect.zero;

        // ---------------- UI ----------------

        void BuildUi()
        {
            // the village's own panels (shop, storage, quest board, dress-up …) still live on the old canvas; the HUD is the 11-26 view
            baseUi = new GameObject("VillageUI").AddComponent<ShopUI>(); baseUi.Build();
            canvas = baseUi.Canvas;
            BindHud();
            BuildShopUi();
            // placement bar (fits a 4:3 screen: 1420 wide); "나가기" always ends placing, ESC too
            placeBar = UiKit.Panel(canvas.transform, "PlaceBar", UiKit.Paper, new Vector2(0.5f, 0f), new Vector2(0, 110), new Vector2(1420, 150), false).gameObject;
            UiKit.MakeButton(placeBar.transform, "돌리기", UiKit.Sky, new Vector2(0.5f, 0.5f), new Vector2(-560, 0), new Vector2(240, 100), RotateSelected, 38);
            UiKit.MakeButton(placeBar.transform, "보관함에 넣기", new Color(0.95f, 0.85f, 0.6f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-275, 0), new Vector2(300, 100), StoreSelected, 34);
            UiKit.MakeButton(placeBar.transform, "여기에 두기", UiKit.Peach, new Vector2(0.5f, 0.5f), new Vector2(40, 0), new Vector2(300, 100), () => ConfirmPlacement(), 36);
            UiKit.MakeButton(placeBar.transform, "취소", UiKit.Mint, new Vector2(0.5f, 0.5f), new Vector2(295, 0), new Vector2(180, 100), CancelOrExitPlacing, 36);
            UiKit.MakeButton(placeBar.transform, "나가기", UiKit.Pink, new Vector2(0.5f, 0.5f), new Vector2(535, 0), new Vector2(270, 100), ExitPlacing, 38);
            // plain pills on the stitched bar (D-026: one dashed line is enough)
            foreach (var b in placeBar.GetComponentsInChildren<Button>(true)) UiKit.NoStitch(b.GetComponent<Image>());
            placeBar.SetActive(false);
            // speech bubble (Figma 58:1332 "말풍선"): a white pill 406x98 with a little tail in the middle of its bottom; the plate
            // grows with long lines while the tail stays centred, its tip just above the head
            var bp = UiKit.SpriteImage(canvas.transform, "Bubble", "speech_body", Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(BubbleW, BubbleH));
            UiKit.Slice(bp);
            // the tail picture starts 4.9 inside the pill (white on white), as in the Figma frame
            UiKit.SpriteImage(bp.transform, "Tail", "speech_tail", new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 4.9f), new Vector2(34f, 27f));
            bubble = bp.gameObject; bubbleText = UiKit.Label(bp.transform, "", 32, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BubbleW - 60f, BubbleH - 16f));
            bubble.SetActive(false);
            baseUi.ApplySkin(SkinName);   // the village screen's prefab, edited in Unity (Resources/UI/Skins, D-035)
        }

        /// <summary>D-035: the editable prefab of the village screen; parts made later get it laid over them with <see cref="SkinPart"/>.</summary>
        public const string SkinName = "VillageScreen";
        void SkinPart(Component part) { if (baseUi != null && part != null) baseUi.ApplySkinTo(part.transform); }

        /// <summary>The HUD is the Village scene's own object (Assets/_UI수정/3_마을/VillageHud.prefab); the game only listens to it.</summary>
        void BindHud()
        {
            Hud = FindAnyObjectByType<Mongle.Views.VillageHudView>(FindObjectsInactive.Include);
            if (Hud == null) { var prefab = Mongle.Views.ViewIndex.Prefab("VillageHud"); if (prefab != null) Hud = Instantiate(prefab).GetComponent<Mongle.Views.VillageHudView>(); }
            Hud.gameObject.SetActive(true);
            Hud.OnHome = GoHomeView; Hud.OnQuest = ToggleQuestBoard; Hud.OnFurniture = TogglePlacing; Hud.OnAlbum = ShowAlbum; Hud.OnStorage = ShowStorage;
            Hud.OnRide = ExitRideView; Hud.OnZoom = f => Orbit.ZoomStep(f);
            Hud.ShowToolbar(false);
        }

        /// <summary>The home button: the camera goes back to the village's first view (the shops, the hospital and the old garden).</summary>
        void GoHomeView()
        {
            if (ViewingRide != null) ExitRideView();
            Orbit.target = DefaultOrbitTarget; Orbit.SetOrbit(DefaultOrbitYaw, DefaultOrbitPitch, DefaultOrbitDistance);
            root.Audio.Play("click", 0.5f);
        }

        public void ShowBubble(ToyActor actor, string line, float seconds) => ShowBubble(actor.Node("Head") ?? actor.transform, line, seconds);

        public void ShowBubble(Transform anchor, string line, float seconds)
        {
            bubbleText.text = line; bubble.SetActive(true); bubbleAnchor = anchor; bubbleUntil = Time.time + seconds;
            // the Figma size; a longer line widens it (up to 560) before it wraps, and a still longer one makes it taller
            float w = Mathf.Clamp(bubbleText.preferredWidth + 60f, BubbleW, 560f);
            bubbleText.rectTransform.sizeDelta = new Vector2(w - 60f, BubbleH - 16f);
            float h = Mathf.Max(BubbleH, bubbleText.preferredHeight + 28f);
            ((RectTransform)bubble.transform).sizeDelta = new Vector2(w, h); bubbleText.rectTransform.sizeDelta = new Vector2(w - 60f, h - 16f);
            PlaceBubble();   // right away: not one frame in the corner before Update moves it over the head
        }

        /// <summary>Figma 58:1332: the pill, and how far its tail reaches below it (27 tall, 4.9 of it inside the pill).</summary>
        const float BubbleW = 406.28f, BubbleH = 97.9f, BubbleTail = 27f - 4.9f;
        /// <summary>The pill's bottom sits this far above the head: the tail plus a little air.</summary>
        const float BubbleLift = BubbleTail + 4f;

        void PlaceBubble()
        {
            if (bubbleAnchor == null || cam == null) return;
            var sp = cam.WorldToScreenPoint(bubbleAnchor.position + Vector3.up * 0.12f);
            var scale = canvas.GetComponent<Canvas>().scaleFactor;
            ((RectTransform)bubble.transform).anchoredPosition = new Vector2(sp.x, sp.y) / scale + new Vector2(0, BubbleLift);
        }

        // ---------------- album: Mongi's first photo, then every friend's (D-005); the 11-26 binder (AlbumView, Figma 88:1393) ----------------

        public List<(PhotoRecord photo, string name)> AlbumEntries()
        {
            var list = new List<(PhotoRecord, string)>();
            if (state.Photo != null) list.Add((state.Photo, Catalog.toy != null ? Catalog.toy.displayName : "몽이"));
            foreach (var r in root.Residents)
            {
                var rec = SaveMapper.FromPhotoSave(r.photo); if (rec == null) continue;
                var def = Catalog.ToyDef(r.kindId); list.Add((rec, def != null ? def.displayName : r.kindId));
            }
            return list;
        }

        void ShowAlbum()
        {
            CloseQuestUi();
            var list = new List<Mongle.Views.AlbumView.Entry>();
            foreach (var (ph, name) in AlbumEntries())
            {
                var rec = ph;
                list.Add(new Mongle.Views.AlbumView.Entry(AlbumLine(rec, name), () => PhotoStore.Load(root.Saves, rec)));
            }
            Mongle.Views.AlbumView.Open(list);
            root.Audio.Play("click", 0.5f, 1.1f);
        }

        /// <summary>The line under a photo in the album (Figma: "2026.11.26 · 몽이").</summary>
        public static string AlbumLine(PhotoRecord ph, string name)
        {
            string day = ph != null && !string.IsNullOrEmpty(ph.takenAt) ? ph.takenAt.Split(' ')[0].Replace('-', '.') : "";
            return string.IsNullOrEmpty(day) ? name : day + " · " + name;
        }
        public int AlbumCount => AlbumEntries().Count;

        // ---------------- update ----------------
        void Update()
        {
            if (!Ready) return;
            if (bubble.activeSelf)
            {
                if (Time.time > bubbleUntil || bubbleAnchor == null) bubble.SetActive(false);
                else PlaceBubble();
            }
            var f = root.Pointer.World;
            UpdateFriendQuests(); // bubbles, the star hunt and riding together also run while watching a ride
            UpdateClaw();
            if (ViewingRide != null) { UpdateRideView(); return; } // watching a ride up close: only the "back" button acts
            HandleEscape();
            UpdatePlaceRing();
            UpdateHousePlacing();
            if ((PlacingMode || HousePlacing || !AnyPanelOpen) && state.Admitted) PollKeyboardPan(); // W/A/S/D also while placing (user)
            if (PlacingMode) UpdatePlacing(f);
            else
            {
                if (f.pressed) pressAt = f.position;
                if (f.released && (f.position - pressAt).magnitude < 8f) TryClickWorld(f.position);
            }
        }

        Vector2 pressAt;

        /// <summary>W/A/S/D or the arrow keys slide the view over the park (Mongi is not steered by the player any more).</summary>
        void PollKeyboardPan()
        {
            var kb = Keyboard.current; if (kb == null || Orbit.Focusing) return;
            float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f) - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);
            float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f) - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);
            if (x == 0f && y == 0f) return;
            var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
            var right = cam.transform.right; right.y = 0; right.Normalize();
            Orbit.Slide((right * x + fwd * y) * (2f + Orbit.distance * 0.5f) * Time.deltaTime);
        }

        /// <summary>Steer Mongi relative to the camera (what W/A/S/D and the arrow keys call).</summary>
        public void DriveToy(Vector2 input)
        {
            var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
            var right = cam.transform.right; right.y = 0; right.Normalize();
            Agent.Drive(input, fwd, right);
        }

        public bool TryClickToy(Vector2 screen)
        {
            if (state.Admitted == false || Agent == null || Agent.State == AgentState.Disabled) return false;
            var ray = cam.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 30f) && hit.collider.GetComponentInParent<ToyAgent>() == Agent)
            {
                Agent.TryTalk(ChatLine(Agent, TalkLines[talkIndex++ % TalkLines.Length])); // may remember what the player told it (D-011)
                OpenDress(Agent); // Mongi can change clothes too
                return true;
            }
            return false;
        }

        // ---------------- placement ----------------
        void TogglePlacing()
        {
            if (PlacingMode) { CancelPlacement(); SetPlacing(false); }
            else SetPlacing(true);
        }

        void SetPlacing(bool on)
        {
            PlacingMode = on;
            if (on)
            {
                // panels opened earlier (a friend's dress-up, the album) used to sit on top of the bar and swallow its buttons
                CloseDress(); Mongle.Views.AlbumView.Close(); CloseQuestUi();
                placeBar.transform.SetAsLastSibling();
            }
            placeBar.SetActive(on);
            Hud.SetPlacing(on);
            Hud.Hint(on ? "옮길 가구를 눌러서 끌어요 · 다 옮겼으면 '나가기'" : null);
            dragging = false; Orbit.allowRotate = true;
            if (!on && placeRing != null) placeRing.SetActive(false);
            root.Audio.Play("click", 0.5f);
        }

        /// <summary>"취소" puts the piece being moved back; with nothing picked it leaves placing like "나가기".</summary>
        void CancelOrExitPlacing()
        {
            if (Selected != null) CancelPlacement();
            else ExitPlacing();
        }

        // ---------------- ground ring under the piece being placed ----------------
        GameObject placeRing; LineRenderer placeRingLine;

        void UpdatePlaceRing()
        {
            bool show = PlacingMode && Selected != null;
            if (!show) { if (placeRing != null && placeRing.activeSelf) placeRing.SetActive(false); return; }
            if (placeRing == null)
            {
                placeRing = new GameObject("PlaceRing");
                placeRingLine = placeRing.AddComponent<LineRenderer>();
                placeRingLine.loop = true; placeRingLine.useWorldSpace = false; placeRingLine.positionCount = 40; placeRingLine.widthMultiplier = 0.05f;
                for (int i = 0; i < 40; i++) { float a = i / 40f * Mathf.PI * 2f; placeRingLine.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a))); }
                var m = Catalog.Mat("M_Marker"); if (m != null) placeRingLine.sharedMaterial = m;
                placeRingLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (!placeRing.activeSelf) placeRing.SetActive(true);
            var s = VillageGrid.Size(Selected.Def.footprint, pendingRot);
            float r = (Mathf.Max(s.x, s.y) * 0.5f + 0.25f) * Grid.Cell * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 6f));
            placeRing.transform.position = Grid.CenterOf(Selected.Def.footprint, pendingX, pendingY, pendingRot) + Vector3.up * 0.03f;
            placeRing.transform.localScale = new Vector3(r, 1f, r);
            var c = PendingValid ? new Color(0.45f, 0.9f, 0.6f, 1f) : new Color(1f, 0.45f, 0.45f, 1f);
            placeRingLine.startColor = placeRingLine.endColor = c;
        }

        /// <summary>Glides the park view over to a spot (the new piece, a friend) without leaving the park bounds.</summary>
        public void LookAt(Vector3 world)
        {
            Orbit.target = new Vector3(Mathf.Clamp(world.x, ParkRect.x, ParkRect.z), Orbit.target.y, Mathf.Clamp(world.z, ParkRect.y, ParkRect.w));
        }

        // ---------------- keyboard: ESC leaves whatever is open ----------------
        bool HandleEscape()
        {
            var kb = Keyboard.current; if (kb == null || !kb.escapeKey.wasPressedThisFrame) return false;
            if (HousePlacing) { HouseLater(); return true; }
            if (ClawOpen) { CloseClaw(); return true; }
            if (PlacingMode) { ExitPlacing(); return true; }
            if (FriendDialogOpen) { OfferLater(talking); return true; }
            if (QuestBoardOpen) { CloseQuestBoard(); return true; }
            if (Shop != null && Shop.Visible) { CloseShop(); return true; }
            if (Storage != null && Storage.Visible) { CloseStorage(); return true; }
            if (DressOpen) { CloseDress(); return true; }
            if (Mongle.Views.AlbumView.IsOpen) { Mongle.Views.AlbumView.Close(); return true; }
            return false;
        }

        FurnitureObject PickFurniture(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 30f);
            foreach (var h in hits.OrderBy(h => h.distance)) { var f = h.collider.GetComponentInParent<FurnitureObject>(); if (f != null) return f; }
            return null;
        }

        void UpdatePlacing(PointerFrame f)
        {
            if (f.pressed)
            {
                var pick = PickFurniture(f.position);
                if (pick != null)
                {
                    if (pick.InUse) { root.Say("몽이가 쓰고 있어요. 조금 이따 옮겨요"); root.Audio.Play("wrong", 0.4f); }
                    else
                    {
                        if (Selected != pick) { RestoreSelectedIfInvalid(); Select(pick); }
                        dragging = true; Orbit.allowRotate = false; root.Audio.Play("grab", 0.5f);
                    }
                }
            }
            if (dragging && f.held && Selected != null)
            {
                var ray = cam.ScreenPointToRay(f.position);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d))
                {
                    var cell = Grid.SnapOrigin(Selected.Def.footprint, pendingRot, ray.GetPoint(d));
                    if (cell.x != pendingX || cell.y != pendingY) { pendingX = cell.x; pendingY = cell.y; Preview(); root.Audio.Play("click", 0.15f, 1.5f); }
                }
            }
            if (f.released) { dragging = false; Orbit.allowRotate = true; }
        }

        void Select(FurnitureObject f)
        {
            if (Selected != null && Selected != f) Selected.SetTint(null);
            Selected = f;
            pendingOriginal = new FurniturePlacement { furnitureId = f.Placement.furnitureId, cellX = f.Placement.cellX, cellY = f.Placement.cellY, rotation = f.Placement.rotation };
            pendingX = f.Placement.cellX; pendingY = f.Placement.cellY; pendingRot = f.Placement.rotation;
            Preview();
        }

        IEnumerable<(Vector2Int, FurniturePlacement)> OthersOf(FurnitureObject me) =>
            Furniture.Where(x => x != me).Select(x => (x.Def.footprint, x.Placement));

        void Preview()
        {
            if (Selected == null) return;
            PendingValid = Grid.IsValid(OthersOf(Selected), Selected.Def.footprint, pendingX, pendingY, pendingRot);
            Selected.Apply(Grid, Mathf.Clamp(pendingX, Grid.MinX, Grid.MaxX - 1), Mathf.Clamp(pendingY, Grid.MinY, Grid.MaxY - 1), pendingRot);
            Selected.SetTint(PendingValid ? new Color(0.65f, 1f, 0.7f) : new Color(1f, 0.55f, 0.55f));
        }

        void RotateSelected()
        {
            if (Selected == null) { root.Say("먼저 옮길 가구를 눌러요"); return; }
            pendingRot = (pendingRot + 1) & 3;
            var s = VillageGrid.Size(Selected.Def.footprint, pendingRot);
            pendingX = Mathf.Clamp(pendingX, Grid.MinX, Mathf.Max(Grid.MinX, Grid.MaxX - s.x)); pendingY = Mathf.Clamp(pendingY, Grid.MinY, Mathf.Max(Grid.MinY, Grid.MaxY - s.y));
            Preview(); root.Audio.Play("click", 0.5f, 1.2f);
        }

        void RestoreSelectedIfInvalid()
        {
            if (Selected == null) return;
            // moving on to another piece discards an unconfirmed edit of this one
            if (Selected.IsNew) { DiscardNew(Selected, "보관함으로 돌아갔어요"); return; }
            Selected.Apply(Grid); Selected.SetTint(null); Selected = null;
        }

        public bool ConfirmPlacement()
        {
            if (Selected == null) { root.Say("먼저 옮길 가구를 눌러요"); return false; }
            if (!PendingValid) { root.Say("여기에는 둘 수 없어요"); root.Audio.Play("wrong", 0.5f); return false; }
            if (Selected.InUse) { root.Say("몽이가 쓰고 있어요"); return false; }
            var f = Selected;
            f.Placement.cellX = pendingX; f.Placement.cellY = pendingY; f.Placement.rotation = pendingRot;
            f.Apply(Grid); f.SetTint(null);
            if (f.IsNew)
            {
                if (!World.TakeFromInventory(f.Def.id)) { DiscardNew(f, "보관함에 없는 물건이에요"); return false; }
                state.Furniture.Add(new FurniturePlacement { furnitureId = f.Def.id, instanceId = f.Placement.Id, cellX = pendingX, cellY = pendingY, rotation = pendingRot });
                f.IsNew = false; f.Pending = false;
                World.Report("ItemPlaced", f.Def.id);
                if (f.Def.IsPlant) { World.EnsurePlant(f.Placement.Id, f.Def.id); f.Plant.Refresh(false); }
                f.Wobble();
            }
            else
            {
                var entry = state.Furniture.FirstOrDefault(x => x.Id == f.Placement.Id);
                if (entry != null) { entry.cellX = pendingX; entry.cellY = pendingY; entry.rotation = pendingRot; }
                else state.Furniture.Add(new FurniturePlacement { furnitureId = f.Placement.furnitureId, instanceId = f.Placement.Id, cellX = pendingX, cellY = pendingY, rotation = pendingRot });
            }
            root.Persist("furniture");
            Agent.OnFurnitureChanged(f);
            root.Audio.Play("success", 0.5f);
            root.Say("가구를 놓았어요");
            Selected = null;
            return true;
        }

        public void CancelPlacement()
        {
            if (Selected != null && Selected.IsNew) DiscardNew(Selected, "보관함으로 돌아갔어요");
            else if (Selected != null) { Selected.Apply(Grid); Selected.SetTint(null); Selected = null; }
            root.Audio.Play("click", 0.4f, 0.8f);
        }

        public void ExitPlacing() { CancelPlacement(); SetPlacing(false); }

        // test hooks
        public void QaBeginPlacing() { if (!PlacingMode) SetPlacing(true); }
        public bool QaMove(string furnitureId, int cx, int cy, int rot)
        {
            var f = Furniture.FirstOrDefault(x => x.Def.id == furnitureId); if (f == null) return false;
            if (f.InUse) return false;
            Select(f); pendingX = cx; pendingY = cy; pendingRot = rot; Preview();
            return ConfirmPlacement();
        }
    }
}
