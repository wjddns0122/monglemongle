using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mongle.Data;
using Mongle.Diagnosis;
using Mongle.Photo;
using Mongle.Repair;
using Mongle.Toy;
using Mongle.Village;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mongle.Core
{
    /// <summary>
    /// Automated play-through used for QA (editor tests and the built app). It drives the real game code through the
    /// scripted pointer (same path as the mouse) and writes a plain result file.
    /// Launch args: -mongleQa full|midrepair|verifyfinal|verifymid  -mongleSave path  -mongleQaOut path  -mongleQaShots dir
    /// </summary>
    public class QaDriver : MonoBehaviour
    {
        public static QaDriver Instance { get; private set; }
        public readonly List<string> Lines = new List<string>();
        public int Fails { get; private set; }
        public int Passes { get; private set; }
        public bool Finished { get; private set; }
        string outPath, shotsDir, scenario, expectedPath;
        ScriptedSource src; GameRoot root;
        int logErrors; readonly List<string> errorTexts = new List<string>();
        Camera Cam => Camera.main;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void AutoStart()
        {
            var args = Environment.GetCommandLineArgs();
            string sc = Arg(args, "-mongleQa");
            if (string.IsNullOrEmpty(sc)) return;
            var go = new GameObject("QaDriver"); DontDestroyOnLoad(go);
            var d = go.AddComponent<QaDriver>();
            d.outPath = Arg(args, "-mongleQaOut") ?? "qa_result.txt";
            d.shotsDir = Arg(args, "-mongleQaShots");
            d.scenario = sc;
            d.expectedPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(d.outPath)) ?? ".", "qa_expected.json");
            d.StartCoroutine(d.Main());
        }

        static string Arg(string[] a, string key) { for (int i = 0; i < a.Length - 1; i++) if (a[i] == key) return a[i + 1]; return null; }

        /// <summary>The friend the next visit brings (a look at one friend's repair); null = the game's random pick.</summary>
        public static string ForceFriend;

        public static QaDriver StartForTest(string scenario, string outPath, string shotsDir)
        {
            var go = new GameObject("QaDriver"); DontDestroyOnLoad(go);
            var d = go.AddComponent<QaDriver>();
            d.outPath = outPath; d.shotsDir = shotsDir; d.scenario = scenario;
            d.expectedPath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(outPath)) ?? ".", "qa_expected.json");
            d.StartCoroutine(d.Main());
            return d;
        }

        void OnEnable() { Application.logMessageReceived += OnLog; Instance = this; }
        void OnDisable() { Application.logMessageReceived -= OnLog; }
        void OnLog(string msg, string stack, LogType t)
        {
            if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) { logErrors++; if (errorTexts.Count < 8) errorTexts.Add(t + ": " + msg); }
        }

        // ---------------- helpers ----------------
        void Check(string name, bool ok, string detail = "")
        {
            if (ok) Passes++; else Fails++;
            Lines.Add((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
            Debug.Log("[QA] " + (ok ? "PASS " : "FAIL ") + name + " " + detail);
        }

        public bool Aborted { get; private set; }
        IEnumerator Step(IEnumerator e) { if (Aborted) yield break; yield return Safe(e); }

        /// <summary>Runs a nested coroutine and turns an exception into a QA failure instead of silently stopping the run.</summary>
        IEnumerator Safe(IEnumerator e)
        {
            while (true)
            {
                object cur;
                try { if (!e.MoveNext()) yield break; cur = e.Current; }
                catch (Exception ex) { Check("exception in step", false, ex.GetType().Name + ": " + ex.Message + " @ " + ex.StackTrace.Split('\n').FirstOrDefault()); Aborted = true; yield break; }
                if (cur is IEnumerator inner) yield return Safe(inner); else yield return cur;
            }
        }

        public void ForceFlush() { if (!Finished) Flush(false); }

        IEnumerator Until(Func<bool> cond, float timeout, string what)
        {
            if (Aborted) yield break;
            float t = 0;
            while (t < timeout) { bool ok = false; try { ok = cond(); } catch (Exception) { } if (ok) yield break; t += Time.unscaledDeltaTime; yield return null; }
            Check("timeout: " + what, false, timeout + "s");
            Aborted = true; // later steps depend on this one
        }

        bool Reached(Func<bool> cond) { try { return cond(); } catch (Exception) { return false; } }

        Vector2 Scr(Vector3 w) { var p = Cam.WorldToScreenPoint(w); return new Vector2(p.x, p.y); }

        IEnumerator Move(Vector2 to, float dur = 0.2f)
        {
            var from = src.Position; float t = 0;
            while (t < dur) { t += Time.unscaledDeltaTime; src.Position = Vector2.Lerp(from, to, Mathf.Clamp01(t / dur)); yield return null; }
            src.Position = to;
        }

        IEnumerator Click(Vector2 p) { src.Position = p; src.Held = false; yield return null; src.Held = true; yield return null; yield return null; src.Held = false; yield return null; yield return null; }

        bool ClickButton(string label)
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsInactive.Exclude))
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                // by its words, or (picture-only buttons such as the village tiles) by its name "Btn_<label>"
                if (b.name == "Btn_" + label || b.GetComponentsInChildren<Text>().Any(t => t.text == label) || b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == label)) { b.onClick.Invoke(); return true; }
            }
            return false;
        }

        /// <summary>The UI pieces a click at <paramref name="p"/> would land on (the game ignores ground clicks under any of them).</summary>
        static string UiAt(Vector2 p)
        {
            var es = UnityEngine.EventSystems.EventSystem.current; if (es == null) return "";
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            es.RaycastAll(new UnityEngine.EventSystems.PointerEventData(es) { position = p }, hits);
            return string.Join(", ", hits.Select(h => { var t = h.gameObject.transform; var s = t.name; for (var q = t.parent; q != null; q = q.parent) s = q.name + "/" + s; return s; }));
        }

        IEnumerator ClickWhenReady(string label, float timeout = 10f)
        {
            float t = 0;
            while (t < timeout) { if (ClickButton(label)) yield break; t += Time.unscaledDeltaTime; yield return null; }
            Check("button '" + label + "' available", false, timeout + "s"); Aborted = true;
        }

        IEnumerator Shot(string name)
        {
            if (string.IsNullOrEmpty(shotsDir)) yield break;
            Directory.CreateDirectory(shotsDir);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shotsDir, name + ".png"));
            yield return null; yield return null;
        }

        void Flush(bool quit)
        {
            Lines.Add("SUMMARY passes=" + Passes + " fails=" + Fails + " logErrors=" + logErrors);
            foreach (var e in errorTexts) Lines.Add("LOGERR " + e);
            try { File.WriteAllText(outPath, string.Join("\n", Lines)); } catch (Exception e) { Debug.LogError(e); }
            Finished = true;
            if (quit && !Application.isEditor) Application.Quit(Fails == 0 ? 0 : 1);
        }

        ToyRuntime State => root.ActiveToy; // Mongi, or the hospital friend being treated (D-005)
        bool SavedDamageDone(string id)
        {
            var data = root.Saves.Load().data;
            var list = root.Patient != null ? data.patient.damages : data.damages;
            return list != null && list.Any(x => x.id == id && x.completed);
        }

        // ---------------- scenarios ----------------
        IEnumerator Main()
        {
            while ((root = GameRoot.Instance) == null) yield return null;
            src = new ScriptedSource { Position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            root.Pointer.UseScripted(src);
            Debug.Log("[QA] scenario " + scenario + " save=" + root.Saves.Path);
            StartCoroutine(Watchdog());
            IEnumerator run = null;
            switch (scenario)
            {
                case "full": run = Full(); break;
                case "ui": run = UiTour(); break;
                case "midrepair": run = MidRepair(); break;
                case "verifyfinal": run = VerifyFinal(); break;
                case "verifymid": run = VerifyMid(); break;
                case "corrupt": run = Corrupt(); break;
                case "friends": run = FriendsVisit(); break;
                case "verifyfriends": run = VerifyFriends(); break;
                default: Check("known scenario", false, scenario); break;
            }
            if (run != null) yield return Safe(run); // an exception becomes a FAIL and the run still reports and quits
            if (Finished) yield break;
            Check("no logged errors/exceptions", logErrors == 0, logErrors + " errors");
            Flush(true);
        }

        /// <summary>A scenario that hangs (or a run left in an unexpected state) must still write its report and quit.</summary>
        IEnumerator Watchdog()
        {
            const float limit = 25 * 60f;
            float t0 = Time.realtimeSinceStartup;
            while (!Finished && Time.realtimeSinceStartup - t0 < limit) yield return null;
            if (Finished) yield break;
            Check("scenario finishes in time", false, (limit / 60f) + " min");
            Flush(true);
        }

        IEnumerator ToIntroAndDiagnosis()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.RepairShop && ShopDirector.Current != null && ShopDirector.Current.Toy != null, 30, "RepairShop loaded");
            var d = ShopDirector.Current;
            if (State.DiagnosisComplete) yield break;
            yield return Until(() => d.Phase == FlowPhase.CustomerIntro, 30, "intro dialog");
            yield return Shot("01_intro");
            Check("intro dialog shown with toy name", Mongle.Views.DialogView.IsOpen);
            Check("arrival: no damage discovered yet", State.Damages.All(x => x.status == DamageStatus.Hidden));
            Check("notebook does not spoil names before discovery", true);
            ClickButton("살펴볼게요");
            yield return Until(() => d.Phase == FlowPhase.Diagnosis, 5, "diagnosis phase");
        }

        IEnumerator Diagnose()
        {
            var d = ShopDirector.Current; var orbit = d.Orbit; var toy = d.Toy; var cat = root.Catalog;
            // real drag rotates the camera
            float yaw0 = orbit.yaw;
            src.Position = new Vector2(Screen.width * 0.5f, Screen.height * 0.8f); yield return null;
            src.Held = true; yield return null;
            yield return Move(src.Position + new Vector2(180, 0), 0.3f);
            src.Held = false; yield return null; yield return new WaitForSeconds(0.4f);
            Check("drag rotates the diagnosis camera", Mathf.Abs(Mathf.DeltaAngle(orbit.yaw, yaw0)) > 5f, "yaw " + yaw0 + "→" + orbit.yaw);
            Check("rotation drag does not discover anything", State.Damages.All(x => x.status == DamageStatus.Hidden));
            orbit.SetOrbit(0, 14, 1.2f); yield return new WaitForSeconds(0.9f);
            // clicking a clean spot does nothing
            yield return Click(Scr(toy.Point("Head") + new Vector3(0.0f, 0.085f, 0.06f)));
            Check("clicking a clean spot discovers nothing", State.Damages.All(x => x.status == DamageStatus.Hidden));
            // wrong side: back tear is not discoverable from the front
            yield return Click(Scr(toy.Point("DP_BACK")));
            Check("hidden-side spot is not discoverable", State.Get("Tear").status == DamageStatus.Hidden);

            // staged hints: stuck players get help well before 10 seconds
            d.Diagnosis.ForceIdle(9.5f); yield return new WaitForSeconds(0.4f);
            Check("hint level 2 (sparkle) available by ~9.5s of being stuck", d.Diagnosis.HintLevel >= 2, "level " + d.Diagnosis.HintLevel);
            d.Diagnosis.ForceIdle(16f); yield return new WaitForSeconds(0.4f);
            Check("hint level 3 (direction note) after ~16s", d.Diagnosis.HintLevel >= 3, "level " + d.Diagnosis.HintLevel);
            Check("hint text does not name the damage", !d.Hud.BannerText.Contains("가시") && !d.Hud.BannerText.Contains("얼룩") && !d.Hud.BannerText.Contains("솜"), d.Hud.BannerText);
            var plan = new[] { ("Thorn", 0f), ("Dirt", 0f), ("LowCotton", -25f), ("Tear", 180f) };
            foreach (var (id, yaw) in plan)
            {
                orbit.SetOrbit(yaw, 14, 1.2f); yield return new WaitForSeconds(0.9f);
                yield return Until(() => !orbit.Focusing, 5, "focus shot ended");
                var def = cat.Damage(id);
                int before = State.Damages.Count(x => x.status != DamageStatus.Hidden); bool wasHidden = State.Get(id).status == DamageStatus.Hidden;
                yield return Click(Scr(toy.Point(def.damagePointName)));
                int after = State.Damages.Count(x => x.status != DamageStatus.Hidden);
                Check("discovered " + id, State.Get(id).status != DamageStatus.Hidden);
                Check("discovery of " + id + " adds only itself (no duplicate event)", after == before + (wasHidden ? 1 : 0), before + "→" + after);
                if (id == "Thorn") yield return Shot("02_discover_thorn");
                yield return new WaitForSeconds(1.5f);
            }
            // second click on a found one must not duplicate
            yield return Until(() => State.DiagnosisComplete, 5, "diagnosis complete flag");
            Check("diagnosis completes once", State.DiagnosisComplete);
            Check("save written at diagnosis complete", Reached(() => root.Saves.Load().data.diagnosisComplete));
            yield return Until(() => d.Phase == FlowPhase.Repair, 15, "repair phase");
        }

        IEnumerator RepairOne(string id, string wrongTool)
        {
            var d = ShopDirector.Current; var rep = d.Repair; var toy = d.Toy; var cat = root.Catalog;
            var def = cat.Damage(id);
            yield return Until(() => rep.Active && !rep.Busy && rep.Current == null, 20, "ready to pick tool for " + id);
            if (!string.IsNullOrEmpty(wrongTool))
            {
                rep.PickTool(wrongTool); yield return new WaitForSeconds(0.3f);
                Check("wrong tool is gently refused for " + id, rep.Current == null && State.Get(id).status == DamageStatus.Available);
            }
            rep.PickTool(def.requiredToolId);
            yield return Until(() => rep.Current != null, 8, "minigame started " + id);
            Check("repair active state for " + id, State.Get(id).status == DamageStatus.Active);
            yield return Until(() => !d.Orbit.Focusing || d.Orbit.FocusBlend >= 0.99f, 5, "focus arrived");
            yield return new WaitForSeconds(1.2f);
            var mg = rep.Current;
            var dpw = toy.Point(def.damagePointName);
            var look = id == "Thorn" ? mg.HintPoint : toy.Point(string.IsNullOrEmpty(def.lookPointName) ? def.damagePointName : def.lookPointName);
            float ang = Vector3.Angle(Cam.transform.forward, look - Cam.transform.position);
            float angDp = Vector3.Angle(Cam.transform.forward, dpw - Cam.transform.position);
            Check("close-up camera frames the " + id + " spot", ang < 6f && angDp < 40f, ang.ToString("F1") + "°/" + angDp.ToString("F1") + "° dist " + (look - Cam.transform.position).magnitude.ToString("F2"));
            int completes = 0; mg.OnCompleted += _ => completes++;
            switch (d.TreatingFriend ? "" : id)
            {
                case "Thorn": yield return DoThorn(mg, toy); break;
                case "Dirt": yield return DoDirt(mg, toy); break;
                case "Tear": yield return DoSew(mg, toy); break;
                case "LowCotton": yield return DoCotton(mg, toy); break;
                default: yield return DoGeneric(mg, toy, def); break;   // the friends' damages (D-009)
            }
            yield return Until(() => State.Get(id).status == DamageStatus.Completed, 30, id + " completed");
            yield return new WaitForSeconds(0.3f);
            Check(id + " completed exactly once", completes == 1);
            Check(id + " progress is 1", Mathf.Approximately(State.Get(id).progress, 1f));
            Check("save written after " + id, Reached(() => SavedDamageDone(id)));
            yield return Shot("repair_done_" + id);
            yield return Until(() => rep.Current == null && (!rep.Busy || State.AllRepaired), 15, "session ready after " + id);
        }

        IEnumerator DoThorn(RepairMiniGame mg, ToyActor toy)
        {
            var embed = toy.Point("Ear_L"); var grab = mg.HintPoint;
            {
                Vector3 V(Vector3 w) { var v = Cam.WorldToViewportPoint(w); return new Vector3(v.x, v.y, v.z); }
                var proxy = GameObject.Find("ThornProxy");
                var rend = proxy != null ? proxy.GetComponent<MeshRenderer>() : null;
                var pmf = proxy != null ? proxy.GetComponent<MeshFilter>() : null;
                Debug.Log("[QA] thorn mesh verts=" + (pmf != null && pmf.sharedMesh != null ? pmf.sharedMesh.vertexCount + " meshBounds=" + pmf.sharedMesh.bounds.size.ToString("F3") : "none") + " mats=" + (rend != null ? rend.sharedMaterials.Length + ":" + string.Join(",", rend.sharedMaterials.Select(m => m != null ? m.name : "null")) : "-") + " layer=" + (proxy != null ? proxy.layer : -1) + " activeInHierarchy=" + (proxy != null && proxy.activeInHierarchy));
                Debug.Log("[QA] thorn debug cam=" + Cam.transform.position.ToString("F3") + " fwd=" + Cam.transform.forward.ToString("F2")
                    + " DP=" + V(toy.Point("DP_EAR_L")).ToString("F2") + " GP=" + V(toy.Point("GP_THORN")).ToString("F2") + " grab=" + V(grab).ToString("F2")
                    + " proxy=" + (rend != null ? V(rend.bounds.center).ToString("F2") + " enabled=" + rend.enabled + " size=" + rend.bounds.size.ToString("F3") : "none")
                    + " earL=" + V(toy.Node("Ear_L").position).ToString("F2") + " head=" + V(toy.Node("Head").position).ToString("F2"));
                var gv = V(grab);
                Check("thorn grab point is on screen", gv.z > 0 && gv.x > 0.1f && gv.x < 0.9f && gv.y > 0.1f && gv.y < 0.9f, gv.ToString("F2"));
                Check("thorn is drawn (proxy renderer enabled)", rend != null && rend.enabled);
            }
            Check("thorn visible before repair", toy.ThornTransform.gameObject.activeSelf);
            yield return Shot("03_thorn_closeup");
            var pStart = Scr(grab);
            // wrong direction first: pushing into the ear must not progress
            src.Position = pStart; yield return null; src.Held = true; yield return null; yield return new WaitForSeconds(0.3f);
            yield return Move(Scr(embed - (grab - embed) * 1.5f), 0.4f);
            Check("pushing the wrong way does not progress", mg.Progress < 0.05f, mg.Progress.ToString("F2"));
            src.Held = false; yield return new WaitForSeconds(0.2f);
            // miss: grab away from the thorn
            src.Position = new Vector2(Screen.width * 0.82f, Screen.height * 0.12f); yield return null; src.Held = true; yield return new WaitForSeconds(0.3f);
            yield return Move(src.Position + new Vector2(-60, 60), 0.3f);
            Check("grabbing empty space does not progress", mg.Progress < 0.01f);
            src.Held = false; yield return new WaitForSeconds(0.2f);
            // proper pull
            var dir = (Scr(grab + (grab - embed).normalized * 0.12f) - pStart);
            src.Position = pStart; yield return null; src.Held = true; yield return null; yield return new WaitForSeconds(0.35f);
            float partial = 0;
            Debug.Log("[QA] thorn pull start: " + ((ThornMiniGame)mg).DebugInfo + " pStart=" + pStart + " dirPx=" + dir);
            for (int i = 1; i <= 24 && !mg.Completed; i++)
            {
                yield return Move(pStart + dir * (i / 24f), 0.05f);
                if (i == 5) { partial = mg.Progress; Debug.Log("[QA] thorn pull mid: " + ((ThornMiniGame)mg).DebugInfo); }
            }
            Check("pull progresses gradually", partial > 0.05f && partial < 1f, partial.ToString("F2"));
            src.Held = false; yield return null;
            yield return new WaitForSeconds(0.2f);
        }

        IEnumerator DoDirt(RepairMiniGame mg, ToyActor toy)
        {
            var field = toy.GetComponentInChildren<DirtField>(true);
            Check("dirt blobs exist before cleaning", field.Blobs.Count > 8, field.Blobs.Count + " blobs");
            // while the toy plays its nervous wobble, every blob must still sit on the surface under it
            var stick = SurfaceProbe.Create(toy.Body); float off = 0; string worst = "";
            for (int s = 0; s < 6; s++)
            {
                if (s > 0) yield return new WaitForSeconds(0.13f);
                stick.Rebake();
                foreach (var b in field.Blobs)
                {
                    var fw = b.t.forward; // blobs face into the surface; look only 1.5 cm around so an arm in front does not count
                    float o = stick.Raycast(new Ray(b.t.position - fw * 0.015f, fw), out var hp, out _, 0.04f) ? (hp - b.t.position).magnitude : 1f;
                    if (o > off)
                    {
                        off = o;
                        float wide = stick.Raycast(new Ray(b.t.position - fw * 0.06f, fw), out var hw, out _, 0.12f) ? Vector3.Dot(hw - b.t.position, fw) : 99f;
                        worst = b.t.name + " on " + (b.t.parent != null ? b.t.parent.name : "-") + " sample " + s + " along-normal gap " + wide.ToString("F3") + " deflate " + toy.GetBodyShape("Deflated").ToString("F2");
                    }
                }
            }
            Destroy(stick.gameObject);
            if (off >= 0.012f) Debug.Log("[QA] dirt stick worst: " + worst);
            Check("dirt stays stuck to the moving surface", off < 0.012f, off.ToString("F3") + " m");
            yield return Shot("04_dirt_before");
            // moving outside the dirt does nothing
            src.Position = Scr(toy.Point("Head") + new Vector3(0, 0.05f, 0.08f)); yield return null; src.Held = true; yield return null;
            yield return Move(src.Position + new Vector2(70, 0), 0.2f); yield return Move(src.Position - new Vector2(70, 0), 0.2f);
            Check("rubbing a clean area does not clean", mg.Progress < 0.01f);
            src.Held = false; yield return null;
            // rub on one spot cleans that spot first
            int first = 0; float bestD = 99; var c0 = toy.Point("DP_BELLY");
            for (int i = 0; i < field.Model.Count; i++) { float dd = (field.Model.Positions[i] - c0).magnitude; if (dd < bestD) { bestD = dd; first = i; } }
            src.Position = Scr(field.Model.Positions[first]); yield return null; src.Held = true; yield return null;
            // like a player, follow the spot while the nervous toy wobbles
            for (int k = 0; k < 8; k++) { yield return Move(Scr(field.Model.Positions[first]) + new Vector2(25, 6), 0.06f); yield return Move(Scr(field.Model.Positions[first]) - new Vector2(25, 6), 0.06f); }
            src.Held = false; yield return null;
            Check("rubbed spot gets cleaner than untouched spots", field.Model.Health[first] < 0.7f && field.Model.Health.Max() >= 0.99f, field.Model.Health[first].ToString("F2"));
            yield return Shot("04_dirt_partial");
            // finish
            float t = 0; src.Held = true; yield return null;
            while (!mg.Completed && t < 60)
            {
                for (int i = 0; i < field.Model.Count && !mg.Completed; i++)
                {
                    if (field.Model.Health[i] <= 0f) continue;
                    var p = Scr(field.Model.Positions[i]);
                    yield return Move(p, 0.07f); yield return Move(p + new Vector2(18, 4), 0.05f); yield return Move(p - new Vector2(18, 4), 0.05f);
                    t += 0.2f;
                }
                if (field.Model.Health.All(h => h <= 0f)) break;
            }
            src.Held = false; yield return null;
        }

        IEnumerator DoSew(RepairMiniGame mg, ToyActor toy)
        {
            var pts = toy.Seam.Anchors.ToArray();
            yield return Shot("05_sew_before");
            // jump to a later point: wrong order must not progress
            src.Position = Scr(pts[0].position + new Vector3(0.05f, 0, 0)); yield return null; src.Held = true; yield return null;
            yield return Move(Scr(pts[4].position), 0.2f);
            Check("skipping stitch order does not progress", mg.Progress < 0.01f);
            // proper order
            yield return Move(Scr(pts[0].position), 0.25f);
            for (int i = 0; i < 6; i++)
            {
                yield return Move(Scr(pts[i].position), 0.22f);
                yield return new WaitForSeconds(0.08f);
                if (i == 2) { Check("seam closes gradually while stitching", Mathf.Abs(mg.Progress - 0.5f) < 0.2f, mg.Progress.ToString("F2")); yield return Shot("05_sew_half"); }
            }
            src.Held = false; yield return null;
        }

        /// <summary>A friend's damage (D-009), driven by its type the way a player would: following the mini-game's hint point.</summary>
        IEnumerator DoGeneric(RepairMiniGame mg, ToyActor toy, DamageDefinition def)
        {
            float t = 0f;
            switch (def.type)
            {
                case DamageType.Thorn:
                case DamageType.Battery:
                    // pull it straight away from the body; a battery then takes the new one held on its slot
                    for (int tries = 0; tries < 4 && !mg.Completed; tries++)
                    {
                        var grab = mg.HintPoint;
                        var pull = mg is ThornMiniGame tg ? tg.PullDirection : mg is BatteryMiniGame bg ? bg.PullDirection : Vector3.zero;   // the way the game wants it pulled
                        if (pull.sqrMagnitude < 1e-6f) pull = (grab - toy.BodyBounds.center).normalized;
                        var p0 = Scr(grab); var dir = Scr(grab + pull.normalized * 0.12f) - p0;
                        src.Position = p0; yield return null; src.Held = true; yield return null; yield return new WaitForSeconds(0.35f);
                        for (int i = 1; i <= 24 && !mg.Completed; i++) yield return Move(p0 + dir * (i / 24f), 0.05f);
                        src.Held = false; yield return new WaitForSeconds(0.3f);
                        if (def.type != DamageType.Battery) continue;
                        t = 0f; src.Position = Scr(mg.HintPoint); yield return null; src.Held = true;
                        while (!mg.Completed && t < 8f) { src.Position = Scr(mg.HintPoint); t += Time.unscaledDeltaTime; yield return null; }
                        src.Held = false; yield return null;
                    }
                    break;
                case DamageType.Dirt:
                case DamageType.Paint:
                    var field = toy.GetComponentsInChildren<DirtField>(true).FirstOrDefault(f => f.DamageId == def.id);
                    src.Held = true; yield return null;
                    while (field != null && !mg.Completed && t < 60f)
                    {
                        for (int i = 0; i < field.Model.Count && !mg.Completed; i++)
                        {
                            if (field.Model.Health[i] <= 0f) continue;
                            var p = Scr(field.Model.Positions[i]);
                            yield return Move(p, 0.07f); yield return Move(p + new Vector2(18, 4), 0.05f); yield return Move(p - new Vector2(18, 4), 0.05f);
                            t += 0.2f;
                        }
                        if (field.Model.Health.All(h => h <= 0f)) break;
                    }
                    src.Held = false; yield return null;
                    break;
                case DamageType.Tear:
                case DamageType.EyeButton:
                    src.Position = Scr(mg.HintPoint); yield return null; src.Held = true; yield return null;
                    yield return Shot("sew_" + def.id + "_0");
                    for (int n = 1; !mg.Completed && t < 30f; n++)
                    {
                        yield return Move(Scr(mg.HintPoint), 0.2f); yield return new WaitForSeconds(0.1f); t += 0.3f;
                        if (n <= 8) yield return Shot("sew_" + def.id + "_" + n);
                    }
                    src.Held = false; yield return null;
                    break;
                default:   // cotton through a hole, fuel or water: hold the nozzle on the port
                    src.Position = Scr(mg.HintPoint); yield return null; src.Held = true;
                    while (!mg.Completed && t < 20f) { src.Position = Scr(mg.HintPoint); t += Time.unscaledDeltaTime; yield return null; }
                    src.Held = false; yield return null;
                    break;
            }
        }

        IEnumerator DoCotton(RepairMiniGame mg, ToyActor toy)
        {
            yield return Shot("06_cotton_before");
            var fp = toy.Point("FP_FILL");
            // holding away from the opening does not fill
            src.Position = Scr(fp) + new Vector2(560, 220); yield return null; src.Held = true; yield return new WaitForSeconds(0.8f);
            Check("holding away from the opening does not fill", mg.Progress < 0.01f);
            src.Held = false; yield return null;
            yield return Move(Scr(fp), 0.2f); src.Held = true; yield return null;
            yield return new WaitForSeconds(2.0f);
            Check("cotton fills while held", mg.Progress > 0.2f && mg.Progress < 1f, mg.Progress.ToString("F2"));
            yield return Shot("06_cotton_half");
            // release halfway: progress is kept, never lost
            float keep = mg.Progress; src.Held = false; yield return new WaitForSeconds(0.5f);
            Check("releasing keeps cotton progress", mg.Progress >= keep - 0.001f);
            src.Held = true; yield return null;
            float t = 0; while (!mg.Completed && t < 15) { t += Time.unscaledDeltaTime; yield return null; }
            src.Held = false; yield return null;
        }

        IEnumerator StampAndCustomize()
        {
            var d = ShopDirector.Current; var toy = d.Toy;
            yield return Until(() => d.Phase == FlowPhase.CompletionStamp, 20, "stamp phase");
            yield return new WaitForSeconds(1.2f); // panel appears and the camera glides to the front
            yield return Shot("07_stamp_panel");
            Check("stamp not applied automatically", !State.StampDone);
            var stampCam = d.Orbit.transform.position - toy.transform.position; stampCam.y = 0;
            Check("stamp camera sees the whole toy from the front", !d.Orbit.Focusing && Vector3.Dot(stampCam.normalized, toy.transform.forward) > 0.5f);
            ClickButton("도장 찍기!"); // double click must not double-stamp
            yield return Until(() => State.StampDone, 8, "stamp done");
            Check("stamp recorded once", State.History.Count(h => h.key == "Stamp") == 1);
            yield return Until(() => d.Phase == FlowPhase.Customization, 10, "customization phase");
            yield return new WaitForSeconds(1.2f); // let the camera glide back from the last close-up
            Check("dressing-up camera is out of the repair close-up", !d.Orbit.Focusing);
            var toCam = d.Orbit.transform.position - toy.transform.position; toCam.y = 0;
            Check("dressing-up camera sees the toy from the front", Vector3.Dot(toCam.normalized, toy.transform.forward) > 0.5f);
            yield return Shot("08a_customization_start");
            yield return ClickWhenReady("빨간 리본"); yield return new WaitForSeconds(0.2f);
            yield return ClickWhenReady("파란 모자"); yield return new WaitForSeconds(0.2f);
            yield return ClickWhenReady("동그란 안경"); yield return new WaitForSeconds(0.2f);
            Check("three accessories can be worn together", State.Visible.Count == 3);
            Check("accessory objects attached", toy.WornObject(AccessorySlot.Head) != null && toy.WornObject(AccessorySlot.Neck) != null && toy.WornObject(AccessorySlot.Face) != null);
            yield return ClickWhenReady("파란 모자"); yield return new WaitForSeconds(0.2f);
            Check("accessory can be removed", State.Visible.Count == 2 && toy.WornObject(AccessorySlot.Head) == null);
            yield return ClickWhenReady("파란 모자"); yield return new WaitForSeconds(0.2f);
            toy.Play("Idle"); yield return new WaitForSeconds(0.4f);
            d.Orbit.SetOrbit(15, 8, 0.95f); yield return new WaitForSeconds(0.9f);
            yield return Shot("08_customization");
            yield return ClickWhenReady("이대로 확정");   // Figma 115:1573
            yield return Until(() => State.CustomizationConfirmed, 5, "customization confirmed");
            Check("customization saved", Reached(() => root.Saves.Load().data.equipped.Count == 3));
        }

        IEnumerator Photo()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.PhotoStudio && PhotoDirector.Current != null && PhotoDirector.Current.Toy != null, 20, "PhotoStudio loaded");
            var p = PhotoDirector.Current;
            yield return new WaitForSeconds(0.8f);
            Check("equipped accessories visible in studio", p.Toy.WornObject(AccessorySlot.Neck) != null && p.Toy.WornObject(AccessorySlot.Head) != null && p.Toy.WornObject(AccessorySlot.Face) != null);
            foreach (var pose in new[] { "Idle", "Happy", "Wave" })
            {
                p.SetPose(pose); yield return new WaitForSeconds(0.6f);
                Check("pose " + pose + " plays", p.Toy.CurrentState == pose);
            }
            p.SetPose("Wave"); yield return new WaitForSeconds(0.9f);
            yield return Shot("09_photo_studio");
            p.Capture();
            yield return Until(() => p.LastShot != null && !p.Capturing, 8, "photo captured");
            Check("photo texture is square 1024", p.LastShot != null && p.LastShot.width == 1024 && p.LastShot.height == 1024);
            // not all one flat color
            var px = p.LastShot.GetPixels32(); var a = px[0]; int diff = 0; for (int i = 0; i < px.Length; i += 997) if (Mathf.Abs(px[i].r - a.r) + Mathf.Abs(px[i].g - a.g) + Mathf.Abs(px[i].b - a.b) > 40) diff++;
            Check("photo contains the scene (not blank)", diff > 30, diff.ToString());
            yield return Shot("10_photo_result");
            Check("keep photo", p.KeepPhoto());
            Check("second keep is refused", !p.KeepPhoto());
            Check("first photo file stored", File.Exists(PhotoStore.PathFor(root.Saves, PhotoStore.FirstPhotoFile)));
            Check("photo metadata saved", Reached(() => { var s = root.Saves.Load().data; return s.photo != null && s.photo.pose == "Wave" && s.photo.background == "Garden" && s.photo.accessoryIds.Count == 3 && !string.IsNullOrEmpty(s.photo.residentId); }));
        }

        IEnumerator VillageLife(bool fullChecks)
        {
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.Village && VillageDirector.Current != null && VillageDirector.Current.Ready, 25, "Village ready");
            var v = VillageDirector.Current;
            yield return new WaitForSeconds(1.0f);
            if (!State.Admitted)
            {
                yield return Shot("11_admission");
                Check("not admitted before invite", !State.Admitted);
                yield return ClickWhenReady("마을로 초대하기"); ClickButton("마을로 초대하기");
                yield return Until(() => State.Admitted, 8, "admitted");
                Check("admitted once with resident id", State.Admitted && !string.IsNullOrEmpty(State.ResidentId));
                Check("admission history unique", State.History.Count(h => h.key == "Admitted") == 1);
                Check("admission saved", Reached(() => root.Saves.Load().data.admitted));
            }
            yield return Until(() => v.Agent.State != AgentState.Disabled, 8, "agent enabled");
            yield return Shot("12_village");
            if (!fullChecks) yield break;

            // --- placement ---
            v.QaBeginPlacing();
            int benchBefore = State.Furniture.First(f => f.furnitureId == "Fur_Bench").cellX;
            Check("overlap placement refused", !v.QaMove("Fur_FlowerPot", State.Furniture.First(f => f.furnitureId == "Fur_Bench").cellX, State.Furniture.First(f => f.furnitureId == "Fur_Bench").cellY, 0));
            Check("out-of-bounds placement refused", !v.QaMove("Fur_Bench", v.Grid.MaxX - 1, v.Grid.MaxY - 1, 0));
            v.CancelPlacement();
            Check("refused moves leave layout unchanged", State.Furniture.First(f => f.furnitureId == "Fur_Bench").cellX == benchBefore);
            Check("flower pot can be moved", v.QaMove("Fur_FlowerPot", 9, 2, 0));
            Check("bench can be rotated and moved", v.QaMove("Fur_Bench", 2, 6, 1));
            yield return new WaitForSeconds(0.3f);
            Check("placement saved", Reached(() => { var s = root.Saves.Load().data; var f = s.furniture.First(x => x.furnitureId == "Fur_FlowerPot"); var b = s.furniture.First(x => x.furnitureId == "Fur_Bench"); return f.cellX == 9 && f.cellY == 2 && b.cellX == 2 && b.cellY == 6 && b.rotation == 1; }));
            v.ExitPlacing();
            yield return Shot("13_village_rearranged");

            // pointer-driven placement (drag the flower pot to another spot, then confirm with the button)
            v.QaBeginPlacing();
            var potObj = v.Furniture.First(f => f.Def.id == "Fur_FlowerPot");
            var potFrom = Scr(potObj.transform.position + Vector3.up * 0.1f);
            var targetCell = new Vector2Int(11, 7);
            var potTo = Scr(v.Grid.CenterOf(potObj.Def.footprint, targetCell.x, targetCell.y, 0) + Vector3.up * 0.1f);
            src.Position = potFrom; yield return null; src.Held = true; yield return null; yield return null;
            yield return Move(potTo, 0.5f); yield return null;
            src.Held = false; yield return null; yield return null;
            Check("dragging shows a pending placement", v.Selected == potObj);
            Check("pending placement is valid here", v.PendingValid);
            ClickButton("여기에 두기");
            yield return new WaitForSeconds(0.3f);
            Check("pointer placement confirmed and saved", State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX == 11 && Reached(() => root.Saves.Load().data.furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX == 11));
            // Figma 115:1398: 되돌리기 (Z) takes the move back, 다시 하기 (Y) does it again
            Check("undo takes the pot back", v.QaUndo() && State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX != 11);
            Check("redo puts it there again", v.QaRedo() && State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX == 11);
            v.ExitPlacing();
            Check("confirmed placement survives leaving placement mode", State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX == 11);
            v.QaBeginPlacing(); v.QaMove("Fur_FlowerPot", 9, 2, 0); v.ExitPlacing();

            // --- failure recovery: an unreachable bench is skipped, the toy goes back to idle ---
            var benchObj = v.Find(InteractionTag.Sit);
            v.Agent.MarkUnreachableForTest(benchObj);
            Check("unreachable furniture is not offered", !v.Agent.Available(benchObj));
            v.Agent.ForceIntent(Intent.Bench); yield return new WaitForSeconds(0.2f);
            Check("unreachable intent falls back to idle", v.Agent.CurrentIntent == Intent.Idle && v.Agent.State != AgentState.Move);

            // --- autonomy ---
            v.Agent.ForceIntent(Intent.Wander); yield return new WaitForSeconds(0.5f);
            Check("walk starts", v.Agent.State == AgentState.Move || v.Agent.State == AgentState.Idle);
            yield return Until(() => v.Agent.State == AgentState.Idle, 40, "wander finished");
            v.Agent.ClearUnreachableForTest();
            v.Agent.AbortToIdle("test reset");
            v.Agent.ForceIntent(Intent.Bench);
            yield return Until(() => v.Agent.Seated, 60, "bench sit");
            Check("toy sits on the bench", v.Agent.Seated);
            Check("furniture in use cannot be moved", !v.QaMove("Fur_Bench", 6, 6, 0));
            v.CancelPlacement();
            yield return new WaitForSeconds(1.5f);
            Check("seated toy is on the seat", (v.Toy.transform.position - v.Find(InteractionTag.Sit).SeatWorld).magnitude < 0.08f, (v.Toy.transform.position - v.Find(InteractionTag.Sit).SeatWorld).magnitude.ToString("F3"));
            yield return Shot("14_bench_sit");
            // talk while seated
            Check("talk while seated is accepted", v.Agent.TryTalk("안녕!"));
            yield return Until(() => !v.Agent.Seated, 40, "stand up");
            Check("toy stands up again", !v.Agent.Seated);
            yield return Until(() => v.Agent.State == AgentState.Idle, 20, "idle after bench");
            v.Agent.ForceIntent(Intent.Flower);
            yield return Until(() => v.Agent.State == AgentState.Interact && v.Agent.CurrentIntent == Intent.Flower, 40, "flower interact");
            Check("toy reaches the flower", v.Agent.CurrentIntent == Intent.Flower);
            yield return Shot("15_flower");
            yield return Until(() => v.Agent.State == AgentState.Idle, 30, "idle after flower");
            // talk by clicking the toy with the pointer
            yield return new WaitForSeconds(1.0f);
            var tp = Scr(v.Toy.transform.position + Vector3.up * 0.2f);
            yield return Click(tp);
            yield return new WaitForSeconds(0.4f);
            Check("clicking the toy makes it talk", v.Agent.State == AgentState.Talk || v.Agent.State == AgentState.Interact || v.Agent.State == AgentState.Idle);
            yield return Shot("16_talk");
            // 11-26: a click on Mongi opens its friend card, and a click anywhere outside the card closes it (user 2026-11-26)
            Check("clicking the toy opens its friend card", v.DressOpen);
            Check("the HUD stays for the friend card", !v.HudHidden);
            if (v.DressOpen)
            {
                var outside = new[] { new Vector2(0.5f, 0.62f), new Vector2(0.3f, 0.6f), new Vector2(0.7f, 0.6f), new Vector2(0.5f, 0.5f) }
                    .Select(frac => new Vector2(Screen.width * frac.x, Screen.height * frac.y)).FirstOrDefault(at => UiAt(at).Length == 0);
                yield return Click(outside); yield return new WaitForSeconds(0.3f);
                Check("a click outside the friend card closes it", !v.DressOpen, UiAt(outside));
                if (v.DressOpen) v.CloseDress();
            }
            yield return new WaitForSeconds(4.5f);
            // free life for a while: must never get stuck forever
            var counts = new Dictionary<Intent, int>(); float t = 0; Vector3 last = v.Toy.transform.position; float moved = 0;
            while (t < 45f)
            {
                t += Time.unscaledDeltaTime; counts[v.Agent.CurrentIntent] = (counts.TryGetValue(v.Agent.CurrentIntent, out var c) ? c : 0) + 1;
                moved += (v.Toy.transform.position - last).magnitude; last = v.Toy.transform.position; yield return null;
            }
            Check("toy keeps living on its own (moves)", moved > 0.3f, moved.ToString("F2") + "m");
            Check("toy still on navmesh", v.Agent.Nav.isOnNavMesh || v.Agent.Seated);
        }

        // ---------------- D-003: quests (accept -> progress -> claim, chains, saved) ----------------
        IEnumerator Quests()
        {
            var v = VillageDirector.Current; var w = root.World; var cat = v.Catalog;
            QuestDefinition Q(string id) => cat.Quest(id);
            long skew = 5000; w.Clock = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds() + skew;
            Check("six quests: three from the plant keeper, two from the furniture keeper, one from the accessory keeper", w.AllQuests.Count == 6 && w.QuestsOf("Shop_Plant").Count == 3 && w.QuestsOf("Shop_Furniture").Count == 2 && w.QuestsOf("Shop_Accessory").Count == 1);
            Check("chains: only the first quest of each keeper is open", w.StatusOf(Q("Q_Tulip")) == QuestStatus.Available && w.StatusOf(Q("Q_Chair")) == QuestStatus.Available
                && w.StatusOf(Q("Q_Accessory")) == QuestStatus.Available && w.StatusOf(Q("Q_Water")) == QuestStatus.Locked && w.StatusOf(Q("Q_Sunflower")) == QuestStatus.Locked && w.StatusOf(Q("Q_Wheel")) == QuestStatus.Locked);
            Check("nothing is tracked before accepting", v.TrackerShown == "");
            int coins0 = w.Coins;

            // accept through the real shop UI
            Check("plant shop opens", v.QaOpenShop("Shop_Plant")); yield return new WaitForSeconds(0.5f);
            ClickButton("부탁 보기 (!)"); yield return null;
            Check("the quest tab lists the open quest", v.Shop.QuestTabOpen);
            yield return Shot("50_quest_tab");
            Check("accepting works", v.Shop.QaClick("Quest_Accept_Q_Tulip") && w.StatusOf(Q("Q_Tulip")) == QuestStatus.Active);
            Check("a locked quest cannot be accepted", !w.AcceptQuest(Q("Q_Water")));
            Check("accepted quest is saved", Reached(() => root.Saves.Load().data.quests.Any(q => q.id == "Q_Tulip" && q.status == (int)QuestStatus.Active)));
            Check("tracker shows the active quest", v.TrackerShown == "" || v.TrackerShown.Contains("첫 화분 놓기")); // HUD is hidden while the shop is open
            ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            Check("tracker shows the active quest on the HUD", v.TrackerShown.Contains("첫 화분 놓기") && v.TrackerShown.Contains("0/1"), v.TrackerShown);

            // do the task: buy a tulip and put it down
            Check("buy a tulip for the quest", v.QaBuy("Plant_Tulip") == BuyResult.Ok);
            Check("place it", v.QaPlace("Plant_Tulip", 7, 7, 0)); v.ExitPlacing();
            Check("placing finishes the quest (ready to claim)", w.StatusOf(Q("Q_Tulip")) == QuestStatus.ReadyToClaim && w.ProgressOf(Q("Q_Tulip")) == 1);
            Check("the keeper has not paid yet", w.Coins == coins0 - 15);
            yield return new WaitForSeconds(0.3f);
            Check("tracker tells where to collect", v.TrackerShown.Contains("보상"), v.TrackerShown);

            // claim at the keeper (the shop opens straight on the quest tab)
            Check("shop reopens", v.QaOpenShop("Shop_Plant")); yield return new WaitForSeconds(0.4f);
            Check("it opens on the quest tab when a reward waits", v.Shop.QuestTabOpen);
            Check("claiming pays", v.Shop.QaClick("Quest_Claim_Q_Tulip") && w.Coins == coins0 - 15 + 15 && w.StatusOf(Q("Q_Tulip")) == QuestStatus.Done, w.Coins.ToString());
            Check("a quest pays only once", !w.ClaimQuest(Q("Q_Tulip")) && w.Coins == coins0);
            Check("finishing a quest opens the next one", w.StatusOf(Q("Q_Water")) == QuestStatus.Available);
            Check("accept the watering quest", v.Shop.QaClick("Quest_Accept_Q_Water")); ClickButton("나가기"); yield return new WaitForSeconds(0.3f);

            // five waterings (any plant) -> done
            var cactus = v.QaInstance("Plant_Cactus"); var sun = v.QaInstance("Plant_Sunflower"); int n = 0;
            foreach (var f in new[] { cactus, cactus, cactus, sun, sun })
            {
                skew += 30; Check("watering " + (++n) + " accepted", v.WaterPlant(f) == WaterResult.Ok); yield return new WaitForSeconds(1.7f);
            }
            Check("five waterings finish the quest", w.StatusOf(Q("Q_Water")) == QuestStatus.ReadyToClaim && w.ProgressOf(Q("Q_Water")) == 5);
            Check("extra events never push progress past the target", Reached(() => { w.Report("PlantWatered", "Plant_Cactus"); return w.ProgressOf(Q("Q_Water")) == 5; }));
            Check("claim the watering reward", v.QaOpenShop("Shop_Plant") && v.Shop.QaClick("Quest_Claim_Q_Water") && w.Coins == coins0 + 20, w.Coins.ToString());
            Check("accept the sunflower quest", v.Shop.QaClick("Quest_Accept_Q_Sunflower")); ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            int cBefore = w.Coins;
            for (int i = 0; i < 2; i++) { skew += 30; v.WaterPlant(sun); yield return new WaitForSeconds(1.7f); }
            Check("the sunflower blooms and finishes its quest", w.IsFullyGrown(w.Plant(sun.Placement.Id), sun.Def) && w.StatusOf(Q("Q_Sunflower")) == QuestStatus.ReadyToClaim);
            Check("bloom bonus and quest reward are separate", w.Coins == cBefore + 15, w.Coins.ToString());
            Check("claim the sunflower reward", v.QaOpenShop("Shop_Plant") && v.Shop.QaClick("Quest_Claim_Q_Sunflower") && w.Coins == cBefore + 15 + 40, w.Coins.ToString()); ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            Check("the plant keeper's chain is complete", w.QuestsOf("Shop_Plant").All(q => w.StatusOf(q) == QuestStatus.Done));
            yield return Shot("51_quest_done");

            // furniture keeper: Mongi must sit on the armchair
            Check("furniture shop opens", v.QaOpenShop("Shop_Furniture")); yield return new WaitForSeconds(0.4f);
            ClickButton("부탁 보기 (!)"); yield return null;
            Check("accept the chair quest", v.Shop.QaClick("Quest_Accept_Q_Chair")); ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 8 && w.StatusOf(Q("Q_Chair")) == QuestStatus.Active; i++)
            {
                v.Agent.AbortToIdle("qa quest"); v.Agent.ClearUnreachableForTest(); v.Agent.ForceIntent(Intent.Bench);
                yield return Until(() => v.Agent.Seated, 60, "sit for the quest"); yield return new WaitForSeconds(0.5f);
                yield return Until(() => !v.Agent.Seated, 40, "stand up");
            }
            Check("Mongi sitting on the armchair counts", w.StatusOf(Q("Q_Chair")) == QuestStatus.ReadyToClaim);
            int cc = w.Coins;
            Check("claim the chair reward", v.QaOpenShop("Shop_Furniture") && v.Shop.QaClick("Quest_Claim_Q_Chair") && w.Coins == cc + 30, w.Coins.ToString());
            Check("next: the wheel quest is open but not started", w.StatusOf(Q("Q_Wheel")) == QuestStatus.Available && w.ProgressOf(Q("Q_Wheel")) == 0);
            ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            Check("events for a quest that is not accepted do not count", Reached(() => { w.Report("MongiPlayed", "Fur_Wheel"); return w.ProgressOf(Q("Q_Wheel")) == 0 && w.StatusOf(Q("Q_Wheel")) == QuestStatus.Available; }));
            Check("tracker is empty when nothing is active", v.TrackerShown == "", v.TrackerShown);
            root.Persist("qa-quests");
            Check("quest states saved", Reached(() => { var s = root.Saves.Load().data; return s.quests.Count(q => q.status == (int)QuestStatus.Done) == 4 && s.coins == w.Coins; }));
        }

        // ---------------- D-003: the player moves Mongi (click-to-walk, camera-relative steering) ----------------
        static Vector3 Flat(Vector3 p) { p.y = 0; return p; }

        IEnumerator PlayerControl()
        {
            var v = VillageDirector.Current;
            v.Agent.AbortToIdle("qa player control"); yield return new WaitForSeconds(0.6f);
            var target = v.Grid.CenterOf(Vector2Int.one, 8, 8, 0);
            var over = UiAt(Scr(target));
            Check("no UI covers the ground the player clicks", over.Length == 0, over);
            // Mongi is one of the residents and lives on its own: a click on the bare ground does not send it anywhere
            Check("a click on the bare ground does not steer Mongi", !v.TryClickWorld(Scr(target)));
            v.Agent.Nav.Warp(target); yield return new WaitForSeconds(0.5f);   // drive from the open middle of the park (it used to walk there first)
            yield return Shot("48_player_walk");

            // steering: right on the screen is +screen-x
            var p0 = v.Toy.transform.position; float sx0 = Scr(p0).x; float t = 0;
            while (t < 1.4f) { v.DriveToy(Vector2.right); t += Time.deltaTime; yield return null; }
            Check("holding a direction drives Mongi", v.Agent.Driving && (v.Toy.transform.position - p0).magnitude > 0.5f, (v.Toy.transform.position - p0).magnitude.ToString("F2"));
            Check("steering is relative to the camera (right = right on screen)", Scr(v.Toy.transform.position).x > sx0 + 20f);
            Check("steering keeps Mongi on the walkable ground", v.Agent.Nav.isOnNavMesh);
            yield return Shot("49_player_drive");
            yield return null; yield return null;
            Check("driving stops when the keys are released", !v.Agent.Driving);
            // walk into the village edge: must stay inside the walkable area
            t = 0; while (t < 6f) { v.DriveToy(new Vector2(1f, 1f)); t += Time.deltaTime; yield return null; }
            var tp = v.Toy.transform.position; var pr = VillageDirector.ParkRect; // the park (D-006)
            Check("pushing against the edge never leaves the navmesh", v.Agent.Nav.isOnNavMesh && tp.x > pr.x - 0.1f && tp.x < pr.z + 0.1f && tp.z > pr.y - 0.1f && tp.z < pr.w + 0.1f, tp.ToString("F2"));

            yield return new WaitForSeconds(11f);
            yield return Until(() => v.Agent.CurrentIntent != Intent.Idle || v.Agent.State != AgentState.Idle, 40, "autonomy resumes");
            Check("autonomy resumes after the player stops", v.Agent.CurrentIntent != Intent.Idle || v.Agent.State != AgentState.Idle);
        }

        // ---------------- D-002: shops, storage, placing, reactions, watering ----------------
        IEnumerator Economy()
        {
            var v = VillageDirector.Current; var w = root.World;
            Check("repair rewards were paid once each (25+25+40+40+30)", w.Coins == 160, w.Coins.ToString());
            Check("coin display matches", v.CoinsShown == "160", v.CoinsShown);
            Check("three shops with keepers exist (plants, furniture, accessories)", v.Shops.Count == 3 && v.Shops.All(s => s.Def != null && s.Keeper != null && s.Def.items.Length > 0)
                && new[] { "Shop_Plant", "Shop_Furniture", "Shop_Accessory" }.All(want => v.Shops.Any(s => s.shopId == want)), string.Join(",", v.Shops.Select(s => s.shopId + ":" + (s.Def != null ? s.Def.items.Length : -1))));
            Check("only starter pieces exist at first", v.Furniture.Count == 2 && v.Furniture.All(f => f.Def.starter));
            Check("starters never trigger a 'new piece' reaction", v.Agent.Reactions == 0);

            // open the furniture shop by clicking the building with the pointer
            if (v.DressOpen) v.CloseDress();   // an open friend card takes the next click (it closes on a click outside it)
            v.Orbit.ReleaseFocus(); yield return new WaitForSeconds(0.4f);
            var fshop = v.Shops.First(s => s.shopId == "Shop_Furniture");
            yield return Shot("40_village_with_shops");
            yield return Click(Scr(fshop.transform.position + new Vector3(0.1f, 0.5f, 0.25f)));
            yield return Until(() => v.Shop.Visible, 5, "shop opens on click");
            Check("clicking a shop opens it", v.OpenedShop == fshop && v.Shop.Visible);
            Check("first visit is remembered", w.HasSeen("shop:Shop_Furniture"));
            Check("the HUD steps away while the shop is open", v.HudHidden);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("41_shop_furniture");
            Check("buying a chair works", v.Shop.QaClick("Buy_Fur_Armchair")); yield return null;
            Check("chair paid (160-45)", w.Coins == 115 && w.Owned("Fur_Armchair") == 1, w.Coins.ToString());
            Check("buying a lamp works", v.Shop.QaClick("Buy_Fur_Lamp")); yield return null;
            Check("lamp paid (115-35)", w.Coins == 80 && w.Owned("Fur_Lamp") == 1, w.Coins.ToString());
            Check("purchase is saved immediately", Reached(() => { var s = root.Saves.Load().data; return s.coins == 80 && s.inventory.Any(i => i.itemId == "Fur_Armchair" && i.count == 1); }));
            ClickButton("나가기"); yield return new WaitForSeconds(0.3f);
            Check("shop closes", !v.Shop.Visible && v.OpenedShop == null);
            Check("the HUD comes back when the shop closes", !v.HudHidden);

            // plant shop
            Check("plant shop opens", v.QaOpenShop("Shop_Plant")); yield return new WaitForSeconds(1.0f);
            yield return Shot("42_shop_plants");
            Check("tulip bought", v.Shop.QaClick("Buy_Plant_Tulip")); Check("sunflower bought", v.Shop.QaClick("Buy_Plant_Sunflower")); Check("cactus bought", v.Shop.QaClick("Buy_Plant_Cactus"));
            Check("plants paid (80-15-25-30)", w.Coins == 10, w.Coins.ToString());
            Check("an unaffordable item is refused and nothing changes", v.QaBuy("Plant_Sapling") == BuyResult.NotEnoughCoins && w.Coins == 10 && w.Owned("Plant_Sapling") == 0);
            ClickButton("나가기"); yield return new WaitForSeconds(0.3f);

            // place the tulip through the storage box UI, the rest through the same placement code
            ClickButton("보관함"); yield return new WaitForSeconds(0.3f);
            Check("storage box lists owned items", v.Storage.Visible);
            yield return Shot("43_storage");
            Check("place from storage starts placing", v.Storage.QaUse("Plant_Tulip") && v.PlacingMode && v.Selected != null && v.Selected.IsNew);
            Check("new piece waits unconfirmed", v.Selected.Pending && !State.Furniture.Any(f => f.furnitureId == "Plant_Tulip") && w.Owned("Plant_Tulip") == 1);
            Check("new piece starts on a valid cell", v.PendingValid);
            v.CancelPlacement(); v.ExitPlacing();
            Check("cancelling returns it to the box untouched", w.Owned("Plant_Tulip") == 1 && !v.Furniture.Any(f => f.Def.id == "Plant_Tulip") && !State.Furniture.Any(f => f.furnitureId == "Plant_Tulip"));
            Check("overlapping a bought piece is refused", !v.QaPlace("Plant_Tulip", State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellX, State.Furniture.First(f => f.furnitureId == "Fur_FlowerPot").cellY, 0));
            v.CancelPlacement(); v.ExitPlacing();
            Check("refused placement left the box intact", w.Owned("Plant_Tulip") == 1 && !State.Furniture.Any(f => f.furnitureId == "Plant_Tulip"));
            Check("tulip placed", v.QaPlace("Plant_Tulip", 5, 1, 0));
            Check("sunflower placed", v.QaPlace("Plant_Sunflower", 12, 2, 0));
            Check("cactus placed", v.QaPlace("Plant_Cactus", 12, 6, 0));
            Check("chair placed", v.QaPlace("Fur_Armchair", 5, 6, 0));
            Check("lamp placed", v.QaPlace("Fur_Lamp", 7, 2, 0));
            v.ExitPlacing();
            Check("storage box is empty after placing everything", w.Inventory.Count == 0);
            Check("placed pieces have unique instance ids", State.Furniture.Select(f => f.Id).Distinct().Count() == State.Furniture.Count && State.Furniture.Count == 7);
            Check("plant state created on placing", w.Plants.Count == 3 && w.Plants.All(p => p.waters == 0));
            Check("placement saved with instance ids", Reached(() => { var s = root.Saves.Load().data; return s.furniture.Count == 7 && s.furniture.Count(f => f.instanceId.Contains("#")) == 5 && s.inventory.Count == 0; }));
            Check("five new pieces await Mongi's first look", v.Furniture.Count(f => !w.HasSeen(f.Placement.Id)) == 5);

            // storing: plants and starters stay, furniture can go back and return
            v.QaBeginPlacing();
            Check("a bench cannot be stored", !v.QaStore("Fur_Bench"));
            Check("a plant cannot be stored", !v.QaStore("Plant_Cactus"));
            Check("a lamp can be stored", v.QaStore("Fur_Lamp") && w.Owned("Fur_Lamp") == 1 && State.Furniture.Count == 6);
            Check("stored lamp can be placed again", v.QaPlace("Fur_Lamp", 7, 2, 0) && State.Furniture.Count == 7 && w.Owned("Fur_Lamp") == 0);
            v.ExitPlacing();
            Check("storing and re-placing keeps exactly five unseen pieces", v.Furniture.Count(f => !w.HasSeen(f.Placement.Id)) == 5);
            yield return Shot("44_placed");

            // Mongi reacts to every new piece, once, and sits on the new chair
            yield return Until(() => v.Agent.Reactions >= 1, 60, "first reaction");
            Check("Mongi walks to a new piece and reacts", v.Agent.Reactions >= 1);
            yield return Shot("45_reaction");
            yield return Until(() => v.Furniture.All(f => w.HasSeen(f.Placement.Id)), 180, "all pieces seen");
            yield return new WaitForSeconds(1.0f);
            yield return Until(() => v.Agent.State == AgentState.Idle, 60, "idle after reactions");
            Check("every new piece got exactly one reaction", v.Agent.Reactions == 5, v.Agent.Reactions.ToString());
            Check("reactions are saved as seen", Reached(() => root.Saves.Load().data.seenInstances.Count(s => s.Contains("#")) == 5));
            // later life can pick the new tags (chair = sit, lamp/table = admire, toy box = play)
            var chair = v.QaInstance("Fur_Armchair");
            v.Agent.AbortToIdle("qa"); v.Agent.ForceIntent(Intent.Bench);
            yield return Until(() => v.Agent.Seated, 60, "sit again");
            Check("a seat is chosen among available chairs/benches", v.Agent.Target != null && (v.Agent.Target.Tags & InteractionTag.Sit) != 0);
            yield return Until(() => !v.Agent.Seated, 40, "stand up again");
            yield return Until(() => v.Agent.State == AgentState.Idle, 20, "idle");

            // watering with the real pointer path (click the plant), then the same code path for the rest
            long skew = 0; w.Clock = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + skew;
            var tulip = v.QaInstance("Plant_Tulip"); var tid = tulip.Placement.Id;
            Check("a new plant is thirsty", tulip.Plant.Thirsty);
            yield return Click(Scr(tulip.transform.position + Vector3.up * 0.18f));
            yield return new WaitForSeconds(0.4f);
            Check("clicking a plant waters it", w.Plant(tid).waters == 1, w.Plant(tid).waters.ToString());
            yield return Shot("46_watering");
            Check("a freshly watered plant is not thirsty", !w.NeedsWater(w.Plant(tid), tulip.Def));
            Check("watering twice in a row is refused", v.WaterPlant(tulip) == WaterResult.TooSoon && w.Plant(tid).waters == 1);
            yield return new WaitForSeconds(1.6f);
            skew += 30; Check("after the cooldown it can be watered again", v.WaterPlant(tulip) == WaterResult.Ok && w.Plant(tid).waters == 2);
            yield return new WaitForSeconds(1.7f);
            Check("the second watering grows it to stage 1", tulip.Plant.Stage == 1, tulip.Plant.Stage.ToString());
            for (int i = 0; i < 3; i++) { skew += 30; v.WaterPlant(tulip); yield return new WaitForSeconds(1.7f); }
            Check("five waterings fully grow it", w.Plant(tid).waters == 5 && tulip.Plant.Stage == 2 && w.IsFullyGrown(w.Plant(tid), tulip.Def));
            Check("blooming pays the bonus once (10+15)", w.Coins == 25 && w.Plant(tid).bloomRewarded, w.Coins.ToString());
            yield return Shot("47_bloom");
            skew += 30; Check("a fully grown plant takes no more water", v.WaterPlant(tulip) == WaterResult.FullyGrown && w.Coins == 25 && w.Plant(tid).waters == 5);
            var sun = v.QaInstance("Plant_Sunflower"); skew += 30; v.WaterPlant(sun); yield return new WaitForSeconds(1.7f);
            Check("each plant keeps its own growth", w.Plant(sun.Placement.Id).waters == 1 && w.Plant(v.QaInstance("Plant_Cactus").Placement.Id).waters == 0);

            // poking a lamp switches it
            var lamp = v.QaInstance("Fur_Lamp").GetComponentInChildren<LampLight>();
            bool was = lamp.On; v.ClickFurniture(v.QaInstance("Fur_Lamp")); yield return new WaitForSeconds(0.4f);
            Check("clicking the lamp toggles its light", lamp.On != was); v.ClickFurniture(v.QaInstance("Fur_Lamp"));
            yield return new WaitForSeconds(1.2f);
            Check("coins HUD follows the wallet", v.CoinsShown == w.Coins.ToString(), v.CoinsShown);
            w.Clock = () => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds() + skew;
            root.Persist("qa-economy");
            Check("final economy state saved", Reached(() => { var s = root.Saves.Load().data; return s.coins == 25 && s.plants.Count == 3 && s.plants.Any(p => p.waters == 5 && p.bloomRewarded); }));
        }

        void WriteExpected(string stage)
        {
            var t = State;
            var sb = new StringBuilder();
            sb.AppendLine("stage=" + stage);
            sb.AppendLine("resident=" + t.ResidentId);
            sb.AppendLine("admitted=" + t.Admitted);
            sb.AppendLine("history=" + t.History.Count);
            sb.AppendLine("equipped=" + string.Join(",", t.Equipped.Select(k => k.Key + ":" + k.Value).OrderBy(x => x)));
            sb.AppendLine("furniture=" + string.Join(",", t.Furniture.Select(f => f.Id + ":" + f.cellX + ":" + f.cellY + ":" + f.rotation).OrderBy(x => x)));
            var w = root.World;
            sb.AppendLine("coins=" + w.Coins);
            sb.AppendLine("inventory=" + string.Join(",", w.Inventory.Select(k => k.Key + "x" + k.Value).OrderBy(x => x)));
            sb.AppendLine("plants=" + string.Join(",", w.Plants.Select(p => p.instanceId + ":" + p.waters + ":" + p.bloomRewarded).OrderBy(x => x)));
            sb.AppendLine("seen=" + string.Join(",", w.Seen.OrderBy(x => x)));
            sb.AppendLine("quests=" + string.Join(",", w.QuestStates.Values.Select(q => q.id + ":" + (int)q.status + ":" + q.progress).OrderBy(x => x)));
            sb.AppendLine("photo=" + (t.Photo != null ? t.Photo.photoId + ":" + t.Photo.pose + ":" + t.Photo.takenAt : ""));
            sb.AppendLine("completed=" + string.Join(",", t.Damages.Where(x => x.status == DamageStatus.Completed).Select(x => x.id)));
            File.WriteAllText(expectedPath, sb.ToString());
        }

        Dictionary<string, string> ReadExpected()
        {
            var d = new Dictionary<string, string>();
            if (!File.Exists(expectedPath)) return d;
            foreach (var l in File.ReadAllLines(expectedPath)) { int i = l.IndexOf('='); if (i > 0) d[l.Substring(0, i)] = l.Substring(i + 1); }
            return d;
        }

        IEnumerator Full()
        {
            Check("new game starts without a save", State != null && FlowResolver.Resolve(State) == FlowPhase.CustomerArrival);
            yield return Step(ToIntroAndDiagnosis());
            yield return Step(Diagnose());
            yield return Step(RepairOne("Thorn", "Tool_Sponge"));
            Check("sequence is fixed: Dirt is next", State.NextRepair.id == "Dirt");
            yield return Step(RepairOne("Dirt", "Tool_Needle"));
            yield return Step(RepairOne("Tear", "Tool_CottonInjector"));
            yield return Step(RepairOne("LowCotton", "Tool_Tweezer"));
            yield return Step(StampAndCustomize());
            yield return Step(Photo());
            yield return Step(VillageLife(true));
            yield return Step(Economy());
            yield return Step(PlayerControl());
            yield return Step(Quests());
            yield return Step(Rides());
            Check("history entries are unique", State.History.Select(h => h.key).Distinct().Count() == State.History.Count);
            WriteExpected("village");
        }

        /// <summary>
        /// A walk past every screen for a UI review (screenshots): the whole game like <see cref="Full"/> plus the friends' visits,
        /// without the long toy-autonomy checks of the village (wandering, the bench), which test the toy and not the screens
        /// and can stop the run before the shop, quest and ride screens. Also shows the dress bar and the settings window.
        /// </summary>
        IEnumerator UiTour()
        {
            Check("new game starts without a save", State != null && FlowResolver.Resolve(State) == FlowPhase.CustomerArrival);
            yield return Step(ToIntroAndDiagnosis());
            yield return Step(Diagnose());
            yield return Step(RepairOne("Thorn", "Tool_Sponge"));
            yield return Step(RepairOne("Dirt", "Tool_Needle"));
            yield return Step(RepairOne("Tear", "Tool_CottonInjector"));
            yield return Step(RepairOne("LowCotton", "Tool_Tweezer"));
            yield return Step(StampAndCustomize());
            yield return Step(Photo());
            yield return Step(VillageLife(false));
            // every village screen gets its turn even when a step before it failed (its failures are still reported)
            yield return Tour(Economy());
            yield return Tour(DressAndSettings());
            yield return Tour(PlayerControl());
            yield return Tour(Quests());
            yield return Tour(Rides());
            yield return Tour(FriendsVisit());
        }

        IEnumerator Tour(IEnumerator e) { Aborted = false; yield return Safe(e); }

        IEnumerator DressAndSettings()
        {
            yield return ToVillageReady();
            var v = VillageDirector.Current;
            v.OpenDress(v.Agent); yield return new WaitForSeconds(0.6f);
            Check("dress bar opens", v.DressOpen);
            yield return Shot("45b_dress");
            v.CloseDress(); yield return new WaitForSeconds(0.3f);
            Mongle.UI2.UiFront.OpenSettings(); yield return new WaitForSeconds(0.6f);
            Check("settings window opens", Mongle.UI2.UiFront.SettingsOpen);
            yield return Shot("90_settings");
            var w = GameObject.Find("UI2_SettingsWindow"); if (w != null) w.SetActive(false);
            yield return new WaitForSeconds(0.3f);
        }

        // ---------------- park rides (D-006) ----------------
        static bool OnScreen(Vector3 world, Camera c) { var p = c.WorldToViewportPoint(world); return p.z > 0f && p.x > 0.05f && p.x < 0.95f && p.y > 0.05f && p.y < 0.95f; }

        IEnumerator Rides()
        {
            var v = VillageDirector.Current;
            if (v.ViewingRide != null) v.ExitRideView();
            v.Orbit.ReleaseFocus(); v.Orbit.SetOrbit(VillageDirector.DefaultOrbitYaw, VillageDirector.DefaultOrbitPitch, VillageDirector.DefaultOrbitDistance);
            yield return new WaitForSeconds(1.0f);
            var rides = Ride.All.ToList();
            Check("the carousel, the Ferris wheel and the balloon stand in the park", rides.Any(r => r is CarouselRide) && rides.Any(r => r is WheelRide) && rides.Any(r => r is BalloonRide), string.Join(",", rides.Select(r => r.rideId)));
            var tri = UnityEngine.AI.NavMesh.CalculateTriangulation();
            float minX = tri.vertices.Min(p => p.x), maxX = tri.vertices.Max(p => p.x), minZ = tri.vertices.Min(p => p.z), maxZ = tri.vertices.Max(p => p.z);
            float area = (maxX - minX) * (maxZ - minZ);
            Check("the walkable park is 5 x 5 = 25 times the old garden (6.7 x 5.3 m)", area > 35.5f * 22f && area < 35.5f * 27f, area.ToString("F0") + " m2 = x" + (area / 35.5f).ToString("F2"));
            foreach (var r in rides)
            {
                var b = r.BoardWorld;
                Check(r.displayName + " stands inside the walkable park and can be reached", b.x > minX && b.x < maxX && b.z > minZ && b.z < maxZ && v.Agent.CanReach(b), b.ToString("F1"));
            }
            yield return Shot("70_park_rides");

            // carousel: Mongi rides a horse; the player stops and restarts it
            var car = rides.OfType<CarouselRide>().First();
            v.Agent.ClearUnreachableForTest();
            Check("Mongi heads for the carousel", v.Agent.ForceRide(car));
            yield return Until(() => v.Agent.OnRide && v.Agent.Riding == car, 60, "Mongi on a carousel horse");
            // boarding is two hops now (onto the stairs, then onto the horse): wait until it has landed
            yield return Until(() => car.SeatOf(v.Agent) >= 0 && (v.Toy.transform.position - car.SeatPose(car.SeatOf(v.Agent)).position).magnitude < 0.05f, 6, "Mongi lands on the horse");
            int horse = car.SeatOf(v.Agent);
            Check("Mongi sits on a carousel horse and goes round with it", horse >= 0 && (v.Toy.transform.position - car.SeatPose(horse).position).magnitude < 0.05f, horse + " off " + (horse >= 0 ? (v.Toy.transform.position - car.SeatPose(horse).position).magnitude.ToString("F3") : "-"));
            var p0 = v.Toy.transform.position; yield return new WaitForSeconds(1.5f);
            Check("the ride carries Mongi round", (v.Toy.transform.position - p0).magnitude > 0.05f, (v.Toy.transform.position - p0).magnitude.ToString("F2"));
            var carCam = car.BoardWorld + (car.BoardWorld - car.transform.position).normalized * 2.2f + Vector3.up * 1.6f;
            v.Orbit.FocusOn(carCam, car.transform.position + Vector3.up * 0.7f); yield return new WaitForSeconds(1.4f);
            yield return Shot("71_carousel_ride");
            v.Orbit.ReleaseFocus(); yield return new WaitForSeconds(1.2f);
            yield return Click(Scr(car.ClickPoint));
            yield return Until(() => !car.Running && car.Motion.Speed01 < 0.01f, 6, "carousel stops");
            Check("clicking the carousel stops it", !car.Running && car.Motion.Speed01 < 0.01f);
            var rest = car.Motion.Spin.localRotation; yield return new WaitForSeconds(1.0f);
            Check("a stopped carousel stands still", Quaternion.Angle(rest, car.Motion.Spin.localRotation) < 0.01f);
            yield return Click(Scr(car.ClickPoint));
            yield return Until(() => car.Running && car.Motion.Speed01 > 0.99f, 6, "carousel runs again");
            Check("clicking it again starts it", car.Running && car.Motion.Speed01 > 0.99f);
            yield return Until(() => !v.Agent.OnRide, 50, "Mongi gets off the carousel");
            Check("after the ride Mongi hops off and walks again", !v.Agent.OnRide && v.Agent.Nav.enabled && v.Agent.Nav.isOnNavMesh);

            // Ferris wheel: a click shows a gondola up close and calls a toy over; it boards at the bottom and goes up
            var wheel = rides.OfType<WheelRide>().First();
            yield return new WaitForSeconds(0.5f);
            yield return Click(Scr(wheel.ClickPoint));
            yield return null;
            Check("clicking the Ferris wheel shows it up close", v.ViewingRide == wheel && v.Orbit.Focusing && v.RideCaptionShown != "", v.RideCaptionShown);
            Check("someone is called over to ride it", v.Agent.Riding == wheel || v.ResidentAgents.Any(a => a.Riding == wheel));
            yield return Until(() => wheel.RiderCount > 0, 90, "a toy gets into a gondola");
            Check("a toy gets into a gondola at the bottom", wheel.RiderCount > 0);
            var rider = wheel.Riders.FirstOrDefault(); int g = rider != null ? wheel.SeatOf(rider) : 0;
            yield return Until(() => Mathf.Abs(wheel.AngleFromBottom(g)) > 50f, 40, "the gondola goes up");
            yield return new WaitForSeconds(1.0f);
            Check("gondolas hang upright while the wheel turns", wheel.gondolas.All(t => Vector3.Angle(t.up, Vector3.up) < 1f));
            var radii = wheel.gondolas.Select(t => Vector3.ProjectOnPlane(t.position - wheel.spinner.position, wheel.transform.forward).magnitude).ToList();
            Check("all eight gondolas hang from the rim", radii.Count == 8 && radii.Max() / Mathf.Max(0.01f, radii.Min()) < 1.08f, string.Join(" ", radii.Select(r => r.ToString("F2"))));
            Check("the close view travels with the rider's gondola", v.ViewingSeat == g && (Cam.transform.position - wheel.seats[g].position).magnitude < 2.5f && rider != null && OnScreen(rider.transform.position + Vector3.up * 0.2f, Cam), v.ViewingSeat + "/" + g);
            yield return Shot("72_wheel_inside");
            ClickButton("밖에서 보기"); yield return new WaitForSeconds(0.6f);
            Check("'밖에서 보기' returns to the park view", v.ViewingRide == null && !v.Orbit.Focusing);
            yield return Until(() => wheel.RiderCount == 0 && rider != null && !rider.OnRide, 120, "the wheel ride ends");
            Check("after one full turn the rider gets off at the bottom", wheel.RiderCount == 0 && rider != null && !rider.OnRide && rider.Nav.isOnNavMesh);

            // hot-air balloon: same, and it lifts off and lands again
            var bal = rides.OfType<BalloonRide>().First();
            yield return new WaitForSeconds(0.5f);
            yield return Click(Scr(bal.ClickPoint));
            yield return null;
            Check("clicking the balloon shows its basket up close", v.ViewingRide == bal && v.Orbit.Focusing);
            yield return Until(() => bal.RiderCount > 0, 90, "a toy climbs into the basket");
            var flyer = bal.Riders.FirstOrDefault();
            yield return Until(() => bal.Altitude > 2.5f, 40, "the balloon lifts off");
            yield return new WaitForSeconds(1.0f);
            Check("the balloon rises with the toy in its basket", bal.Altitude > 2.5f && flyer != null && flyer.transform.position.y > 2.5f && OnScreen(flyer.transform.position + Vector3.up * 0.3f, Cam), bal.Altitude.ToString("F1"));
            yield return Shot("73_balloon_inside");
            yield return Until(() => bal.RiderCount == 0 && flyer != null && !flyer.OnRide, 60, "the balloon trip ends");
            Check("it lands and the toy climbs out", bal.RiderCount == 0 && bal.Altitude < 0.01f && flyer != null && flyer.Nav.isOnNavMesh);
            ClickButton("밖에서 보기"); yield return new WaitForSeconds(0.5f);
            Check("nothing is left in a ride view", v.ViewingRide == null);
        }

        // ---------------- hospital friends (D-005) ----------------
        IEnumerator ToVillageReady()
        {
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.Village && VillageDirector.Current != null && VillageDirector.Current.Ready && !root.Transitioning, 40, "village ready");
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator ClickHospital()
        {
            var v = VillageDirector.Current;
            v.Orbit.SetOrbit(VillageDirector.DefaultOrbitYaw, VillageDirector.DefaultOrbitPitch, VillageDirector.DefaultOrbitDistance);
            yield return new WaitForSeconds(1.0f);
            var h = FindAnyObjectByType<HospitalBuilding>();
            Check("the village has 몽글몽글 수선점", h != null);
            if (h == null) yield break;
            var col = h.GetComponent<Collider>();
            yield return Click(Scr(col.bounds.center + Vector3.up * col.bounds.extents.y * 0.3f));
        }

        IEnumerator TreatFriend()
        {
            var v = VillageDirector.Current;
            int residentsBefore = root.Residents.Count;
            var waiting = root.Catalog.friends.Where(f => f != null && f.prefab != null && root.Residents.All(r => r.kindId != f.kindId)).Select(f => f.kindId).ToList();
            // friends wear what the player bought at the accessory shop: one red ribbon for the coming friend
            Check("a red ribbon bought for the coming friend", v.QaBuy("Acc_RedRibbon") == BuyResult.Ok && root.World.Owned("Acc_RedRibbon") >= 1, root.World.Coins.ToString());
            if (!string.IsNullOrEmpty(ForceFriend) && root.Patient == null && waiting.Contains(ForceFriend)) root.AdmitPatient(root.Catalog.ToyDef(ForceFriend));
            ForceFriend = null;
            yield return ClickHospital();
            yield return Until(() => root.Patient != null, 5, "patient admitted");
            string kind = root.Patient != null ? root.Patient.KindId : "none";
            Check("clicking the shop brings a waiting friend: " + kind, waiting.Contains(kind), string.Join(",", waiting));
            if (root.Patient == null) { Aborted = true; yield break; }
            Check("patient saved right away", Reached(() => root.Saves.Load().data.patient.toyKindId == kind));
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.RepairShop && ShopDirector.Current != null && ShopDirector.Current.Toy != null, 30, "repair shop for " + kind);
            var d = ShopDirector.Current;
            Check("the shop treats the friend, not Mongi", d.TreatingFriend && d.Toy.name == kind, d.Toy.name);
            yield return Until(() => d.Phase == FlowPhase.CustomerIntro, 30, "intro for " + kind);
            Check("intro names the friend", Mongle.Views.DialogView.IsOpen);
            yield return Shot("60_friend_intro_" + kind);
            ClickButton("살펴볼게요");
            yield return Until(() => d.Phase == FlowPhase.Diagnosis, 5, "diagnosis for " + kind);
            foreach (var dmg in State.Damages.ToList())
            {
                // turn the camera to the side the damage faces (its DN point, D-009), then click it like a player
                var ddef = root.Catalog.Damage(dmg.id); var dp = d.Toy.Point(ddef.damagePointName); var dn = d.Toy.Node(ddef.normalPointName);
                var outw = dn != null ? dn.position - dp : dp - d.Toy.BodyBounds.center; outw.y = 0f;
                float yaw = outw.sqrMagnitude > 1e-6f ? Vector3.SignedAngle(d.Toy.transform.forward, outw.normalized, Vector3.up) : 0f;
                foreach (var tryYaw in new[] { yaw, -yaw })
                {
                    if (State.Get(dmg.id).status != DamageStatus.Hidden) break;
                    d.Orbit.SetOrbit(tryYaw, 14, 1.2f); yield return new WaitForSeconds(0.9f);
                    yield return Until(() => !d.Orbit.Focusing, 5, "focus ended");
                    yield return Click(Scr(d.Toy.Point(ddef.damagePointName)));
                    yield return new WaitForSeconds(0.3f);
                }
                Check(kind + ": discovered " + dmg.id, State.Get(dmg.id).status != DamageStatus.Hidden, "yaw " + yaw.ToString("F0"));
                yield return new WaitForSeconds(1.2f);
            }
            yield return Until(() => d.Phase == FlowPhase.Repair, 15, "repair phase for " + kind);
            foreach (var dmg in State.Damages.ToList()) yield return RepairOne(dmg.id, null);
            yield return Until(() => d.Phase == FlowPhase.CompletionStamp, 20, "stamp phase for " + kind);
            yield return Until(() => Mongle.Views.StampView.IsOpen, 10, "stamp card for " + kind);
            var card = FindAnyObjectByType<Mongle.Views.StampView>();
            if (card != null)
            {
                // Mongi's stamp first, then one per friend who moved in (user 2026-11-27: the second repair stamped the first slot)
                int shown = card.marks.Count(m => m != null && m.activeSelf), want = Mathf.Min(1 + residentsBefore, card.marks.Length - 1);
                Check(kind + "'s stamp card already shows the stamps before it", shown == want, shown + " shown, " + want + " expected");
            }
            yield return ClickWhenReady("도장 찍기!");   // the 11-26 stamp card's button
            yield return Until(() => State.StampDone, 8, "stamp for " + kind);
            yield return Until(() => d.Phase == FlowPhase.Customization, 10, "gift for " + kind);
            yield return new WaitForSeconds(1.0f);
            yield return ClickWhenReady("빨간 리본"); yield return new WaitForSeconds(0.3f);
            Check(kind + " wears the ribbon", d.Toy.WornObject(AccessorySlot.Neck) != null);
            yield return Shot("61_friend_gift_" + kind);
            yield return ClickWhenReady("이대로 확정");
            // a friend has its first photo taken and kept before it moves in (D-005)
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.PhotoStudio && PhotoDirector.Current != null && PhotoDirector.Current.Toy != null, 20, "photo studio for " + kind);
            var ph = PhotoDirector.Current;
            if (ph != null)
            {
                yield return new WaitForSeconds(0.8f);
                ph.Capture();
                yield return Until(() => ph.LastShot != null && !ph.Capturing, 8, "photo of " + kind);
                Check(kind + "'s first photo is kept for the album", ph.KeepPhoto());
            }
            yield return ToVillageReady();
            v = VillageDirector.Current;
            Check(kind + " moved into the village", root.Patient == null && root.Residents.Any(r => r.kindId == kind) && root.Residents.Count == residentsBefore + 1);
            if (residentsBefore == 0)
            {
                // the first friend's house: 나중에 puts it in the storage box, 사용하기 brings it back to be set down
                yield return Until(() => v.HousePlacing, 10, "house prompt for " + kind);
                if (v.HousePlacing)
                {
                    ClickButton("취소"); yield return new WaitForSeconds(0.3f);   // the placing screen's ✕ (Figma 115:1398): not now
                    Check(kind + "'s house waits in the storage box after 나중에", !v.HousePlacing && v.QaBoxHas("house:" + kind));
                    v.QaUseFromBox("house:" + kind); yield return new WaitForSeconds(0.3f);
                    Check("사용하기 on it brings the house back to be set down", v.HousePlacing);
                    yield return Shot("63a_house_from_box_" + kind);
                    ClickButton("보관함에 넣기"); yield return new WaitForSeconds(0.3f);   // back in the box: the visits go on
                }
            }
            Check(kind + " walks around the village", v.ResidentAgents.Any(a => a != null && a.Actor != null && a.Actor.name == kind && a.State != AgentState.Disabled));
            Check(kind + " saved as a resident with the gift", Reached(() => { var s2 = root.Saves.Load().data; return string.IsNullOrEmpty(s2.patient.toyKindId) && s2.residents.Any(r => r.kindId == kind && r.equipped.Any(e => e.accessoryId == "Acc_RedRibbon")); }));
            Check("Mongi is still the main toy after " + kind, root.Toy.Admitted && v.Agent != null && v.Agent.IsMain);
            yield return Shot("62_friend_home_" + kind);
        }

        IEnumerator FriendsVisit()
        {
            Check("friends need Mongi to live in the village first", root.Toy != null && root.Toy.Admitted, root.Toy != null ? FlowResolver.Resolve(root.Toy).ToString() : "no toy");
            yield return ToVillageReady();
            var kinds = root.Catalog.friends.Where(f => f != null && f.prefab != null).Select(f => f.kindId).ToList();
            Check("the friends are waiting to visit", kinds.Count >= 4, kinds.Count + ": " + string.Join(",", kinds));
            int coins0 = root.World.Coins;
            for (int n = 0; n < kinds.Count && root.NextFriend() != null; n++) yield return Step(TreatFriend());   // the next friend is picked at random
            if (Aborted) yield break; // a failed visit may leave us outside the village: stop here so the run still ends and reports
            Check("repairing friends earns coins", root.World.Coins > coins0, coins0 + "→" + root.World.Coins);
            yield return Step(ClickHospital()); yield return new WaitForSeconds(0.6f);
            Check("after everyone moved in, the shop calls nobody", root.Patient == null && root.NextFriend() == null && SceneManager.GetActiveScene().name == SceneNames.Village);
            var v = VillageDirector.Current;
            v.Orbit.SetOrbit(VillageDirector.DefaultOrbitYaw, VillageDirector.DefaultOrbitPitch, 7f); yield return new WaitForSeconds(2.5f);
            yield return Shot("63_friends_village");
        }

        IEnumerator VerifyFriends()
        {
            yield return ToVillageReady();
            var v = VillageDirector.Current;
            Check("residents restored after relaunch", root.Residents.Count == root.Catalog.friends.Count(f => f != null && f.prefab != null), root.Residents.Count.ToString());
            Check("every resident is spawned in the village", root.Residents.All(r => v.ResidentAgents.Any(a => a != null && a.Actor != null && a.Actor.name == r.kindId)));
            Check("residents keep their gifts", v.ResidentAgents.All(a => a.Actor.WornObject(AccessorySlot.Neck) != null));
            Check("no patient after relaunch", root.Patient == null);
            yield return Until(() => v.ResidentAgents.Any(a => a.State != AgentState.Idle || a.CurrentIntent != Intent.Idle), 15, "a resident starts doing something");
            Check("residents move on their own", v.ResidentAgents.Any(a => a.State != AgentState.Idle || a.CurrentIntent != Intent.Idle));
            yield return Shot("64_friends_after_relaunch");
            // nobody is told to: sooner or later a toy goes for a ride by itself (D-006)
            yield return Until(() => v.ResidentAgents.Any(a => a.OnRide) || v.Agent.OnRide, 240, "a toy rides on its own");
            var riding = v.ResidentAgents.Concat(new[] { v.Agent }).FirstOrDefault(a => a.OnRide);
            Check("toys use the rides on their own", riding != null, riding != null ? riding.Riding.displayName : "none");
            yield return Shot("74_friends_ride");
        }

        IEnumerator Corrupt()
        {
            string path = root.Saves.Path; string dir = Path.GetDirectoryName(path);
            Check("corrupt save is detected, not treated as a new game", root.NeedsSaveDecision && root.LoadResult.status == SaveStatus.Corrupt, root.LoadResult.status.ToString());
            Check("unreadable save is left untouched", File.Exists(path) && File.ReadAllText(path) == "{ this is not json");
            yield return Until(() => Mongle.Views.DialogView.IsOpen && FindObjectsByType<Button>(FindObjectsInactive.Exclude).Any(b => b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text == "새로 시작")), 15, "decision dialog");   // 11-26 dialog: TMP words
            yield return new WaitForSeconds(1.0f);
            Check("game waits for the player's decision", SceneManager.GetActiveScene().name == SceneNames.Bootstrap);
            Check("file still untouched while the dialog is open", File.ReadAllText(path) == "{ this is not json");
            yield return Shot("30_corrupt_dialog");
            ClickButton("새로 시작");
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.RepairShop, 20, "new game after decision");
            var moved = Directory.GetFiles(dir, Path.GetFileName(path) + ".corrupt-*");
            Check("unreadable save was kept aside (not deleted)", moved.Length == 1 && File.ReadAllText(moved[0]) == "{ this is not json");
            Check("a new game starts fresh", State != null && FlowResolver.Resolve(State) == FlowPhase.CustomerArrival && State.Damages.All(x => x.status == DamageStatus.Hidden));
        }

        IEnumerator MidRepair()
        {
            yield return Step(ToIntroAndDiagnosis());
            yield return Step(Diagnose());
            yield return Step(RepairOne("Thorn", null));
            yield return Step(RepairOne("Dirt", null));
            // start sewing, do three stitches, then "quit" in the middle
            var d = ShopDirector.Current; var rep = d.Repair;
            yield return Until(() => rep.Active && !rep.Busy && rep.Current == null, 20, "ready for Tear");
            rep.PickTool("Tool_Needle");
            yield return Until(() => rep.Current != null, 8, "sew started");
            yield return new WaitForSeconds(2.0f);
            var toy = d.Toy;
            src.Position = Scr(toy.Seam.Anchors[0].position); yield return null; src.Held = true; yield return null;
            for (int i = 0; i < 3; i++) { yield return Move(Scr(toy.Seam.Anchors[i].position), 0.25f); yield return new WaitForSeconds(0.1f); }
            Check("sewing in progress before quit", rep.Current.Progress > 0.3f && rep.Current.Progress < 0.6f, rep.Current.Progress.ToString("F2"));
            yield return Shot("20_midrepair_before_quit");
            WriteExpected("midrepair");
            src.Held = false;
        }

        IEnumerator VerifyMid()
        {
            var exp = ReadExpected();
            Check("expected file present", exp.Count > 0);
            Check("restart resumes in Repair phase", FlowResolver.Resolve(State) == FlowPhase.Repair);
            Check("completed repairs restored", string.Join(",", State.Damages.Where(x => x.status == DamageStatus.Completed).Select(x => x.id)) == exp["completed"], exp["completed"]);
            Check("unfinished Tear restarts from its start state", State.Get("Tear").status == DamageStatus.Available && State.Get("Tear").progress == 0f);
            Check("history not duplicated", State.History.Count == int.Parse(exp["history"]) && State.History.Select(h => h.key).Distinct().Count() == State.History.Count);
            Check("same resident id after restart", State.ResidentId == exp["resident"]);
            yield return Until(() => SceneManager.GetActiveScene().name == SceneNames.RepairShop && ShopDirector.Current != null && ShopDirector.Current.Toy != null, 30, "RepairShop loaded");
            yield return new WaitForSeconds(2.0f);
            var toy = ShopDirector.Current.Toy;
            Check("restored thorn is gone", !toy.ThornTransform.gameObject.activeSelf);
            Check("restored tear is still open", toy.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s => s.name == "DMG_Tear_Back").GetBlendShapeWeight(0) < 1f);
            Check("restored body is still deflated", toy.GetBodyShape("Deflated") > 0.99f);
            var rep = ShopDirector.Current.Repair;
            yield return Until(() => rep.Active && !rep.Busy, 20, "repair session resumed");
            Check("repair session resumes with Tear next", State.NextRepair.id == "Tear");
            yield return Shot("21_midrepair_after_restart");
            // finish the sewing now to prove the path continues
            yield return Step(RepairOne("Tear", null));
            yield return Step(RepairOne("LowCotton", null));
            yield return Step(StampAndCustomize());
            yield return Step(Photo());
            yield return Step(VillageLife(false));
            Check("repair rewards total once across the restart (160)", root.World.Coins == 160, root.World.Coins.ToString());
        }

        IEnumerator VerifyFinal()
        {
            var exp = ReadExpected();
            Check("expected file present", exp.Count > 0);
            Check("restart goes straight to Village", FlowResolver.Resolve(State) == FlowPhase.Village);
            Check("resident restored", State.Admitted && State.ResidentId == exp["resident"]);
            Check("accessories restored", string.Join(",", State.Equipped.Select(k => k.Key + ":" + k.Value).OrderBy(x => x)) == exp["equipped"], exp["equipped"]);
            Check("furniture layout restored", string.Join(",", State.Furniture.Select(f => f.Id + ":" + f.cellX + ":" + f.cellY + ":" + f.rotation).OrderBy(x => x)) == exp["furniture"], exp["furniture"]);
            var w = root.World;
            Check("coins restored", w.Coins.ToString() == exp["coins"], w.Coins + " vs " + exp["coins"]);
            Check("storage box restored", string.Join(",", w.Inventory.Select(k => k.Key + "x" + k.Value).OrderBy(x => x)) == exp["inventory"], exp["inventory"]);
            Check("plant growth restored", string.Join(",", w.Plants.Select(p => p.instanceId + ":" + p.waters + ":" + p.bloomRewarded).OrderBy(x => x)) == exp["plants"], exp["plants"]);
            Check("seen pieces restored", string.Join(",", w.Seen.OrderBy(x => x)) == exp["seen"]);
            Check("quest progress restored", string.Join(",", w.QuestStates.Values.Select(q => q.id + ":" + (int)q.status + ":" + q.progress).OrderBy(x => x)) == exp["quests"], exp["quests"]);
            Check("repair history restored without duplicates", State.History.Count == int.Parse(exp["history"]) && State.History.Select(h => h.key).Distinct().Count() == State.History.Count);
            Check("all repairs completed", State.AllRepaired && State.StampDone);
            Check("first photo record restored", State.Photo != null && (State.Photo.photoId + ":" + State.Photo.pose + ":" + State.Photo.takenAt) == exp["photo"]);
            var tex = PhotoStore.Load(root.Saves, State.Photo);
            Check("first photo image file loads after restart", tex != null && tex.width == 1024, tex != null ? tex.width + "x" + tex.height : "null");
            yield return Step(VillageLife(false));
            var v = VillageDirector.Current;
            Check("exactly one resident toy in the village", FindObjectsByType<ToyActor>(FindObjectsInactive.Exclude).Length == 1);
            Check("toy wears restored accessories", v.Toy.WornObject(AccessorySlot.Neck) != null && v.Toy.WornObject(AccessorySlot.Head) != null && v.Toy.WornObject(AccessorySlot.Face) != null);
            Check("furniture objects at restored cells", v.Furniture.Count == State.Furniture.Count && v.Furniture.All(f => State.Furniture.Any(p => p.Id == f.Placement.Id && p.cellX == f.Placement.cellX && p.cellY == f.Placement.cellY)));
            Check("both shops exist after restart", v.Shops.Count == 2 && v.Shops.All(s => s.Def != null && s.Keeper != null));
            Check("restored plants show their saved growth", v.Furniture.Where(f => f.Plant != null).All(f => f.Plant.Stage == w.StageOf(w.Plant(f.Placement.Id), f.Def)));
            Check("restored coin display matches", v.CoinsShown == w.Coins.ToString());
            Check("restored toy rests safely (Idle/Move/Interact, not stuck disabled)", v.Agent.State != AgentState.Disabled);
            // look at the album and history
            yield return new WaitForSeconds(2f);
            yield return Shot("22_restored_village");
            // second restart-safe: persist again must not change history
            int h0 = State.History.Count; root.Persist("qa"); Check("persisting again keeps history count", State.History.Count == h0);
            yield return new WaitForSeconds(8f);
            Check("autonomy resumes after restart", v.Agent.State != AgentState.Disabled && (v.Agent.Nav.isOnNavMesh || v.Agent.Seated));
            Check("no 'new piece' reaction repeats after restart", v.Agent.Reactions == 0, v.Agent.Reactions.ToString());
        }
    }
}
