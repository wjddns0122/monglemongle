using System;
using System.Collections;
using System.Linq;
using Mongle.Data;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mongle.Core
{
    /// <summary>
    /// Global services (content, save, input, audio, scene flow). Created once before the first scene loads and kept alive.
    /// It holds no gameplay rules: features own their own state and talk to it through ToyRuntime events.
    /// </summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        /// <summary>Tests may point the save at a temp file before the first scene loads.</summary>
        public static string SavePathOverride;

        public ContentCatalog Catalog { get; private set; }
        public SaveService Saves { get; private set; }
        public ToyRuntime Toy { get; private set; }
        /// <summary>The toy friend being treated at the hospital right now, or null (D-005).</summary>
        public ToyRuntime Patient { get; private set; }
        /// <summary>The toy the repair shop works on: the patient if there is one, otherwise Mongi.</summary>
        public ToyRuntime ActiveToy => Patient ?? Toy;
        /// <summary>Repaired friends living in the village, in moving-in order.</summary>
        public readonly System.Collections.Generic.List<ResidentSave> Residents = new System.Collections.Generic.List<ResidentSave>();
        /// <summary>Kind id of the friend who moved in most recently, until the village has welcomed it (not saved).</summary>
        public string LastMovedIn;
        public PointerInput Pointer { get; private set; }
        public Sfx Audio { get; private set; }
        public SaveLoadResult LoadResult { get; private set; }
        public SettingsSave Settings = new SettingsSave();
        public Mongle.Village.WorldState World { get; } = new Mongle.Village.WorldState();
        public bool Transitioning { get; private set; }
        /// <summary>True when the save file could not be read and the player must choose what happens.</summary>
        public bool NeedsSaveDecision => Toy == null;

        Image fade; Text toast; float toastUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate()
        {
            if (Instance != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Catalog = ContentCatalog.Load();
            if (Catalog != null) World.SetQuestDefinitions(Catalog.quests);
            if (Catalog == null) Debug.LogError("[GameRoot] ContentCatalog missing in Resources.");
            string path = SavePathOverride;
#if UNITY_EDITOR
            // "Mongle/Play From Start (new save)" plays from the title with a throwaway save; statics do not survive the play-mode reload
            var fresh = UnityEditor.SessionState.GetString("mongle.playFreshSave", "");
            if (!string.IsNullOrEmpty(fresh)) path = fresh;
#endif
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-mongleSave") path = args[i + 1];
            Saves = string.IsNullOrEmpty(path) ? SaveService.CreateDefault() : new SaveService(path);
            Pointer = gameObject.AddComponent<PointerInput>();
            Audio = gameObject.AddComponent<Sfx>();
            GameSettings.Apply();   // language, quality, screen, brightness, audio (D-038)
            BuildOverlay();
            Boot();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------- boot / load ----------
        public void Boot()
        {
            LoadResult = Saves.Load();
            switch (LoadResult.status)
            {
                case SaveStatus.Ok:
                    SetToy(SaveMapper.FromSave(LoadResult.data, Catalog.toy.kindId, DamageIds()));
                    SaveMapper.ApplyWorld(LoadResult.data, World);
                    Residents.Clear(); Residents.AddRange(SaveMapper.ReadResidents(LoadResult.data, k => Catalog.ToyDef(k) != null && k != Catalog.toy.kindId));
                    Mongle.Village.HouseLots.AssignLooks(Residents); // a house look for every friend; where it stands the player chooses (D-015)
                    var pd = LoadResult.data.patient != null ? Catalog.ToyDef(LoadResult.data.patient.toyKindId) : null;
                    SetPatient(pd != null && Residents.All(r => r.kindId != pd.kindId) ? SaveMapper.FromPatientSave(LoadResult.data.patient, DamageIds(pd)) : null);
                    if (Patient != null && Patient.PhotoConfirmed) MovePatientIn(); // quit right after the photo: finish moving in
                    if (LoadResult.data.saveVersion < 5) MigrateCupcakeHouse();
                    Settings = LoadResult.data.settings ?? new SettingsSave();
                    GameSettings.AdoptLegacyVolume(Settings.masterVolume); GameSettings.ApplyAudio();
                    break;
                case SaveStatus.None:
                    World.Reset(); Residents.Clear(); SetPatient(null);
                    SetToy(NewToy());
                    break;
                default:
                    SetToy(null); // player must decide; never overwritten silently
                    Debug.LogWarning("[Save] " + LoadResult.status + ": " + LoadResult.message);
                    break;
            }
        }

        /// <summary>v5: the cupcake house is the accessory shop's building, no longer furniture. Pieces bought earlier are refunded.</summary>
        void MigrateCupcakeHouse()
        {
            const string id = "Fur_Cake"; const int price = 70;
            int n = Toy != null ? Toy.Furniture.RemoveAll(f => f.furnitureId == id) : 0;
            while (World.TakeFromInventory(id)) n++;
            if (n == 0) return;
            World.Earn(price * n);
            Persist("v5: cupcake house became the accessory shop");
            Say("컵케이크 집은 이제 악세서리점이 됐어요! 몽글 코인 +" + price * n, 4f);
        }

        string[] DamageIds() => DamageIds(Catalog.toy);
        static string[] DamageIds(ToyDefinition def) => def.damageSequence.Where(d => d != null).Select(d => d.id).ToArray();
        ToyRuntime NewToy()
        {
            var t = new ToyRuntime(Catalog.toy.kindId, DamageIds());
            t.EnsureResidentId(Guid.NewGuid().ToString("N"));
            return t;
        }

        /// <summary>Start a new game. If a readable-but-unusable save exists it is moved aside first, never deleted.</summary>
        public void StartNewGame(bool quarantineExisting)
        {
            if (quarantineExisting) { var moved = Saves.QuarantineCurrent(); if (moved != null) Debug.Log("[Save] moved unreadable save to " + moved); }
            World.Reset(); Residents.Clear(); SetPatient(null);
            SetToy(NewToy());
        }

        // ---------- hospital friends (D-005) ----------
        /// <summary>A friend who has not moved in yet, picked at random (D-005, user request), or null when everyone lives in the village.</summary>
        public ToyDefinition NextFriend()
        {
            if (Catalog.friends == null) return null;
            var waiting = Catalog.friends.Where(f => f != null && f.prefab != null && Residents.All(r => r.kindId != f.kindId) && (Patient == null || Patient.KindId != f.kindId)).ToList();
            return waiting.Count == 0 ? null : waiting[UnityEngine.Random.Range(0, waiting.Count)];
        }

        /// <summary>A new friend arrives at the hospital (only one patient at a time, only after Mongi has moved in).</summary>
        public bool AdmitPatient(ToyDefinition def)
        {
            if (def == null || Patient != null || Toy == null || !Toy.Admitted) return false;
            if (Residents.Any(r => r.kindId == def.kindId)) return false;
            var p = new ToyRuntime(def.kindId, DamageIds(def));
            p.EnsureResidentId(Guid.NewGuid().ToString("N"));
            SetPatient(p);
            Persist("patient:" + def.kindId);
            return true;
        }

        /// <summary>
        /// "죄송합니다" at the door (D-024): a friend that was not looked at yet goes home; it may come again another time.
        /// Mongi and a friend whose repair has begun cannot be turned away.
        /// </summary>
        public bool DeclinePatient()
        {
            var p = Patient;
            if (p == null || p.Damages.Any(d => d.status != Mongle.Data.DamageStatus.Hidden)) return false;
            SetPatient(null);
            Persist("declined:" + p.KindId);
            return true;
        }

        /// <summary>The repaired patient (dressed and photographed) moves into the village and becomes a resident.</summary>
        public bool MovePatientIn()
        {
            var p = Patient;
            if (p == null || !p.CustomizationConfirmed || Residents.Any(r => r.kindId == p.KindId)) { if (p != null && Residents.Any(r => r.kindId == p.KindId)) SetPatient(null); return false; }
            var r = new ResidentSave { kindId = p.KindId, residentId = p.ResidentId, movedIn = DateTime.Now.ToString("yyyy-MM-dd") };
            foreach (var kv in p.Equipped) r.equipped.Add(new AccessorySave { slot = kv.Key.ToString(), accessoryId = kv.Value });
            r.photo = SaveMapper.ToPhotoSave(p.Photo); // the friend's first photo goes into the album
            Residents.Add(r); LastMovedIn = r.kindId;
            Mongle.Village.HouseLots.AssignLooks(Residents); // the newcomer's house look; the player picks its place (D-015)
            SetPatient(null);
            Persist("moved in:" + r.kindId);
            return true;
        }

        void SetPatient(ToyRuntime p)
        {
            if (Patient != null) Unhook(Patient);
            Patient = p;
            if (Patient != null) Hook(Patient);
        }

        void SetToy(ToyRuntime t)
        {
            if (Toy != null) Unhook(Toy);
            Toy = t;
            if (Toy != null) Hook(Toy);
        }

        void Hook(ToyRuntime t)
        {
            t.DiagnosisCompleted += OnCheckpoint;
            t.DamageCompleted += OnDamage;
            t.StampCompleted += OnStamp;
            t.CustomizationConfirmedEvent += OnCheckpoint;
            t.PhotoConfirmedEvent += OnPhoto;
            t.ToyAdmitted += OnCheckpoint;
        }
        void Unhook(ToyRuntime t)
        {
            t.DiagnosisCompleted -= OnCheckpoint;
            t.DamageCompleted -= OnDamage;
            t.StampCompleted -= OnStamp;
            t.CustomizationConfirmedEvent -= OnCheckpoint;
            t.PhotoConfirmedEvent -= OnPhoto;
            t.ToyAdmitted -= OnCheckpoint;
        }
        void OnCheckpoint() => Persist("checkpoint");
        void OnDamage(string id)
        {
            var reward = Catalog.Damage(id)?.rewardCoins ?? 0;
            if (reward > 0) { World.Earn(reward); Say("몽글 코인 +" + reward); }
            Persist("damage:" + id);
        }
        void OnStamp()
        {
            var def = Catalog.ToyDef(ActiveToy?.KindId) ?? Catalog.toy;
            int reward = def.stampRewardCoins;
            if (reward > 0) { World.Earn(reward); Say("몽글 코인 +" + reward); }
            Persist("stamp");
        }
        void OnPhoto(PhotoRecord p) => Persist("photo");

        public void Persist(string reason)
        {
            if (Toy == null) return;
            try
            {
                var data = SaveMapper.ToSave(Toy, Settings, World);
                SaveMapper.WriteFriends(data, Patient, Residents);
                Saves.Save(data); Debug.Log("[Save] ok (" + reason + ")");
            }
            catch (Exception e) { Debug.LogError("[Save] failed (" + reason + "): " + e.Message); Say("저장하지 못했어요."); }
        }

        // ---------- scene flow ----------
        public FlowPhase CurrentPhase => Patient != null ? FlowResolver.ResolvePatient(Patient) : FlowResolver.Resolve(Toy);

        public void GoToCurrentPhase() => GoToScene(FlowResolver.SceneFor(CurrentPhase));

        public void GoToScene(string scene)
        {
            if (Transitioning) return;
            StartCoroutine(LoadRoutine(scene));
        }

        IEnumerator LoadRoutine(string scene)
        {
            Transitioning = true;
            yield return FadeTo(1f, 0.3f);
            var op = SceneManager.LoadSceneAsync(scene);
            while (!op.isDone) yield return null;
            yield return null;
            yield return FadeTo(0f, 0.35f);
            Transitioning = false;
        }

        IEnumerator FadeTo(float a, float dur)
        {
            float from = fade.color.a, t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; SetFade(Mathf.Lerp(from, a, t / dur)); yield return null; }
            SetFade(a);
        }
        void SetFade(float a) { fade.color = new Color(0.16f, 0.1f, 0.07f, a); fade.raycastTarget = a > 0.01f; }

        void BuildOverlay()
        {
            var c = UiKit.CreateCanvas("GlobalOverlay", 1000, transform);
            var rt = UiKit.Rect(c.transform, "Fade", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            fade = rt.gameObject.AddComponent<Image>(); fade.color = new Color(0.16f, 0.1f, 0.07f, 0f); fade.raycastTarget = false;
            // Figma D-020: the pill under the top banner (no coin: it carries every kind of notice, D-022); it grows with the line
            var tp = UiKit.Fig(c.transform, "hud_toast", UiKit.Edge.Middle, UiKit.Edge.Start, 0f, 0f, false, "Toast");
            UiKit.Slice(tp, true); toastRect = tp.rectTransform;
            toast = UiKit.TitleLabel(tp.transform, "", 38, UiKit.FigBrown, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 70f));
            UiKit.AutoFit(tp, toast, 300f, 110f, 1700f);
            // coin notices ("몽글 코인 +50") get the Figma coin in front of the text (D-023)
            toastCoin = UiKit.SpriteImage(tp.transform, "Coin", "hud_toast_coin", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(58f, 58f)).gameObject;
            tp.gameObject.SetActive(false);
            toastPanel = tp.gameObject;
            UiSkin.Apply(c.transform, OverlaySkin);   // the notices' prefab, edited in Unity (Resources/UI/Skins, D-035)
        }

        /// <summary>D-035: the editable prefab of the notice pill and the fade.</summary>
        public const string OverlaySkin = "Overlay";
#if UNITY_EDITOR
        /// <summary>The overlay with a sample notice showing, for its editable prefab (UiSkinBaker).</summary>
        public GameObject BuildSkinSample()
        {
            BuildOverlay();
            Say("몽글 코인 +50");
            return toastPanel.transform.parent.gameObject;
        }
#endif
        GameObject toastPanel, toastCoin; RectTransform toastRect;

        public void Say(string msg, float seconds = 2.4f)
        {
            if (toast == null) return;
            toast.text = msg; toastPanel.SetActive(true); toastUntil = Time.unscaledTime + seconds;
            bool coin = msg != null && msg.Contains("몽글 코인");
            toastCoin.SetActive(coin);
            toast.rectTransform.anchoredPosition = new Vector2(coin ? 34f : 0f, 0f);
            var fit = toastRect.GetComponent<FitToText>(); fit.padX = coin ? 178f : 110f; fit.Apply();
            // a new notice appears straight in its place (top or under the banner); only later changes glide
            if (!toastLow)
            {
                SaveToastPlace();
                toastLift = 0f; toastRect.anchoredPosition = toastPos;
                toastLift = ToastWant(); toastRect.anchoredPosition = toastPos + new Vector2(0f, toastLift);
            }
        }

        /// <summary>
        /// A scene says when a window covers most of the screen (the village shop, storage, quest board): notices then show at
        /// the bottom edge, below the window's contents, instead of over its title, tabs and buttons.
        /// </summary>
        public static Func<bool> WindowOpen;

        /// <summary>
        /// A scene says where notices may rise to when the top of its screen is free (the village's title banner has gone): that
        /// band in screen pixels, centred on the screen's middle and as wide as the gap between the top buttons. Width 0 (or no
        /// scene asking) keeps notices in their own place under the banner.
        /// </summary>
        public static Func<Rect> TopFree;
        bool toastLow, toastSaved; Vector2 toastMin, toastMax, toastPivot, toastPos;
        float toastLift;   // how far the notice has risen above its own place (canvas units)

        void Update()
        {
            if (toastPanel == null || !toastPanel.activeSelf) return;
            if (Time.unscaledTime > toastUntil) { toastPanel.SetActive(false); return; }
            bool low = (WindowOpen != null && WindowOpen()) || Mongle.UI2.UiFront.AnyWindowOpen;
            if (low != toastLow)
            {
                SaveToastPlace();
                toastLow = low;
                if (low) { toastRect.anchorMin = toastRect.anchorMax = toastRect.pivot = new Vector2(0.5f, 0f); toastRect.anchoredPosition = new Vector2(0f, 20f); }
                else { toastRect.anchorMin = toastMin; toastRect.anchorMax = toastMax; toastRect.pivot = toastPivot; toastLift = ToastWant(); toastRect.anchoredPosition = toastPos + new Vector2(0f, toastLift); }
            }
            if (toastLow) return;
            // the notice glides up into the banner's place once that has gone (and back down if it comes again)
            float want = ToastWant();
            if (Mathf.Approximately(want, toastLift)) return;
            toastLift = Mathf.MoveTowards(toastLift, want, Time.unscaledDeltaTime * 600f);
            toastRect.anchoredPosition = toastPos + new Vector2(0f, toastLift);
        }

        /// <summary>The notice's own place (as built / as the Overlay prefab puts it), kept before it is moved.</summary>
        void SaveToastPlace()
        {
            if (toastSaved || toastRect == null) return;
            toastSaved = true;
            toastMin = toastRect.anchorMin; toastMax = toastRect.anchorMax; toastPivot = toastRect.pivot; toastPos = toastRect.anchoredPosition;
        }

        /// <summary>How far up the notice should sit: to the top of <see cref="TopFree"/> when it fits there, else not at all.</summary>
        float ToastWant()
        {
            if (TopFree == null || toastRect == null) return 0f;
            var free = TopFree();
            if (free.width <= 0f) return 0f;
            var c = toastRect.GetComponentInParent<Canvas>();
            float s = c != null && c.scaleFactor > 0f ? c.scaleFactor : 1f;
            if (toastRect.rect.width * s > free.width) return 0f;   // a long line would run into the coins: it stays below
            var w = new Vector3[4]; toastRect.GetWorldCorners(w);   // overlay canvas: world = screen pixels
            float ownTop = w[1].y - toastLift * s;
            return Mathf.Max(0f, (free.yMax - ownTop) / s);
        }
    }
}
