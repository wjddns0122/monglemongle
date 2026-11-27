using System.Collections;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Diagnosis;
using Mongle.Repair;
using Mongle.Toy;
using Mongle.UI;
using Mongle.Views;
using UnityEngine;

namespace Mongle.Core
{
    /// <summary>
    /// Runs the RepairShop scene: arrival → intro → diagnosis → repair → stamp → customization, then hands off to the next scene.
    /// Phase is derived from ToyRuntime so a reload resumes at the right step (D-001-9).
    /// </summary>
    public class ShopDirector : MonoBehaviour
    {
        public static ShopDirector Current { get; private set; }
        public ToyActor Toy { get; private set; }
        public ShopUI UI { get; private set; }   // still the dress-up window (11-26: to be replaced)
        /// <summary>The 11-26 repair screen: banner, 진료 수첩, tool bar, zoom, back (a RepairShop scene object).</summary>
        public RepairHudView Hud { get; private set; }
        /// <summary>Dressing up after the stamp (Figma 115:1573): Assets/_UI수정/2_수선점/Customize.prefab.</summary>
        public CustomizeView Custom { get; private set; }
        public OrbitCamera Orbit { get; private set; }
        public DiagnosisController Diagnosis { get; private set; }
        public RepairSession Repair { get; private set; }
        public FlowPhase Phase { get; private set; } = FlowPhase.Boot;
        public Camera Cam { get; private set; }
        GameRoot root; ContentCatalog catalog; ToyRuntime state; ToyDefinition def;
        /// <summary>True when a hospital friend (not Mongi) is being treated (D-005).</summary>
        public bool TreatingFriend => root != null && root.Patient != null && state == root.Patient;
        public Vector3 ToySpot = Vector3.zero;

        void OnDestroy() { if (Current == this) Current = null; }

        IEnumerator Start()
        {
            Current = this;
            root = GameRoot.Instance;
            while (root == null) { root = GameRoot.Instance; yield return null; }
            catalog = root.Catalog; state = root.ActiveToy;
            Cam = Camera.main; Orbit = Cam.gameObject.GetOrAdd<OrbitCamera>();
            Orbit.target = new Vector3(0, 0.22f, 0);
            UI = new GameObject("ShopUI").AddComponent<ShopUI>(); UI.Build(); UI.ApplySkin("RepairScreen");   // dress-up (old skin)
            Hud = FindAnyObjectByType<RepairHudView>(FindObjectsInactive.Include);
            if (Hud == null)
            {
                var prefab = ViewIndex.Prefab("RepairHud");
                if (prefab != null) Hud = Instantiate(prefab).GetComponent<RepairHudView>();
            }
            ViewUtil.EnsureEventSystem();
            Hud.gameObject.SetActive(true); Hud.HideAll();
            Hud.OnZoom = f => Orbit.ZoomStep(f);
            Custom = FindAnyObjectByType<CustomizeView>(FindObjectsInactive.Include);
            if (Custom == null)
            {
                var prefab = ViewIndex.Prefab("Customize");
                if (prefab != null) Custom = Instantiate(prefab).GetComponent<CustomizeView>();
            }
            if (Custom != null)
            {
                Custom.OnPick = id => { var a = customList.FirstOrDefault(x => x.id == id); if (a != null) ToggleAccessory(a); };
                Custom.OnConfirm = ConfirmCustomization;
                Custom.Hide();
            }
            root.Audio.StartBgm();
            if (state == null) { GoBootstrap(); yield break; }

            def = catalog.ToyDef(state.KindId) ?? catalog.toy;
            var start = TreatingFriend ? FlowResolver.ResolvePatient(state) : FlowResolver.Resolve(state);
            Toy = ToyActor.Spawn(catalog, def, ToySpot, Quaternion.identity, false);
            Toy.ApplyRepairState(state);
            Toy.LayDirtFields(state);   // the stains go on once the toy stands in its pose (deflated, nervous)

            Diagnosis = gameObject.AddComponent<DiagnosisController>();
            Diagnosis.Toy = Toy; Diagnosis.State = state; Diagnosis.ToyName = def.displayName; Diagnosis.Orbit = Orbit; Diagnosis.Catalog = catalog; Diagnosis.Cam = Cam;
            Diagnosis.OnMessage = m => Hud.Banner(m);
            Diagnosis.OnChanged = () => { Hud.RefreshNotebook(state, catalog); if (state.AllDiscovered) StartCoroutine(DiagnosisDone()); };
            Repair = gameObject.AddComponent<RepairSession>();
            Repair.Toy = Toy; Repair.State = state; Repair.Catalog = catalog; Repair.Cam = Cam; Repair.Orbit = Orbit; Repair.UI = Hud;
            Repair.AllDone += () => StartCoroutine(ToStamp());

            switch (start)
            {
                case FlowPhase.CustomerArrival: StartCoroutine(Arrival()); break;
                case FlowPhase.Repair: BeginRepair(); break;
                case FlowPhase.CompletionStamp: StartCoroutine(ToStamp()); break;
                case FlowPhase.Customization: BeginCustomization(); break;
                default: root.GoToCurrentPhase(); break;
            }
        }

        void GoBootstrap() { root.GoToScene(SceneNames.Bootstrap); }

        // ---------------- arrival & intro ----------------
        IEnumerator Arrival()
        {
            Phase = FlowPhase.CustomerArrival;
            root.Pointer.Mode = PointerMode.None;
            var from = new Vector3(-1.5f, 0, 0.15f);
            Toy.transform.position = from; Toy.transform.rotation = Quaternion.Euler(0, 90, 0);
            Toy.Play("Walk", 0.05f);
            Orbit.SetOrbit(10, 12, 1.9f); Orbit.target = new Vector3(-0.2f, 0.22f, 0);
            yield return new WaitForSeconds(0.6f);
            root.Audio.Play("chime", 0.8f);
            float t = 0, dur = 2.6f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Toy.transform.position = Vector3.Lerp(from, ToySpot, Mathf.SmoothStep(0, 1, t / dur));
                yield return null;
            }
            Toy.transform.position = ToySpot;
            // turn toward the player
            float r = 0; var rot0 = Toy.transform.rotation;
            while (r < 0.5f) { r += Time.deltaTime; Toy.transform.rotation = Quaternion.Slerp(rot0, Quaternion.identity, r / 0.5f); yield return null; }
            Toy.transform.rotation = Quaternion.identity;
            Toy.Play("Nervous", 0.2f);
            Orbit.target = new Vector3(0, 0.22f, 0); Orbit.SetOrbit(0, 12, 1.3f);
            yield return new WaitForSeconds(0.7f);
            Phase = FlowPhase.CustomerIntro;
            Orbit.target = new Vector3(0, 0.1f, 0); // lift the toy above the dialog
            // friends (not Mongi, whose visit is the tutorial) can also be turned away (D-024; 11-26 "몽이가 왔어요" box)
            if (TreatingFriend)
                DialogView.Open(Josa.IGa(def.displayName) + " 왔어요", def.shortBackstory, "살펴볼게요", () => { Orbit.target = new Vector3(0, 0.22f, 0); BeginDiagnosis(); }, "죄송합니다", () => StartCoroutine(SendAway()));
            else
                DialogView.Open(Josa.IGa(def.displayName) + " 왔어요", def.shortBackstory, "살펴볼게요", () => { Orbit.target = new Vector3(0, 0.22f, 0); BeginDiagnosis(); });
        }

        /// <summary>"죄송합니다": the friend waves, walks back out of the door and the game returns to the village; it can come another day.</summary>
        IEnumerator SendAway()
        {
            Phase = FlowPhase.CustomerArrival;
            Toy.PlayThen("Wave", "Idle");
            yield return new WaitForSeconds(1.0f);
            var from = Toy.transform.position; var to = new Vector3(-1.5f, 0, 0.15f);
            Toy.Play("Walk", 0.1f);
            float r = 0; var rot0 = Toy.transform.rotation; var rot1 = Quaternion.Euler(0, -90, 0);
            while (r < 0.4f) { r += Time.deltaTime; Toy.transform.rotation = Quaternion.Slerp(rot0, rot1, r / 0.4f); yield return null; }
            float t = 0, dur = 2.2f;
            while (t < dur) { t += Time.deltaTime; Toy.transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / dur)); yield return null; }
            root.DeclinePatient();
            root.Say(Josa.EunNeun(def.displayName) + " 다음에 다시 올게요", 2.6f);
            root.GoToCurrentPhase();
        }

        // ---------------- diagnosis ----------------
        void BeginDiagnosis()
        {
            Phase = FlowPhase.Diagnosis;
            Hud.ShowNotebook(true); Hud.RefreshNotebook(state, catalog);
            Hud.SetInspectControls(true);
            Diagnosis.Begin();
        }

        IEnumerator DiagnosisDone()
        {
            Diagnosis.End();
            root.Pointer.Mode = PointerMode.None;
            yield return new WaitForSeconds(1.4f);
            root.Audio.Play("success", 0.8f);
            Toy.PlayThen("Relief", "Idle");
            Hud.Banner("문제를 모두 찾았어요! 이제 고쳐줄 차례예요");
            Hud.RefreshNotebook(state, catalog);
            yield return new WaitForSeconds(1.8f);
            BeginRepair();
        }

        void BeginRepair()
        {
            Phase = FlowPhase.Repair;
            Hud.SetInspectControls(true);
            Hud.ShowNotebook(true);
            Hud.RefreshNotebook(state, catalog);
            Orbit.SetOrbit(0, 12, 1.3f);
            Repair.Begin();
        }

        // ---------------- stamp ----------------
        IEnumerator ToStamp()
        {
            Phase = FlowPhase.CompletionStamp;
            Hud.HideAll(); UI.HideAll();
            root.Pointer.Mode = PointerMode.None;
            // the orbit may still face the last repaired spot (e.g. the back): show the whole toy from the front
            Orbit.ReleaseFocus(); Orbit.target = new Vector3(0, 0.27f, 0); Orbit.SetOrbit(15, 8, 1.05f);
            Toy.ApplyRepairState(state);
            yield return new WaitForSeconds(0.6f);
            StampView stamp = null;
            stamp = StampView.Open(StampsBefore(TreatingFriend, root.Residents.Count), () =>
            {
                root.Audio.Play("thump", 1f);
                stamp.PlayStamp(() =>
                {
                    if (!state.CompleteStamp()) return;
                    Orbit.Punch(0.03f);
                    Fx.Burst(catalog, Toy.transform.position + new Vector3(0, 0.3f, 0.1f), new Color(1f, 0.9f, 0.5f), 40, 0.018f, 0.5f, 1f, 0.05f);
                    root.Audio.Play("success", 1f);
                    Toy.PlayThen("Happy", "Idle");
                    StartCoroutine(StampToCustomize());
                });
            });
        }

        /// <summary>Stamps already on the card when this toy gets its own: Mongi's comes first, then one per friend who moved
        /// in (user 2026-11-27: the second repair stamped the first slot again).</summary>
        public static int StampsBefore(bool friend, int residents) => (friend ? 1 : 0) + Mathf.Max(0, residents);

        IEnumerator StampToCustomize()
        {
            yield return new WaitForSeconds(2.0f);
            StampView.Close();
            BeginCustomization();
        }

        // ---------------- customization ----------------
        void BeginCustomization()
        {
            Phase = FlowPhase.Customization;
            Hud.HideAll(); UI.HideAll();
            Toy.ApplyRepairState(state);
            Toy.SetAccessories(state.Visible, catalog);
            Orbit.ReleaseFocus(); // a focus close-up would override the orbit and hide the toy's front
            Orbit.target = new Vector3(0, 0.27f, 0); Orbit.SetOrbit(15, 8, 0.95f);
            root.Pointer.Mode = PointerMode.Orbit;
            Orbit.minDistance = 0.55f; Orbit.maxDistance = 1.6f;
            Hud.SetInspectControls(true);
            if (TreatingFriend)
            {
                // friends wear what the player bought at the accessory shop (one item is used up per friend)
                customList = catalog.AllAccessories.Where(a => root.World.Owned(a.id) > 0).ToArray();
                if (Custom != null)
                    Custom.Show(customList.Length > 0 ? "악세서리점에서 산 것으로 꾸며줘요" : "이번엔 그대로 가도 괜찮아요",
                        customList.Select(a => CardOf(a, root.World.Owned(a.id))).ToList(),
                        "아직 가진 악세서리가 없어요.\n마을의 컵케이크 악세서리점에서 사 오면 입혀 줄 수 있어요.\n이대로도 괜찮아요!");
            }
            else
            {
                // Mongi's first dress-up: the free gifts only (the rest is sold at the accessory shop later)
                customList = (def.giftAccessories != null && def.giftAccessories.Length > 0 ? def.giftAccessories : def.accessories ?? new AccessoryDefinition[0]).Where(a => a != null).ToArray();
                if (Custom != null) Custom.Show("마음에 드는 것을 골라 꾸며줘요", customList.Select(a => CardOf(a, -1)).ToList());
            }
            RefreshCustomButtons();
        }
        AccessoryDefinition[] customList = new AccessoryDefinition[0];

        /// <summary>A card: the accessory's name and picture (the accessory shop's icon, else one drawn from the accessory itself);
        /// <paramref name="count"/> below 0 shows no badge (Mongi's free gifts).</summary>
        CustomizeView.Item CardOf(AccessoryDefinition a, int count) => new CustomizeView.Item
        {
            id = a.id, name = a.displayName, count = count,
            icon = IconBaker.Get(catalog.Furniture(a.id)) ?? (a.prefab != null ? IconBaker.Get("acc:" + a.id, a.prefab) : null),
        };

        void ToggleAccessory(AccessoryDefinition a)
        {
            state.Visible.TryGetValue(a.slot, out var cur);
            state.SetPreview(a.slot, cur == a.id ? null : a.id);
            Toy.SetAccessories(state.Visible, catalog);
            root.Audio.Play(cur == a.id ? "click" : "chime", 0.6f, cur == a.id ? 0.8f : 1.2f);
            if (cur != a.id) Toy.PlayThen("Happy", "Idle");
            RefreshCustomButtons();
        }

        void RefreshCustomButtons()
        {
            if (Custom != null) Custom.SetWorn(state.Visible.Values);
        }

        void ConfirmCustomization()
        {
            if (!state.ConfirmCustomization()) return;
            root.Audio.Play("success", 0.9f);
            Toy.PlayThen("Happy", "Idle");
            if (Custom != null) Custom.Hide();
            if (TreatingFriend)
            {
                // what a friend wears was bought at the accessory shop: it is used up now (D-005, accessory shop)
                foreach (var id in state.Equipped.Values) root.World.TakeFromInventory(id);
                if (state.Equipped.Count > 0) root.World.Report("AccessoryWorn", def.kindId);
                root.Persist("friend dressed");
                Hud.Banner("멋져요! " + Josa.IGa(def.displayName) + " 사진을 찍으러 가요");
            }
            else
            {
                if (root.GiveStarterAccessories()) root.Persist("starter accessories");   // the gifts Mongi does not wear wait in the box
                Hud.Banner("멋져요! 사진을 남기러 가요");
            }
            StartCoroutine(Next());
        }

        IEnumerator Next() { yield return new WaitForSeconds(1.4f); root.GoToCurrentPhase(); }
    }
}
