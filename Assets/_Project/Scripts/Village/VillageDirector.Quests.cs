using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.Village
{
    /// <summary>
    /// Friends' requests and questions (D-011): "!" and "?" bubbles over moved-in friends (and Mongi), the dialog in the friend's
    /// voice, the 퀘스트 window, shaking candy trees for fruit, the star hunt, riding together, dressing up, watering, and the
    /// player's answers coming back in later chats (birthday wishes on the day).
    /// </summary>
    public partial class VillageDirector
    {
        public const float HuntSeconds = 120f;
        public const int BirthdayGift = 30, MaxFruitLying = 12;
        FriendQuestBook Book => World.FriendQuests;
        PlayerProfile Profile => World.Profile;
        public QuestBoard QuestWindow { get; private set; }
        public FriendDialog Dialog { get; private set; }
        public bool QuestBoardOpen => QuestWindow != null && QuestWindow.Visible;
        public bool FriendDialogOpen => Dialog != null && Dialog.Visible;
        public bool QuestUiOpen => QuestBoardOpen || FriendDialogOpen;
        public readonly List<FruitTree> FruitTrees = new List<FruitTree>();
        Button questButton; GameObject questBadge; Text questBadgeText; int questNews, boardTab;
        RectTransform markLayer; readonly Dictionary<FriendOffer, QuestMark> marks = new Dictionary<FriendOffer, QuestMark>();
        FriendOffer talking; float offerTick, boardTick; bool fruitHintShown;
        readonly System.Random chatRng = new System.Random();
        static string birthdayGreeted; // the day the village last cheered (once per day and run)

        class QuestMark { public GameObject go; public RectTransform rt; public float phase; }

        // ---------------- setup ----------------
        /// <summary>
        /// The 퀘스트 button at the top left (D-026): the half-clear cream pill of the notices, no stitch, "새 N" beside it.
        /// The tracker of what is under way hangs under it in the same pill; both open the quest window.
        /// </summary>
        void BuildQuestButton()
        {
            questButton = UiKit.SpriteButton(hud.transform, "Btn_퀘스트", "hud_toast", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -65f), new Vector2(190f, 76f), ToggleQuestBoard);   // centred on the top bar's line (54..151)
            UiKit.Slice(questButton.GetComponent<Image>(), true);
            UiKit.TitleLabel(questButton.transform, "퀘스트", 34, UiKit.FigBrown, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(170f, 60f));
            var badge = UiKit.Panel(questButton.transform, "NewBadge", UiKit.Pink, new Vector2(1f, 0.5f), new Vector2(46, 0), new Vector2(80, 40), false);
            questBadge = badge.gameObject; questBadgeText = UiKit.TitleLabel(badge.transform, "", 22, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), new Vector2(76, 36));
            questBadge.SetActive(false);
        }

        /// <summary>The friends' quest marks layer, the star hunt pill, the speech bubble with choices and the quest window.</summary>
        void BuildQuestUi()
        {
            markLayer = UiKit.Rect(canvas.transform, "QuestMarks", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            markLayer.SetAsFirstSibling(); // under every panel
            BuildHuntHud();
            Dialog = new FriendDialog(canvas.transform) { Skin = SkinPart };
            QuestWindow = new QuestBoard(canvas.transform) { OnClose = CloseQuestBoard, OnTab = t => { boardTab = t; RefreshQuestBoard(); root.Audio.Play("click", 0.4f, 1.2f); }, Skin = SkinPart };
            SkinPart(huntHud);
        }

        void SetupFriendQuests()
        {
            QuestProps.Begin();
            foreach (var po in FindObjectsByType<ParkObstacle>(FindObjectsInactive.Exclude))
                if (po.name == "CandyTree") { var t = FruitTree.Ensure(po.gameObject); if (t != null && !FruitTrees.Contains(t)) FruitTrees.Add(t); }
            foreach (var t in FindObjectsByType<FruitTree>(FindObjectsInactive.Exclude)) if (!FruitTrees.Contains(t)) FruitTrees.Add(t);
            BuildQuestUi();
            Book.QuestChanged += OnFriendQuestChanged;
            Book.HoldOffers(Book.FirstDelay);
            if (state.Admitted) GreetBirthday();
        }

        void UnhookFriendQuests() { if (root != null) root.World.FriendQuests.QuestChanged -= OnFriendQuestChanged; }

        // ---------------- who is who ----------------
        public string GiverOf(ToyAgent a) => a == null || a.Actor == null ? null : a.IsMain ? (Catalog.toy != null ? Catalog.toy.kindId : a.Actor.name) : a.Actor.name;
        static Personality PersonalityOf(ToyAgent a) => a != null && a.Selector != null ? a.Selector.Personality : Personality.Gentle;
        public ToyAgent AgentOf(string giver) => string.IsNullOrEmpty(giver) ? null : AllAgents().FirstOrDefault(a => a.Actor != null && GiverOf(a) == giver);
        string GiverName(string giver) { var d = Catalog.ToyDef(giver); return d != null ? d.displayName : giver; }
        Ride RideById(string id) => Ride.All.FirstOrDefault(r => r != null && r.rideId == id);
        string RideName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var r = RideById(id); if (r != null) return r.displayName;
            return id == "Ride_Carousel" ? "회전목마" : id == "Ride_Wheel" ? "관람차" : id == "Ride_Balloon" ? "열기구" : id;
        }

        // ---------------- every frame ----------------
        void UpdateFriendQuests()
        {
            if (QuestWindow == null) return;
            TickOffers();
            SyncMarks();
            PollRideQuests();
            UpdateHunt();
            if (questBadge != null && questBadge.activeInHierarchy) questBadge.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 5f));
            if (QuestBoardOpen && hunt != null && (boardTick -= Time.unscaledDeltaTime) <= 0f) { boardTick = 0.5f; RefreshQuestBoard(); } // live hunt timer
        }

        void TickOffers()
        {
            if ((offerTick -= Time.deltaTime) > 0f) return;
            offerTick = 1f;
            if (!state.Admitted || QaDriver.Instance != null) return; // the scripted QA run keeps its old expectations
            if (FriendDialogOpen) return; // the bubble being answered must not fade under the player's hand
            var givers = AllAgents().Where(a => a.Actor != null && a.State != AgentState.Disabled && !a.OnRide && !a.AtHome && a.Riding == null)
                .Select(a => new OfferCandidate(GiverOf(a), PersonalityOf(a))).ToList();
            var o = Book.Tick(givers, OfferContextNow());
            if (o != null) root.Audio.Play("pop", 0.3f, 1.5f);
        }

        OfferContext OfferContextNow() => new OfferContext
        {
            trees = FruitTrees.Count > 0,
            rides = Ride.All.Where(r => r != null && r.isActiveAndEnabled).Select(r => r.rideId).Distinct().ToArray(),
            waterLeft = Furniture.Where(f => f != null && !f.Pending && f.Def.IsPlant).Sum(WatersLeft),
            hasAccessories = Catalog.AllAccessories.Any(a => World.Owned(a.id) > 0) || World.Coins >= 20,
            questions = Profile.Unasked(FriendQuestions.Keys).ToArray(),
        };

        int WatersLeft(FurnitureObject f)
        {
            var w = f.Def.stageWaters; if (w == null || w.Length == 0) return 0;
            var p = World.Plant(f.Placement.Id);
            return Mathf.Max(0, w[w.Length - 1] - (p != null ? p.waters : 0));
        }

        // ---------------- "!" / "?" bubbles over the heads ----------------
        void SyncMarks()
        {
            foreach (var gone in marks.Keys.Where(o => !Book.Offers.Contains(o)).ToList()) { Destroy(marks[gone].go); marks.Remove(gone); }
            foreach (var o in Book.Offers) if (!marks.ContainsKey(o)) marks[o] = MakeMark(o);
            bool hideAll = !state.Admitted || PlacingMode || ViewingRide != null || AnyPanelOpen || QuestUiOpen || root.Transitioning;
            float scale = canvas.scaleFactor;
            foreach (var kv in marks)
            {
                var m = kv.Value; var a = AgentOf(kv.Key.giver);
                bool show = !hideAll && a != null && !a.OnRide && !a.AtHome && a.State != AgentState.Disabled;
                Vector3 sp = default;
                if (show)
                {
                    var head = a.Actor.Node("Head") ?? a.transform;
                    if (bubble.activeSelf && bubbleAnchor == head) show = false; // the toy is talking: its speech bubble is up there
                    // the tail tip sits just above-right of the head, as if the toy were saying it (D-023)
                    sp = cam.WorldToScreenPoint(head.position + Vector3.up * 0.09f + cam.transform.right * 0.05f);
                    if (sp.z <= 0.1f || sp.x < -80f || sp.x > Screen.width + 80f || sp.y < -80f || sp.y > Screen.height + 80f) show = false;
                }
                if (m.go.activeSelf != show) m.go.SetActive(show);
                if (!show) continue;
                float t = Time.unscaledTime + m.phase, k = Mathf.Clamp(13f / Mathf.Max(1f, sp.z), 0.75f, 1.2f);
                m.rt.anchoredPosition = new Vector2(sp.x, sp.y) / scale + new Vector2(4f * k, 6f * k + Mathf.Abs(Mathf.Sin(t * 2.6f)) * 5f);
                m.rt.localScale = Vector3.one * k * (1f + 0.04f * Mathf.Sin(t * 5.2f));   // the pivot is the tail tip, so it bobs from the mouth
            }
        }

        QuestMark MakeMark(FriendOffer o)
        {
            bool question = o.type == OfferType.Question;
            var col = question ? new Color(0.45f, 0.68f, 0.98f, 1f) : new Color(1f, 0.6f, 0.36f, 1f);
            // Figma D-020: round speech bubbles with a little tail ("!" orange, "?" blue)
            // pivot = the tail tip (bottom-left of the Figma bubble), so the bubble grows out of the toy's head
            var ring = UiKit.SpriteImage(markLayer, "QuestMark_" + o.giver, question ? "shop_bub_q" : "shop_bub_excl", Vector2.zero, new Vector2(0.05f, 0.07f), Vector2.zero, new Vector2(100, 100), true);
            UiKit.TitleLabel(ring.transform, question ? "?" : "!", 62, UiKit.FigBrown2, new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(90, 90));
            var b = ring.gameObject.AddComponent<Button>(); b.targetGraphic = ring;
            b.onClick.AddListener(() => OpenOffer(o));
            return new QuestMark { go = ring.gameObject, rt = ring.rectTransform, phase = Random.value * 6f };
        }

        /// <summary>Clicking a toy with a bubble opens its request first (true when it did).</summary>
        bool OpenOfferOf(ToyAgent a) { var o = Book.OfferOf(GiverOf(a)); return o != null && OpenOffer(o); }

        // ---------------- the friend talks ----------------
        public bool OpenOffer(FriendOffer o)
        {
            if (o == null || !Book.Offers.Contains(o) || PlacingMode || AnyPanelOpen || ViewingRide != null) return false;
            var a = AgentOf(o.giver); if (a == null || a.OnRide) return false;
            CloseDress(); CloseQuestBoard(); album.Hide();
            talking = o;
            var p = PersonalityOf(a); var hey = FriendQuestText.Greeting(p, o.type);
            if (!a.TryTalk(hey)) ShowBubble(a.Actor, hey, 2f);
            LookAt(a.transform.position);
            root.Audio.Play("hello", 0.7f, 1.15f);
            if (o.type == OfferType.Quest) ShowRequest(o, a);
            else ShowQuestion(o, a);
            return true;
        }

        void ShowRequest(FriendOffer o, ToyAgent a)
        {
            Dialog.Show(NameOf(a), TagColorOf(a), FriendQuestText.Request(o.kind, PersonalityOf(a), o.target, RideName(o.param), o.giver),
                "· " + FriendQuestText.Title(o.kind) + "      · 보상: 몽글 코인 +" + o.reward, null, 0,
                new[] { new FriendDialog.Choice("좋아, 할게!", UiKit.Peach, () => AcceptOffer(o)), new FriendDialog.Choice("나중에", UiKit.Mint, () => OfferLater(o)) });
        }

        /// <summary>Each friend's own pastel for its name tag (D-025).</summary>
        static readonly System.Collections.Generic.Dictionary<string, Color> TagColors = new System.Collections.Generic.Dictionary<string, Color>
        {
            { "TeddyBear_001", new Color(1f, 0.85f, 0.72f) },  // 몽이: honey peach
            { "Bunny_001", new Color(1f, 0.8f, 0.88f) },       // pink
            { "Cat_001", new Color(0.88f, 0.82f, 1f) },        // lavender
            { "Dino_001", new Color(0.76f, 0.93f, 0.8f) },     // mint
            { "Bee_001", new Color(1f, 0.92f, 0.6f) },         // butter
            { "Frog_001", new Color(0.82f, 0.95f, 0.66f) },    // lime
            { "Robot_001", new Color(0.78f, 0.89f, 1f) },      // sky
            { "Car_001", new Color(1f, 0.79f, 0.76f) },        // coral
            { "Plane_001", new Color(0.72f, 0.93f, 0.95f) },   // aqua
            { "FireTruck_001", new Color(1f, 0.73f, 0.71f) },  // soft red
            { "Pawn_001", new Color(0.95f, 0.86f, 0.74f) },    // wood
        };
        Color TagColorOf(ToyAgent a)
        {
            string kind = a == null || a.Actor == null ? "" : a.IsMain ? "TeddyBear_001" : a.Actor.name;
            return TagColors.TryGetValue(kind, out var c) ? c : new Color(1f, 0.85f, 0.9f);
        }

        /// <summary>A colour answer is drawn in that colour (분홍색 on pink, 하늘색 on sky ...).</summary>
        static readonly System.Collections.Generic.Dictionary<string, Color> AnswerColors = new System.Collections.Generic.Dictionary<string, Color>
        {
            { "분홍색", new Color(1f, 0.8f, 0.86f) }, { "하늘색", new Color(0.76f, 0.89f, 1f) }, { "노란색", new Color(1f, 0.92f, 0.6f) },
            { "민트색", new Color(0.76f, 0.94f, 0.86f) }, { "보라색", new Color(0.87f, 0.81f, 1f) }, { "빨간색", new Color(1f, 0.7f, 0.7f) },
        };

        static readonly Color[] ChoiceColors = { new Color(1f, 0.8f, 0.86f), new Color(1f, 0.9f, 0.7f), new Color(0.82f, 0.92f, 1f), new Color(0.8f, 0.94f, 0.86f), new Color(0.9f, 0.85f, 1f), new Color(1f, 0.86f, 0.78f) };

        void ShowQuestion(FriendOffer o, ToyAgent a)
        {
            var fq = FriendQuestions.Get(o.question);
            if (fq == null || Profile.Has(o.question)) { Book.Withdraw(o); Dialog.Hide(); talking = null; return; }
            var ask = FriendQuestText.Ask(fq, PersonalityOf(a));
            if (fq.key == PlayerProfile.Birthday) { ShowMonths(o, a, ask); return; }
            var choices = fq.choices.Select((c, i) => new FriendDialog.Choice(c, AnswerColors.TryGetValue(c, out var ac) ? ac : ChoiceColors[i % ChoiceColors.Length], () => AnswerQuestion(o, c))).ToList();
            Dialog.Show(NameOf(a), TagColorOf(a), ask, "· " + fq.label, choices, choices.Count <= 4 ? choices.Count : 3, new[] { new FriendDialog.Choice("나중에", UiKit.Mint, () => OfferLater(o)) });
        }

        void ShowMonths(FriendOffer o, ToyAgent a, string ask)
        {
            var months = Enumerable.Range(1, 12).Select(m => new FriendDialog.Choice(m + "월", ChoiceColors[(m - 1) % ChoiceColors.Length], () => ShowDays(o, a, ask, m))).ToList();
            Dialog.Show(NameOf(a), TagColorOf(a), ask, "· 생일 · 몇 월이야?", months, 6, new[] { new FriendDialog.Choice("나중에", UiKit.Mint, () => OfferLater(o)) });
        }

        void ShowDays(FriendOffer o, ToyAgent a, string ask, int month)
        {
            root.Audio.Play("click", 0.5f, 1.2f);
            var days = Enumerable.Range(1, PlayerProfile.DaysIn(month)).Select(d => new FriendDialog.Choice(d + "일", ChoiceColors[(month - 1) % ChoiceColors.Length], () => AnswerQuestion(o, PlayerProfile.BirthdayValue(month, d)))).ToList();
            Dialog.Show(NameOf(a), TagColorOf(a), ask, "· 생일 · " + month + "월 며칠이야?", days, 8,
                new[] { new FriendDialog.Choice("← 월 다시", UiKit.Sky, () => ShowMonths(o, a, ask)), new FriendDialog.Choice("나중에", UiKit.Mint, () => OfferLater(o)) });
        }

        /// <summary>"나중에": the bubble stays up for a while, the friend says it will wait.</summary>
        public void OfferLater(FriendOffer o)
        {
            Dialog.Hide(); talking = null;
            var a = o != null ? AgentOf(o.giver) : null;
            if (a != null) ShowBubble(a.Actor, FriendQuestText.LaterLine(PersonalityOf(a)), 2.4f);
            root.Audio.Play("click", 0.4f, 0.9f);
        }

        /// <summary>ESC, the placing bar, a shop: the dialog closes like "나중에" and the 퀘스트 window closes.</summary>
        void CloseQuestUi()
        {
            if (FriendDialogOpen) OfferLater(talking);
            CloseQuestBoard();
        }

        public FriendQuest AcceptOffer(FriendOffer o)
        {
            Dialog.Hide(); talking = null;
            var q = Book.Accept(o); if (q == null) return null;
            var a = AgentOf(q.giver); string ride = RideName(q.param), name = GiverName(q.giver);
            if (a != null) StartCoroutine(Celebrate(a, FriendQuestText.AcceptLine(q.kind, ride), false));
            switch (q.kind)
            {
                case FriendQuestKind.Fruit:
                    root.Say("퀘스트를 받았어요! 반짝이는 사탕 나무를 눌러서 흔들어 봐요", 4f);
                    foreach (var t in NearestTrees(Orbit.target, 3)) t.Hint(Catalog);
                    break;
                case FriendQuestKind.Stars:
                    StartStarHunt(q);
                    root.Say("반짝 별 " + FriendQuestBook.StarCount + "개를 2분 안에 찾아요! 화면 가장자리의 ★가 방향을 알려 줘요", 4.5f);
                    break;
                case FriendQuestKind.Ride:
                    root.Say("퀘스트를 받았어요! " + Josa.EulReul(ride) + " 누르면 " + Josa.IGa(name) + " 같이 타러 가요", 4f);
                    break;
                case FriendQuestKind.Dress:
                    if (a != null && Catalog.AllAccessories.Any(acc => World.Owned(acc.id) > 0)) { OpenDress(a); root.Say("입혀 주고 싶은 악세서리를 눌러요!", 3.5f); }
                    else root.Say("퀘스트를 받았어요! 컵케이크 악세서리점에서 악세서리를 사 와서 " + Josa.EulReul(name) + " 눌러 입혀 줘요", 4.5f);
                    break;
                default:
                    root.Say("퀘스트를 받았어요! 화분을 눌러서 물을 줘요 (25초마다 줄 수 있어요)", 4f);
                    break;
            }
            return q;
        }

        public bool AnswerQuestion(FriendOffer o, string value)
        {
            Dialog.Hide(); talking = null;
            if (o == null) return false;
            var a = AgentOf(o.giver);
            if (!Profile.Answer(o.question, value, o.giver)) { Book.Withdraw(o); return false; }
            Book.Answered(o);
            var fq = FriendQuestions.Get(o.question);
            var line = fq != null ? FriendQuestText.Reaction(fq, value, o.giver, System.DateTime.Now) : "꼭 기억할게!";
            if (a != null) StartCoroutine(Celebrate(a, line, true));
            root.Audio.Play("chime", 0.7f, 1.15f);
            root.Persist("answer:" + o.question);
            if (o.question == PlayerProfile.Birthday) GiveBirthdayGift();
            if (QuestBoardOpen) RefreshQuestBoard();
            return true;
        }

        /// <summary>The friend turns to the camera and says it; a happy hop and sparkles (just a bubble while riding or sitting).</summary>
        IEnumerator Celebrate(ToyAgent a, string line, bool big)
        {
            if (a == null || a.Actor == null) yield break;
            var at = a.Actor.Point("Head", a.transform.position + Vector3.up * 0.4f) + Vector3.up * 0.12f;
            Fx.Burst(Catalog, at, big ? new Color(1f, 0.82f, 0.9f) : new Color(1f, 0.92f, 0.65f), big ? 28 : 14, 0.018f, big ? 0.4f : 0.28f, 1.1f, -0.03f);
            if (big) root.Audio.Play("sparkle", 0.6f);
            if (a.OnRide || a.Seated || a.State == AgentState.Disabled) { ShowBubble(a.Actor, line, 3.2f); yield break; }
            if (a.TryTalk(line))
            {
                yield return new WaitForSeconds(0.45f); // after it has turned around
                if (a != null && !a.OnRide && !a.Seated) a.Actor.PlayThen("Happy", "Idle");
            }
            else
            {
                ShowBubble(a.Actor, line, 3.2f);
                if (a.State == AgentState.Talk || a.State == AgentState.Idle) a.Actor.PlayThen("Happy", "Idle");
            }
        }

        // ---------------- progress and the reward ----------------
        void OnFriendQuestChanged(FriendQuest q)
        {
            root.Persist("friend-quest:" + q.id);
            RefreshQuestTracker();
            if (QuestBoardOpen) RefreshQuestBoard();
            string name = GiverName(q.giver);
            if (!q.Done)
            {
                if (q.progress == 0) { NoteQuestNews(); return; } // just accepted
                if (q.kind == FriendQuestKind.Fruit) root.Say(name + "에게 줄 열매 " + q.progress + "/" + q.target, 1.8f);
                else if (q.kind == FriendQuestKind.Water) root.Say(name + "의 부탁: 물 주기 " + q.progress + "/" + q.target, 2f);
                return;
            }
            NoteQuestNews();
            var a = AgentOf(q.giver);
            root.Say(name + "의 부탁 완료! 몽글 코인 +" + q.reward, 3.4f);
            root.Audio.Play("success", 0.8f);
            if (a != null) StartCoroutine(Celebrate(a, FriendQuestText.Done(q.kind, PersonalityOf(a)), true));
        }

        void NoteQuestNews()
        {
            if (QuestBoardOpen) return;
            questNews++;
            if (questBadge == null) return;
            questBadge.SetActive(questNews > 0); questBadgeText.text = "새 " + questNews;
        }

        void PollRideQuests()
        {
            foreach (var q in Book.Active.Where(x => x.kind == FriendQuestKind.Ride).ToList())
            {
                var a = AgentOf(q.giver);
                if (a != null && a.OnRide && a.Riding != null && a.Riding.rideId == q.param) World.Report(FriendQuestBook.FriendRode, q.giver + "@" + q.param);
            }
        }

        /// <summary>The ride of an open "같이 타기" request was clicked: that friend goes to ride it and the player rides along (true = handled).</summary>
        bool CallQuestRider(Ride r)
        {
            var q = r == null ? null : Book.Active.FirstOrDefault(x => x.kind == FriendQuestKind.Ride && x.param == r.rideId);
            if (q == null) return false;
            var a = AgentOf(q.giver);
            if (a == null || a.State == AgentState.Disabled || a.Riding == r) return false; // on the way already: the ride's own click
            if (a.OnRide) { ShowBubble(a.Actor, "지금 " + a.Riding.displayName + " 타는 중이야! 내리면 갈게!", 2.6f); return true; }
            if (!a.ForceRide(r)) { root.Say(r.displayName + "에 자리가 없어요. 조금 이따 다시 눌러 봐요", 2.6f); return true; }
            ShowBubble(a.Actor, r.displayName + " 같이 타자!", 2.4f);
            root.Say(Josa.IGa(NameOf(a)) + " " + r.displayName + " 타러 가요! 같이 타요", 3f);
            root.Audio.Play("hello", 0.7f, 1.2f);
            if (r.HasInsideView) EnterRideView(r); else LookAt(r.transform.position);
            return true;
        }

        // ---------------- candy trees ----------------
        IEnumerable<FruitTree> NearestTrees(Vector3 p, int n) => FruitTrees.Where(t => t != null).OrderBy(t => (t.transform.position - p).sqrMagnitude).Take(n);

        public void ShakeTree(FruitTree t)
        {
            int lying = QuestPickup.Live.Count(x => x != null && x.Kind == QuestPickup.Type.Fruit && !x.Collected);
            int n = t.Shake(Catalog, MaxFruitLying - lying);
            root.Audio.Play("pop", 0.55f, 0.75f);
            if (n > 0)
            {
                root.Audio.Play("sparkle", 0.45f, 1.1f);
                if (!fruitHintShown) { fruitHintShown = true; root.Say("열매가 떨어졌어요! 눌러서 주워요", 2.4f); }
            }
            else if (!t.Ready) root.Say("열매가 다시 열리는 중이에요. 다른 나무를 흔들어 봐요", 2.2f);
        }

        void ClickPickup(QuestPickup p)
        {
            if (!p.Collect()) return;
            if (p.Kind == QuestPickup.Type.Star) { StarFound(p); return; }
            bool counts = Book.Active.Any(q => q.kind == FriendQuestKind.Fruit);
            root.Audio.Play("pop", 0.6f, 1.35f);
            Fx.Burst(Catalog, p.transform.position + Vector3.up * 0.12f, p.Tint, 14, 0.016f, 0.25f, 0.7f, -0.02f);
            World.Report(FriendQuestBook.FruitPicked, "");
            if (!counts) root.Say("냠! 달콤한 사탕 열매예요", 1.8f);
        }

        // ---------------- the star hunt ----------------
        class StarHunt { public FriendQuest quest; public readonly List<QuestPickup> stars = new List<QuestPickup>(); public float left; public int found; }
        StarHunt hunt;
        RectTransform huntHud; Text huntText; readonly List<RectTransform> huntArrows = new List<RectTransform>();
        public bool HuntRunning => hunt != null;
        public int HuntStarsLeft => hunt == null ? 0 : hunt.stars.Count - hunt.found;

        void BuildHuntHud()
        {
            // top middle, under the toast; narrow enough to clear the quest tracker on a 4:3 screen
            // D-024: the same pill as the coin notice, just under the notice's place; it hugs its text
            var p = UiKit.Fig(canvas.transform, "hud_toast", UiKit.Edge.Middle, UiKit.Edge.Start, 0f, 106f, false, "StarHunt");
            UiKit.Slice(p, true);
            huntHud = p.rectTransform;
            huntText = UiKit.TitleLabel(p.transform, "", 36, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800f, 70f));
            UiKit.AutoFit(p, huntText, 300f, 110f, 1200f);
            huntHud.gameObject.SetActive(false);
        }

        public bool StartStarHunt(FriendQuest q)
        {
            if (q == null || q.Done || q.kind != FriendQuestKind.Stars) return false;
            EndHunt(false, true);
            hunt = new StarHunt { quest = q, left = HuntSeconds };
            var near = Orbit.target; near.y = 0f;
            var spots = new List<Vector3>();
            for (int i = 0; i < 120 && spots.Count < FriendQuestBook.StarCount; i++)
            {
                float radius = i < 70 ? 9f : 14f; // around what the player is looking at; wider if the park is crowded there
                if (!RandomPoint(near, radius, out var p) || (p - near).sqrMagnitude > radius * radius) continue;
                if (spots.Any(s => (s - p).sqrMagnitude < 2.4f * 2.4f)) continue;
                spots.Add(p);
            }
            foreach (var p in spots)
            {
                var s = QuestProps.Star(Catalog);
                s.transform.position = p + Vector3.up * Random.Range(0.55f, 0.95f);
                hunt.stars.Add(s);
                Fx.Burst(Catalog, s.transform.position, QuestProps.Gold, 10, 0.016f, 0.25f, 0.8f, -0.02f);
            }
            if (hunt.stars.Count == 0) { hunt = null; root.Say("별이 숨을 곳을 못 찾았어요. 조금 이따 다시 해 봐요", 3f); return false; }
            root.Audio.Play("sparkle", 0.7f, 1.2f);
            return true;
        }

        void StarFound(QuestPickup p)
        {
            Fx.Burst(Catalog, p.transform.position, QuestProps.Gold, 24, 0.02f, 0.4f, 0.9f, -0.03f);
            if (hunt == null || !hunt.stars.Contains(p)) return;
            hunt.found++;
            root.Audio.Play("sparkle", 0.7f, 1f + 0.08f * hunt.found);
            if (hunt.found >= hunt.stars.Count)
            {
                var q = hunt.quest; EndHunt(true);
                World.Report(FriendQuestBook.StarsFound, q.giver);
            }
            else root.Say("반짝! 별 " + hunt.found + "/" + hunt.stars.Count, 1.4f);
        }

        void EndHunt(bool success, bool silent = false)
        {
            if (hunt == null) return;
            foreach (var s in hunt.stars) if (s != null && !s.Collected) s.FadeAway();
            var q = hunt.quest; hunt = null;
            if (huntHud != null) huntHud.gameObject.SetActive(false);
            foreach (var a in huntArrows) a.gameObject.SetActive(false);
            if (success || silent) return;
            root.Say("시간이 다 됐어요. '퀘스트'에서 다시 해 봐요!", 3.4f);
            root.Audio.Play("wrong", 0.35f);
            var giver = AgentOf(q.giver); if (giver != null) ShowBubble(giver.Actor, "괜찮아! 다시 찾아보자!", 2.6f);
            if (QuestBoardOpen) RefreshQuestBoard();
        }

        void UpdateHunt()
        {
            if (hunt == null) return;
            if (hunt.quest.Done || !Book.Quests.Contains(hunt.quest)) { EndHunt(false, true); return; }
            bool paused = AnyPanelOpen || PlacingMode || QuestUiOpen || ViewingRide != null || root.Transitioning;
            if (!paused) hunt.left -= Time.deltaTime;
            if (hunt.left <= 0f) { EndHunt(false); return; }
            bool hudOn = ViewingRide == null && !AnyPanelOpen && !QuestBoardOpen;
            if (huntHud.gameObject.activeSelf != hudOn) huntHud.gameObject.SetActive(hudOn);
            int sec = Mathf.CeilToInt(hunt.left);
            huntText.text = "★ 별 찾기 " + hunt.found + "/" + hunt.stars.Count + "   " + sec / 60 + ":" + (sec % 60).ToString("00") + (paused ? "  (멈춤)" : "");
            // off-screen stars: a little ★ at the screen edge in their direction
            float scale = canvas.scaleFactor; var c = new Vector2(Screen.width, Screen.height) * 0.5f; int k = 0;
            foreach (var s in hunt.stars)
            {
                if (s == null || s.Collected || !hudOn || paused) continue;
                var sp = cam.WorldToScreenPoint(s.transform.position);
                bool behind = sp.z < 0f;
                if (!behind && sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height) continue;
                var d = new Vector2(sp.x, sp.y) - c; if (behind) d = -d;
                if (d.sqrMagnitude < 1f) d = Vector2.down;
                float m = 70f * scale, kx = (c.x - m) / Mathf.Max(1e-3f, Mathf.Abs(d.x)), ky = (c.y - m) / Mathf.Max(1e-3f, Mathf.Abs(d.y));
                var arrow = HuntArrow(k++);
                arrow.anchoredPosition = (c + d * Mathf.Min(kx, ky)) / scale;
                arrow.localScale = Vector3.one * (1f + 0.1f * Mathf.Sin(Time.unscaledTime * 6f));
            }
            for (int i = k; i < huntArrows.Count; i++) if (huntArrows[i].gameObject.activeSelf) huntArrows[i].gameObject.SetActive(false);
        }

        RectTransform HuntArrow(int i)
        {
            while (huntArrows.Count <= i)
            {
                var p = UiKit.Panel(markLayer, "StarArrow", new Color(1f, 0.88f, 0.5f, 0.95f), Vector2.zero, Vector2.zero, new Vector2(70, 70), false);
                p.pixelsPerUnitMultiplier = 0.6f;
                UiKit.Label(p.transform, "★", 42, new Color(0.75f, 0.5f, 0.15f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70, 70));
                huntArrows.Add(p.rectTransform);
            }
            var a = huntArrows[i]; if (!a.gameObject.activeSelf) a.gameObject.SetActive(true);
            return a;
        }

        // ---------------- birthday ----------------
        void GreetBirthday()
        {
            var today = System.DateTime.Now;
            if (!Profile.IsBirthdayToday(today) || birthdayGreeted == today.ToString("yyyy-MM-dd")) return;
            birthdayGreeted = today.ToString("yyyy-MM-dd");
            StartCoroutine(BirthdayRoutine());
        }

        IEnumerator BirthdayRoutine()
        {
            yield return new WaitForSeconds(5.5f); // after the welcome hint
            root.Say("오늘은 생일이에요! 마을 친구들이 다 같이 축하해요 ♪", 4f);
            root.Audio.Play("success", 0.7f);
            if (Agent != null) StartCoroutine(Celebrate(Agent, FriendQuestText.BirthdayCheer(PersonalityOf(Agent)), true));
            foreach (var a in ResidentAgents.Where(x => x != null).Take(4))
                Fx.Burst(Catalog, a.Actor.Point("Head", a.transform.position + Vector3.up * 0.4f) + Vector3.up * 0.12f, new Color(1f, 0.8f, 0.9f), 16, 0.018f, 0.3f, 1f, -0.03f);
            yield return new WaitForSeconds(4.2f);
            GiveBirthdayGift();
        }

        void GiveBirthdayGift()
        {
            if (!Profile.TryClaimBirthdayGift(System.DateTime.Now)) return;
            World.Earn(BirthdayGift);
            root.Persist("birthday gift");
            root.Say("친구들의 생일 선물! 몽글 코인 +" + BirthdayGift, 3.6f);
        }

        /// <summary>What a toy says when clicked: birthday wishes on the day, a reminder of its request, something the player told it, or <paramref name="fallback"/>.</summary>
        string ChatLine(ToyAgent a, string fallback)
        {
            var p = PersonalityOf(a);
            if (Profile.IsBirthdayToday(System.DateTime.Now) && chatRng.NextDouble() < 0.6) return FriendQuestText.BirthdayCheer(p);
            var q = Book.ActiveOf(GiverOf(a));
            if (q != null && chatRng.NextDouble() < 0.5) return FriendQuestText.Remind(q, RideName(q.param));
            if (Profile.Count > 0 && chatRng.NextDouble() < 0.45) { var l = FriendQuestText.Remember(Profile, chatRng, System.DateTime.Now); if (l != null) return l; }
            if (chatRng.NextDouble() < 0.3) { var own = FriendQuestText.KindLine(GiverOf(a)); if (own != null) return own; } // what this friend itself loves
            return fallback;
        }

        // ---------------- the 퀘스트 window ----------------
        public void ToggleQuestBoard() { if (QuestBoardOpen) CloseQuestBoard(); else OpenQuestBoard(); }

        public bool OpenQuestBoard(int tab = -1)
        {
            if (PlacingMode) { root.Say("가구 옮기기 중이에요. '나가기'를 누르고 열어요"); return false; }
            if (AnyPanelOpen || ViewingRide != null) return false;
            CloseDress(); album.Hide();
            if (FriendDialogOpen) OfferLater(talking);
            if (tab >= 0) boardTab = tab;
            questNews = 0; if (questBadge != null) questBadge.SetActive(false);
            SetHud(false);
            QuestWindow.Show(); RefreshQuestBoard();
            root.Audio.Play("click", 0.5f);
            return true;
        }

        public void CloseQuestBoard()
        {
            if (!QuestBoardOpen) return;
            QuestWindow.Hide(); SetHud(true);
            root.Audio.Play("click", 0.4f, 0.9f);
        }

        void RefreshQuestBoard()
        {
            if (QuestWindow == null) return;
            var rows = new List<QuestBoard.Row>(); string empty = "";
            var shopActive = World.AllQuests.Where(d => World.StatusOf(d) == QuestStatus.Active || World.StatusOf(d) == QuestStatus.ReadyToClaim).ToList();
            var shopDone = World.AllQuests.Where(d => World.StatusOf(d) == QuestStatus.Done).ToList();
            if (boardTab == 0)
            {
                rows.AddRange(Book.Active.Select(FriendRow));
                rows.AddRange(shopActive.Select(ShopRow));
                empty = "진행 중인 퀘스트가 없어요.\n친구 머리 위에 '!'가 뜨면 눌러 봐요!";
            }
            else if (boardTab == 1)
            {
                rows.AddRange(Book.Finished.Select(FriendRow));
                rows.AddRange(shopDone.Select(ShopRow));
                empty = "아직 끝낸 퀘스트가 없어요.";
            }
            else rows.AddRange(StoryRows());
            QuestWindow.Render(boardTab, rows, empty, new[] { "진행 중 " + (Book.Active.Count() + shopActive.Count), "완료 " + (Book.Finished.Count() + shopDone.Count), "내 이야기" });
        }

        static Color KindColor(FriendQuestKind k) =>
            k == FriendQuestKind.Fruit ? new Color(1f, 0.6f, 0.68f) : k == FriendQuestKind.Stars ? new Color(1f, 0.76f, 0.35f) : k == FriendQuestKind.Ride ? new Color(0.55f, 0.72f, 1f)
            : k == FriendQuestKind.Dress ? new Color(0.78f, 0.62f, 1f) : new Color(0.45f, 0.8f, 0.62f);

        QuestBoard.Row FriendRow(FriendQuest q)
        {
            string name = GiverName(q.giver), ride = RideName(q.param);
            var row = new QuestBoard.Row
            {
                key = q.id, icon = name.Length > 0 ? name.Substring(0, 1) : "!", iconColor = KindColor(q.kind),
                title = name + " · " + FriendQuestText.Title(q.kind), detail = FriendQuestText.Detail(q.kind, q.target, ride, name), reward = "+" + q.reward, done = q.Done,
            };
            if (q.Done)
            {
                row.progress = "완료";
                row.detail = FriendQuestText.Title(q.kind) + " 끝! " + System.DateTimeOffset.FromUnixTimeSeconds(q.doneUnix).ToLocalTime().ToString("M월 d일") + "에 도와줬어요";
                row.reward = "+" + q.reward + " 받음";
                return row;
            }
            switch (q.kind)
            {
                case FriendQuestKind.Fruit:
                    row.progress = "열매 " + q.progress + "/" + q.target;
                    row.action = "나무 보기"; row.onAction = () => { CloseQuestBoard(); var t = NearestTrees(Orbit.target, 1).FirstOrDefault(); if (t != null) { LookAt(t.transform.position); t.Hint(Catalog); } };
                    break;
                case FriendQuestKind.Stars:
                    bool running = hunt != null && hunt.quest == q;
                    row.progress = running ? "별 " + hunt.found + "/" + hunt.stars.Count + " · " + Mathf.CeilToInt(hunt.left) + "초" : "시작 전";
                    if (!running) { row.action = "별 찾기 시작"; row.actionColor = new Color(1f, 0.86f, 0.5f); row.onAction = () => { CloseQuestBoard(); if (StartStarHunt(q)) root.Say("반짝 별을 찾아요! 화면 가장자리의 ★가 방향을 알려 줘요", 3.5f); }; }
                    break;
                case FriendQuestKind.Ride:
                    row.progress = "아직 안 탔어요";
                    row.action = "같이 타기"; row.actionColor = UiKit.Sky;
                    row.onAction = () => { CloseQuestBoard(); var r = RideById(q.param); if (r == null || !CallQuestRider(r)) { if (r != null) LookAt(r.transform.position); root.Say(Josa.IGa(name) + " 벌써 " + ride + " 타러 가고 있어요!", 2.6f); } };
                    break;
                case FriendQuestKind.Dress:
                    row.progress = "입히기 전";
                    row.action = "꾸며주기"; row.actionColor = UiKit.Pink;
                    row.onAction = () =>
                    {
                        CloseQuestBoard(); var a = AgentOf(q.giver); if (a == null) return;
                        LookAt(a.transform.position); OpenDress(a);
                        if (!Catalog.AllAccessories.Any(acc => World.Owned(acc.id) > 0)) root.Say("가진 악세서리가 없어요. 컵케이크 악세서리점에서 사 와요!", 3f);
                    };
                    break;
                default:
                    row.progress = "물 " + q.progress + "/" + q.target;
                    row.action = "화분 보기";
                    row.onAction = () =>
                    {
                        CloseQuestBoard();
                        var f = Furniture.Where(x => x != null && !x.Pending && x.Def.IsPlant && WatersLeft(x) > 0).OrderBy(x => World.SecondsUntilThirsty(World.Plant(x.Placement.Id), x.Def)).FirstOrDefault();
                        if (f != null) { LookAt(f.transform.position); f.Wobble(); } else root.Say("물을 줄 화분이 없어요. 토순 꽃집에서 화분을 사서 놓아요", 3.4f);
                    };
                    break;
            }
            return row;
        }

        QuestBoard.Row ShopRow(QuestDefinition d)
        {
            var st = World.StatusOf(d); var shop = Catalog.Shop(d.giverShopId); string keeper = shop != null ? shop.keeperName : "가게";
            var row = new QuestBoard.Row
            {
                key = d.id, icon = keeper.Substring(0, 1), iconColor = new Color(1f, 0.78f, 0.45f), title = keeper + " · " + d.title,
                detail = st == QuestStatus.Done ? d.doneLine : d.acceptLine, reward = "+" + d.rewardCoins, done = st == QuestStatus.Done,
                progress = st == QuestStatus.Done ? "완료" : st == QuestStatus.ReadyToClaim ? "보상 받기!" : World.ProgressOf(d) + "/" + d.targetCount,
            };
            if (st == QuestStatus.Done) row.reward = "+" + d.rewardCoins + " 받음";
            if (st == QuestStatus.ReadyToClaim)
            {
                var b = Shops.FirstOrDefault(s => s.shopId == d.giverShopId);
                row.action = "가게 가기"; row.actionColor = UiKit.Peach;
                row.onAction = () => { CloseQuestBoard(); OpenShop(b); };
            }
            return row;
        }

        IEnumerable<QuestBoard.Row> StoryRows()
        {
            foreach (var fq in FriendQuestions.All)
            {
                var v = Profile.Get(fq.key);
                string shown = v;
                if (fq.key == PlayerProfile.Birthday && PlayerProfile.ParseBirthday(v, out int m, out int d)) shown = m + "월 " + d + "일";
                var asker = Profile.AskedBy(fq.key);
                yield return new QuestBoard.Row
                {
                    key = fq.key, icon = v != null ? "♥" : "?", iconColor = v != null ? UiKit.Pink : UiKit.Sky, title = fq.label,
                    detail = v != null ? (string.IsNullOrEmpty(asker) ? "친구들이 기억하고 있어요" : Josa.IGa(GiverName(asker)) + " 물어봤고, 친구들이 다 기억하고 있어요")
                        : "아직 말해 주지 않았어요. 친구 머리 위에 '?'가 뜨면 알려 줘요",
                    progress = shown ?? "", reward = "", done = v != null,
                };
            }
        }

        // ---------------- test hooks ----------------
        public FriendOffer QaOffer(string giver, OfferType type, FriendQuestKind kind = FriendQuestKind.Fruit, string question = null)
        {
            var o = new FriendOffer { giver = giver, type = type, kind = kind, target = kind == FriendQuestKind.Fruit ? 3 : 1, reward = 20, question = question, createdUnix = World.Clock(), expiresUnix = World.Clock() + 600 };
            if (kind == FriendQuestKind.Ride) o.param = Ride.All.Select(r => r.rideId).FirstOrDefault();
            Book.Offers.Add(o); return o;
        }
    }
}
