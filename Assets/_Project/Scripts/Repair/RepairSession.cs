using System;
using System.Collections;
using Mongle.Core;
using Mongle.Data;
using Mongle.Toy;
using Mongle.UI;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>
    /// Runs the ordered repairs: tool choice → close-up → mini-game → completion → next. Owns tool view, probe and hints.
    /// Gameplay state changes go through ToyRuntime; the UI only requests actions (05 §9).
    /// </summary>
    public class RepairSession : MonoBehaviour
    {
        public ToyActor Toy; [System.NonSerialized] public ToyRuntime State; public ContentCatalog Catalog; public Camera Cam; public OrbitCamera Orbit; public ShopUI UI;
        public event Action AllDone;
        public RepairMiniGame Current { get; private set; }
        public bool Busy { get; private set; }
        public bool Active { get; private set; }
        ToolView tool; SurfaceProbe probe; GameObject hintRing; float toolIdle;
        string expectToolId;

        public void Begin()
        {
            Active = true;
            GameRoot.Instance.Pointer.Mode = PointerMode.None;
            UI.ShowNotebook(true); UI.ShowShelf(true);
            UI.BuildToolShelf(Catalog.tools, PickTool);
            UI.OnBack = CancelCurrent;
            SelectNext();
        }

        public void End()
        {
            Active = false;
            UI.ShowShelf(false); UI.ShowBack(false);
        }

        void SelectNext()
        {
            var next = State.NextRepair;
            if (next == null) { Finished(); return; }
            var def = Catalog.Damage(next.id);
            expectToolId = def.requiredToolId;
            UI.RefreshNotebook(State, Catalog, next.id);
            UI.Banner(Catalog.DamageName(State.KindId, def) + " · 알맞은 도구를 골라요");
            Orbit.ReleaseFocus();
            GameRoot.Instance.Pointer.Mode = PointerMode.Orbit;
            Orbit.allowRotate = true;
            toolIdle = 0;
            Busy = false;
        }

        void Finished()
        {
            UI.RefreshNotebook(State, Catalog);
            UI.Banner("모두 고쳤어요!");
            Orbit.ReleaseFocus(); Orbit.allowRotate = true; // leave the last repair close-up so the stamp and dressing-up show the whole toy
            Active = false; Busy = false;
            AllDone?.Invoke();
        }

        void Update()
        {
            if (!Active) return;
            if (Current == null && !Busy)
            {
                toolIdle += Time.unscaledDeltaTime;
                UI.PulseTool(expectToolId, toolIdle > 8f);
            }
            else UI.PulseTool(null, false);
            if (Current != null && !Current.Completed)
            {
                UI.Banner(Current.Instruction);
                float s = Current.SecondsSinceProgress;
                if (s > 8f && hintRing == null)
                {
                    hintRing = Fx.Ring(Catalog, Current.HintPoint, 0.04f, new Color(1f, 0.9f, 0.5f, 0.9f));
                    GameRoot.Instance.Audio.Play("sparkle", 0.5f);
                }
                else if (s <= 8f && hintRing != null) { Destroy(hintRing); hintRing = null; }
                if (hintRing != null) hintRing.transform.position = Current.HintPoint;
            }
        }

        public void PickTool(string id)
        {
            if (!Active || Busy || Current != null) return;
            var next = State.NextRepair; if (next == null) return;
            var def = Catalog.Damage(next.id);
            if (id != def.requiredToolId)
            {
                GameRoot.Instance.Audio.Play("wrong", 0.5f);
                UI.ShakeTool(id);
                UI.Banner("이 도구는 지금은 쓸 곳이 없어요");
                return;
            }
            StartCoroutine(StartRepair(def));
        }

        IEnumerator StartRepair(DamageDefinition def)
        {
            Busy = true;
            if (!State.BeginRepair(def.id)) { Busy = false; yield break; }
            GameRoot.Instance.Audio.Play("click", 0.6f);
            var ptr = GameRoot.Instance.Pointer; ptr.Mode = PointerMode.None;
            // close-up
            var ca = Toy.Node(string.IsNullOrEmpty(def.repairAnchorName) ? def.cameraAnchorName : def.repairAnchorName); var dp = Toy.Point(string.IsNullOrEmpty(def.lookPointName) ? def.damagePointName : def.lookPointName);
            Vector3 camPos = ca != null ? ca.position : dp + Cam.transform.forward * -0.45f;
            var lookNode = Toy.Node(string.IsNullOrEmpty(def.lookPointName) ? def.damagePointName : def.lookPointName);
            if (def.type == DamageType.Thorn)
            {
                // the thorn mesh is skinned: its real place is where the baked proxy is, not where the GP_ empty sits
                var proxy = Toy.BeginThornProxy(def);
                var mf = proxy != null ? proxy.GetComponent<MeshFilter>() : null;
                if (mf != null && mf.sharedMesh != null) { lookNode = proxy; dp = proxy.TransformPoint(mf.sharedMesh.bounds.center); }
            }
            if (lookNode != null) Orbit.FocusOn(camPos, lookNode, dp); else Orbit.FocusOn(camPos, dp);
            UI.ShowShelf(false); UI.ShowBack(true); UI.Banner("");
            Toy.Play("Nervous", 0.1f);
            float w = 0; while (w < 0.9f) { w += Time.unscaledDeltaTime; yield return null; }
            // tool
            tool = ToolView.Spawn(Catalog.Tool(def.requiredToolId));
            tool.Follow(dp, Cam, true);
            bool needProbe = def.miniGameKey != "Thorn" && def.miniGameKey != "Battery"; // pulls move in the camera plane, not on the body
            probe = needProbe ? SurfaceProbe.Create(Toy.Body) : null;
            var ctx = new RepairContext
            {
                toy = Toy, state = State, def = def, catalog = Catalog, cam = Cam, orbit = Orbit, pointer = ptr,
                sfx = GameRoot.Instance.Audio, probe = probe, tool = tool
            };
            Current = AddMiniGame(def.miniGameKey);
            Current.OnCompleted += OnMiniGameCompleted;
            Current.Begin(ctx);
            ptr.Mode = PointerMode.Tool;
            Busy = false;
        }

        RepairMiniGame AddMiniGame(string key)
        {
            switch (key)
            {
                case "Thorn": return gameObject.AddComponent<ThornMiniGame>();
                case "Dirt": return gameObject.AddComponent<DirtMiniGame>();
                case "Sew": return gameObject.AddComponent<SewMiniGame>();
                case "Cotton": return gameObject.AddComponent<CottonMiniGame>();
                case "Paint": return gameObject.AddComponent<DirtMiniGame>(); // Dirt rules with a brush (D-009)
                case "Fuel": return gameObject.AddComponent<FuelMiniGame>();
                case "Battery": return gameObject.AddComponent<BatteryMiniGame>();
                default: throw new ArgumentException("unknown minigame " + key);
            }
        }

        void OnMiniGameCompleted(RepairMiniGame mg)
        {
            var def = mg.Ctx.def;
            State.SetProgress(def.id, 1f);
            State.CompleteDamage(def.id, Catalog.DamageName(State.KindId, def) + " 수선"); // fires events + autosave exactly once
            GameRoot.Instance.Pointer.Mode = PointerMode.None;
            StartCoroutine(AfterComplete(mg));
        }

        IEnumerator AfterComplete(RepairMiniGame mg)
        {
            Busy = true;
            UI.Banner("해냈어요!"); UI.ShowBack(false);
            UI.RefreshNotebook(State, Catalog);
            if (tool != null) tool.SetVisible(false);
            float w = 0; while (w < 1.8f) { w += Time.unscaledDeltaTime; yield return null; }
            Cleanup(mg);
            Toy.ApplyRepairState(State);
            UI.ShowShelf(true);
            SelectNext();
        }

        void Cleanup(RepairMiniGame mg)
        {
            if (hintRing != null) Destroy(hintRing);
            if (mg != null) { mg.OnCompleted -= OnMiniGameCompleted; Destroy(mg); }
            Current = null;
            if (tool != null) Destroy(tool.gameObject); tool = null;
            if (probe != null) Destroy(probe.gameObject); probe = null;
            GameRoot.Instance.Audio.SetRub(0); GameRoot.Instance.Audio.SetPuff(0);
            Toy.earPull = 0; Toy.bodyPulse = 0; Toy.pullBone = null; Toy.pullTip = null; Toy.pullBend = 0.55f;
            if (Toy.Animator != null) Toy.Animator.speed = 1f;
        }

        public void CancelCurrent()
        {
            if (Current == null || Current.Completed || Busy) return;
            var mg = Current; var id = mg.Ctx.def.id;
            mg.OnCompleted -= OnMiniGameCompleted;
            mg.Cancel();
            State.CancelRepair(id); // unfinished repair returns to its start state
            Cleanup(null);
            Toy.ApplyRepairState(State);
            UI.ShowBack(false); UI.ShowShelf(true);
            GameRoot.Instance.Audio.Play("click", 0.5f);
            SelectNext();
        }
    }
}
