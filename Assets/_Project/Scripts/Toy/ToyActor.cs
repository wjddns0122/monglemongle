using System.Collections;
using System.Collections.Generic;
using Mongle.Core;
using Mongle.Data;
using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>
    /// Scene-side view of the toy: finds model nodes by contract name, plays animation states, shows damage/expression
    /// state, equips accessories and applies small procedural poses. Gameplay state lives in ToyRuntime.
    /// </summary>
    public class ToyActor : MonoBehaviour
    {
        public const string BodyMesh = "GEO_Mongi";
        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        readonly Dictionary<AccessorySlot, GameObject> worn = new Dictionary<AccessorySlot, GameObject>();
        readonly Dictionary<AccessorySlot, string> wornIds = new Dictionary<AccessorySlot, string>();
        public Animator Animator { get; private set; }
        public SkinnedMeshRenderer Body { get; private set; }
        SkinnedMeshRenderer tear;
        GameObject thornObj, fillOpen, fillClosed;
        Transform earL, spine, head;
        public string CurrentState { get; private set; }
        public ContentCatalog Catalog;
        /// <summary>The toy's own definition (set by Spawn; null for a prefab placed by hand).</summary>
        public ToyDefinition Def;
        bool seamBuilt;
        System.Collections.Generic.List<StitchThread> seam;
        readonly Dictionary<string, List<StitchThread>> siteSeams = new Dictionary<string, List<StitchThread>>();
        readonly Dictionary<string, DirtField> dirtFields = new Dictionary<string, DirtField>();
        Coroutine once;

        // procedural additive poses (set by minigames)
        [HideInInspector] public float earPull;
        [HideInInspector] public Vector3 earPullDir;
        [HideInInspector] public float bodyPulse;
        /// <summary>Bone a thorn pull bends (null = Mongi's ear), the point it bends from and how far (D-009).</summary>
        [HideInInspector] public Transform pullBone, pullTip;
        [HideInInspector] public float pullBend = 0.55f;
        /// <summary>How limp the hurt arm hangs (0..1); the shown droop eases toward it (D-009).</summary>
        [HideInInspector] public float LimpTarget;
        float limp; Transform limpBone; Vector3 limpAxis = Vector3.up;

        void Awake() { Cache(); if (Application.isPlaying) WingFlap.Attach(this); }

        /// <summary>Spawn the toy prefab and its dirt blobs; call ApplyRepairState afterwards.</summary>
        public static ToyActor Spawn(ContentCatalog catalog, Vector3 pos, Quaternion rot, bool withDirt = true) => Spawn(catalog, catalog.toy, pos, rot, withDirt);

        /// <summary>Spawn any toy that follows the model contract (Mongi or a hospital friend, D-005).</summary>
        public static ToyActor Spawn(ContentCatalog catalog, ToyDefinition def, Vector3 pos, Quaternion rot, bool withDirt = true)
        {
            var go = Instantiate(def.prefab, pos, rot);
            go.name = def == catalog.toy ? "Mongi" : def.kindId;
            var actor = go.GetOrAdd<ToyActor>();
            actor.Catalog = catalog; actor.Def = def;
            if (withDirt) actor.BuildDirtFields();
            return actor;
        }

        /// <summary>One dirt/paint field per Dirt/Paint damage of this toy (Mongi's belly keeps its C-001 layout).</summary>
        public void BuildDirtFields()
        {
            if (Def == null || Def.damageSequence == null) return;
            foreach (var d in Def.damageSequence)
                if (d != null && (d.type == DamageType.Dirt || d.type == DamageType.Paint)) EnsureDirtField(d);
        }

        /// <summary>
        /// Lays the dirt/paint fields in the pose the toy is shown in and shows them as <paramref name="t"/> says (QA 2026-11-27:
        /// laid right after spawning, in the prefab's rest pose, the belly's lower stains hung 1–6 cm off the skin once the toy
        /// stood nervous and deflated). Call after <see cref="ApplyRepairState"/>.
        /// </summary>
        public void LayDirtFields(ToyRuntime t)
        {
            if (Animator != null && Animator.runtimeAnimatorController != null) Animator.Update(0.1f);   // into the pose just asked for, now
            BuildDirtFields();
            if (t == null) return;
            foreach (var d in t.Damages)
            {
                var field = DirtFieldFor(d.id); if (field == null) continue;
                bool clean = d.status == DamageStatus.Completed;
                if (clean) field.Model.Rub(Vector3.zero, 100f, 100f);
                field.SetVisible(!clean); field.Refresh();
            }
        }

        public DirtField EnsureDirtField(DamageDefinition d)
        {
            if (d == null) return null;
            var f = DirtFieldFor(d.id); if (f != null) return f;
            var mat = Catalog != null ? Catalog.Mat("M_Dirt") : null;
            f = d.HasSite ? DirtField.Build(this, d, mat) : DirtField.Build(this, mat);
            f.DamageId = d.id; dirtFields[d.id] = f;
            return f;
        }

        public DirtField DirtFieldFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (dirtFields.TryGetValue(id, out var f) && f != null) return f;
            foreach (var c in GetComponentsInChildren<DirtField>(true)) if (c.DamageId == id) { dirtFields[id] = c; return c; }
            return null;
        }

        void Cache()
        {
            if (nodes.Count > 0) return;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (!nodes.ContainsKey(t.name)) nodes[t.name] = t;
            Animator = GetComponentInChildren<Animator>(true);
            foreach (var s in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.name == BodyMesh) Body = s;
                else if (s.name == "DMG_Tear_Back") tear = s;
            }
            if (Body == null) Body = GetComponentInChildren<SkinnedMeshRenderer>(true);
            thornObj = Node("DMG_Thorn_EarL")?.gameObject;
            fillOpen = Node("DMG_FillOpen")?.gameObject;
            fillClosed = Node("DMG_FillClosed")?.gameObject;
            earL = Node("Ear_L"); spine = Node("Spine"); head = Node("Head");
        }

        public Transform Node(string name)
        {
            Cache();
            return name != null && nodes.TryGetValue(name, out var t) ? t : null;
        }

        /// <summary>Node by name, or <paramref name="fallback"/> when the model does not have it.</summary>
        public Transform NodeOr(string name, Transform fallback) { var n = string.IsNullOrEmpty(name) ? null : Node(name); return n != null ? n : fallback; }

        public Vector3 Point(string name, Vector3 fallback = default)
        {
            var n = Node(name); return n != null ? n.position : fallback;
        }

        public Bounds BodyBounds => Body != null ? Body.bounds : new Bounds(transform.position + Vector3.up * 0.2f, Vector3.one * 0.4f);

        List<Renderer> hiddenRenderers;
        public bool Hidden => hiddenRenderers != null;
        /// <summary>Hides the whole toy (inside the tube slide, inside its house, D-011/D-012); showing it again turns back on only what was on.</summary>
        public void SetHidden(bool hide)
        {
            if (hide == Hidden) return;
            if (hide)
            {
                hiddenRenderers = new List<Renderer>();
                foreach (var r in GetComponentsInChildren<Renderer>()) if (r.enabled) { r.enabled = false; hiddenRenderers.Add(r); }
            }
            else
            {
                foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
                hiddenRenderers = null;
            }
        }

        // ------------- animation -------------
        public void Play(string state, float fade = 0.15f)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return;
            if (once != null) { StopCoroutine(once); once = null; }
            CurrentState = state;
            Animator.CrossFadeInFixedTime(state, fade);
        }

        /// <summary>
        /// Plays a state from its first frame even when it is the one already playing (a cross-fade to the current state is
        /// ignored by the Animator, so a one-shot like Happy would stay frozen on its last frame). Also wakes an animator a
        /// mini-game left paused.
        /// </summary>
        public bool Restart(string state, float fade = 0.15f)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return false;
            int hash = Animator.StringToHash(state);
            if (!Animator.HasState(0, hash)) { Debug.LogWarning("[ToyActor] " + name + " has no animation state " + state); return false; }
            if (once != null) { StopCoroutine(once); once = null; }
            CurrentState = state;
            if (Animator.speed <= 0f) Animator.speed = 1f;
            bool same = Animator.GetCurrentAnimatorStateInfo(0).shortNameHash == hash || (Animator.IsInTransition(0) && Animator.GetNextAnimatorStateInfo(0).shortNameHash == hash);
            if (same) Animator.Play(hash, 0, 0f);
            else Animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
            return true;
        }

        public float ClipLength(string state)
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return 1f;
            foreach (var c in Animator.runtimeAnimatorController.animationClips) if (c.name == state) return c.length;
            return 1f;
        }

        /// <summary>Play a one-shot state, then return to <paramref name="then"/>.</summary>
        public float PlayThen(string state, string then, float fade = 0.12f)
        {
            Play(state, fade);
            float len = ClipLength(state);
            once = StartCoroutine(ThenRoutine(then, len));
            return len;
        }

        IEnumerator ThenRoutine(string then, float len)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, len - 0.1f));
            once = null;
            if (!string.IsNullOrEmpty(then)) { CurrentState = then; Animator?.CrossFadeInFixedTime(then, 0.2f); }
        }

        // ------------- blend shapes -------------
        /// <summary>Blend shape index by name (a key named EyeGone_EyeR is also found as EyeGone_R).</summary>
        public static int ShapeIndex(UnityEngine.Mesh m, string key)
        {
            if (m == null || string.IsNullOrEmpty(key)) return -1;
            int i = m.GetBlendShapeIndex(key);
            if (i < 0 && key.Contains("_Eye")) i = m.GetBlendShapeIndex(key.Replace("_Eye", "_"));
            return i;
        }

        public void SetBodyShape(string shape, float weight01)
        {
            if (Body == null || Body.sharedMesh == null) return;
            int i = ShapeIndex(Body.sharedMesh, shape);
            if (i >= 0) Body.SetBlendShapeWeight(i, Mathf.Clamp01(weight01) * 100f);
        }

        public float GetBodyShape(string shape)
        {
            if (Body == null || Body.sharedMesh == null) return 0f;
            int i = ShapeIndex(Body.sharedMesh, shape);
            return i >= 0 ? Body.GetBlendShapeWeight(i) / 100f : 0f;
        }

        readonly Dictionary<string, SkinnedMeshRenderer[]> overlays = new Dictionary<string, SkinnedMeshRenderer[]>();
        /// <summary>Body shape key plus every damage overlay mesh that carries the same key (so they follow the body shape).</summary>
        public void SetShapeAll(string key, float v)
        {
            if (string.IsNullOrEmpty(key)) return;
            SetBodyShape(key, v);
            // some overlay meshes get destroyed after repair
            if (!overlays.TryGetValue(key, out var list) || System.Array.Exists(list, x => x == null))
                overlays[key] = list = System.Array.FindAll(GetComponentsInChildren<SkinnedMeshRenderer>(true), x => x != Body && x.sharedMesh != null && ShapeIndex(x.sharedMesh, key) >= 0);
            foreach (var m in list) if (m != null) m.SetBlendShapeWeight(ShapeIndex(m.sharedMesh, key), Mathf.Clamp01(v) * 100f);
        }
        public void SetDeflate(float v) => SetShapeAll("Deflated", v);
        public void SetFace(float smile, float worried) { SetBodyShape("Smile", smile); SetBodyShape("Worried", worried); }

        public void SetTearClosed(float v)
        {
            if (tear == null || tear.sharedMesh == null) return;
            int i = tear.sharedMesh.GetBlendShapeIndex("Closed");
            if (i >= 0) tear.SetBlendShapeWeight(i, Mathf.Clamp01(v) * 100f);
        }
        public void SetTearVisible(bool on) { if (tear != null) tear.gameObject.SetActive(on); }
        public bool TearVisible => tear != null && tear.gameObject.activeSelf;

        /// <summary>A site tear (DMG_Tear_&lt;site&gt;, key Closed); Mongi's back tear for a legacy damage.</summary>
        public void SetTearClosed(DamageDefinition d, float v)
        {
            if (d == null || !d.HasSite) { SetTearClosed(v); return; }
            var n = string.IsNullOrEmpty(d.visualName) ? null : Node(d.visualName);
            var m = n != null ? n.GetComponent<SkinnedMeshRenderer>() : null;
            if (m == null || m.sharedMesh == null) return;
            int i = m.sharedMesh.GetBlendShapeIndex("Closed");
            if (i >= 0) m.SetBlendShapeWeight(i, Mathf.Clamp01(v) * 100f);
        }

        /// <summary>Show/hide a DMG_* node by name (older models simply do not have it).</summary>
        public void SetVisual(string name, bool on)
        {
            var n = string.IsNullOrEmpty(name) ? null : Node(name);
            if (n != null && n.gameObject.activeSelf != on) n.gameObject.SetActive(on);
        }

        /// <summary>The fixed stitch points SP_&lt;site&gt;_01.. of a site damage, as many as the model has.</summary>
        public List<Transform> StitchPoints(DamageDefinition d)
        {
            var list = new List<Transform>();
            if (d == null || string.IsNullOrEmpty(d.stitchPrefix)) return list;
            for (int i = 1; i < 100; i++) { var n = Node(d.stitchPrefix + i.ToString("00")); if (n == null) break; list.Add(n); }
            return list;
        }

        public void AdoptSeam(System.Collections.Generic.List<StitchThread> threads)
        {
            // D-018: a finished repair leaves no loose thread behind
            foreach (var t in threads) if (t != null) Destroy(t.gameObject);
            seamBuilt = true;
        }

        /// <summary>The finished threads of a site repair stay on the toy (one seam per damage).</summary>
        public void AdoptSeam(DamageDefinition d, List<StitchThread> threads)
        {
            if (d == null || !d.HasSite) { AdoptSeam(threads); return; }
            foreach (var t in threads) if (t != null) Destroy(t.gameObject);   // D-018: no loose thread after sewing
        }

        /// <summary>Finished seams are not drawn as thread (D-018): the repair is clean.</summary>
        const bool KeepThreads = false;

        void SetSiteSeam(DamageDefinition d, bool on)
        {
            bool has = siteSeams.TryGetValue(d.id, out var list);
            if (on && !has && Catalog != null && KeepThreads)
            {
                var sp = StitchPoints(d); if (sp.Count < 2) return;
                var parent = NodeOr(d.boneName, transform);
                list = new List<StitchThread>();
                for (int i = 0; i + 1 < sp.Count; i++) list.Add(StitchThread.Create(parent, sp[i], sp[i + 1], Catalog.Mat("M_Thread")));
                siteSeams[d.id] = list;
            }
            else if (!on && has) { foreach (var t in list) if (t != null) Destroy(t.gameObject); siteSeams.Remove(d.id); }
        }

        public void SetThornVisible(bool on) { if (thornObj != null) thornObj.SetActive(on); if (!on) EndThornProxy(false); }
        public Transform ThornTransform => thornObj != null ? thornObj.transform : null;
        SeamTracker seamTracker;
        public SeamTracker Seam { get { if (seamTracker == null && tear != null) seamTracker = SeamTracker.Create(this, tear); return seamTracker; } }
        GameObject thornProxy, proxySource; Renderer[] proxyHidden;

        /// <summary>Mongi's ear thorn.</summary>
        public Transform BeginThornProxy() => BeginThornProxy(null);

        /// <summary>
        /// The thorn (or a site's old battery) may be a skinned mesh, so moving its transform does nothing, and the original must stay
        /// on the model. For pulling we bake/copy it into a free proxy that follows its bone and can be moved/thrown freely.
        /// </summary>
        public Transform BeginThornProxy(DamageDefinition d)
        {
            bool site = d != null && d.HasSite;
            var srcT = site ? (string.IsNullOrEmpty(d.visualName) ? null : Node(d.visualName)) : (thornObj != null ? thornObj.transform : null);
            if (srcT == null) return null;
            if (thornProxy != null) return thornProxy.transform;
            var src = srcT.gameObject;
            var bone = site ? NodeOr(d.boneName, srcT.parent) : (earL != null ? earL : srcT.parent);
            var smr = src.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                var mesh = new UnityEngine.Mesh();
                smr.BakeMesh(mesh, true);
                mesh.RecalculateBounds(); // baked meshes come with empty bounds and would be culled
                Debug.Log("[Thorn] proxy mesh verts=" + mesh.vertexCount + " sourceReadable=" + smr.sharedMesh.isReadable);
                thornProxy = new GameObject("ThornProxy");
                thornProxy.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                thornProxy.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = thornProxy.AddComponent<MeshRenderer>(); r.sharedMaterials = smr.sharedMaterials;
            }
            else
            {
                // a plain mesh under a bone: a copy flies off while the original stays (hidden) on the model
                thornProxy = Instantiate(src, srcT.position, srcT.rotation);
                thornProxy.name = "ThornProxy"; thornProxy.SetActive(true);
                thornProxy.transform.localScale = srcT.lossyScale;
            }
            if (bone != null) thornProxy.transform.SetParent(bone, true);
            proxySource = src;
            proxyHidden = src.GetComponentsInChildren<Renderer>();
            foreach (var r in proxyHidden) r.enabled = false;
            return thornProxy.transform;
        }

        public void EndThornProxy(bool restoreOriginal)
        {
            if (thornProxy != null) { Destroy(thornProxy); thornProxy = null; }
            if (proxyHidden != null) foreach (var r in proxyHidden) if (r != null) r.enabled = true;
            if (proxySource != null && !restoreOriginal) proxySource.SetActive(false);
            proxyHidden = null; proxySource = null;
        }
        public void SetFillOpen(bool open)
        {
            if (fillOpen != null) fillOpen.SetActive(open);
            if (fillClosed != null) fillClosed.SetActive(!open);
        }

        /// <summary>Show the toy exactly as its repair state says (used on scene load and after each repair).</summary>
        public void ApplyRepairState(ToyRuntime t)
        {
            if (t == null) return;
            Cache(); // edit-mode spawns (previews, tools) never ran Awake
            // Mongi's tutorial nodes (C-001); a toy without those damages keeps them hidden/closed (D-009)
            bool hasThorn = t.Get("Thorn") != null, hasTear = t.Get("Tear") != null, hasCotton = t.Get("LowCotton") != null;
            bool thorn = IsDone(t, "Thorn"), tearDone = hasTear && IsDone(t, "Tear"), cotton = IsDone(t, "LowCotton");
            SetThornVisible(hasThorn && !thorn);
            SetTearVisible(hasTear);
            SetTearClosed(tearDone ? 1f : 0f);
            SetDeflate(hasCotton && !cotton ? 1f : 0f);
            SetFillOpen(false);
            int done = t.CompletedCount; int total = Mathf.Max(1, t.Damages.Count);
            bool all = t.AllRepaired;
            SetFace(all ? 1f : 0f, all ? 0f : Mathf.Lerp(1f, 0.25f, (float)done / total));
            foreach (var d in t.Damages)
            {
                var field = DirtFieldFor(d.id); if (field == null) continue;
                bool clean = d.status == DamageStatus.Completed;
                if (clean) field.Model.Rub(Vector3.zero, 100f, 100f);
                field.SetVisible(!clean); field.Refresh();
            }
            if (tearDone && !seamBuilt && KeepThreads && Catalog != null && Seam != null) { seam = StitchThread.BuildSeam(this, Catalog.Mat("M_Thread")); seamBuilt = true; }
            if (!tearDone && seamBuilt) { if (seam != null) foreach (var th in seam) if (th != null) Destroy(th.gameObject); seamBuilt = false; }
            ApplySiteDamages(t);
            if (t.CompletedCount == 0) Play("Nervous", 0.05f); else Play("Idle", 0.2f);
        }

        /// <summary>D-009 damages at named sites: each type shows its own hurt and its own repaired look.</summary>
        void ApplySiteDamages(ToyRuntime t)
        {
            LimpTarget = 0f; Transform limping = null; bool powerLow = false;
            if (Catalog != null)
                foreach (var rt in t.Damages)
                {
                    var d = Catalog.Damage(rt.id); if (d == null || !d.HasSite) continue;
                    bool done = rt.status == DamageStatus.Completed;
                    switch (d.type)
                    {
                        case DamageType.Thorn: SetVisual(d.visualName, !done); break;
                        case DamageType.Tear:
                            SetVisual(d.visualName, true); SetTearClosed(d, done ? 1f : 0f); SetSiteSeam(d, done);
                            if (!done && limping == null && !string.IsNullOrEmpty(d.limpBone)) { limping = Node(d.limpBone); LimpTarget = 1f; }
                            break;
                        case DamageType.EyeButton: SetShapeAll(d.shapeKey, done ? 0f : 1f); SetVisual(d.visualName, !done); SetSiteSeam(d, done); break;
                        case DamageType.LowCotton: SetShapeAll(d.shapeKey, done ? 0f : 1f); SetVisual(d.visualName, !done); break;
                        case DamageType.Fuel: SetVisual(d.visualName, true); SetVisual(d.openName, false); powerLow |= !done; break;
                        case DamageType.Battery: SetVisual(d.visualName, !done); SetVisual(d.visualDoneName, done); powerLow |= !done; break;
                    }
                }
            SetLimpBone(limping); limp = LimpTarget;
            SetGlow(powerLow ? 0f : 1f);
        }

        static bool IsDone(ToyRuntime t, string id) { var d = t.Get(id); return d != null && d.status == DamageStatus.Completed; }

        // ------------- limp arm (D-009) -------------
        void SetLimpBone(Transform b)
        {
            limpBone = b; limpAxis = Vector3.up;
            if (b == null) return;
            // the bone points toward its farthest child (the hand point); bones without children use their own Y axis
            float best = 0f;
            foreach (Transform c in b) if (c.localPosition.sqrMagnitude > best) { best = c.localPosition.sqrMagnitude; limpAxis = c.localPosition.normalized; }
        }

        // ------------- power lamps (D-009) -------------
        struct GlowSlot { public Renderer r; public int index; public Color emission; }
        List<GlowSlot> glows; MaterialPropertyBlock glowBlock; float glow = 1f;

        /// <summary>"Power" lamps (materials named *Glow*): dim while a battery/fuel repair is pending, full when repaired.</summary>
        public void SetGlow(float level01)
        {
            if (glows == null)
            {
                glows = new List<GlowSlot>();
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] != null && mats[i].name.Contains("Glow") && mats[i].HasProperty("_EmissionColor"))
                            glows.Add(new GlowSlot { r = r, index = i, emission = mats[i].GetColor("_EmissionColor") });
                }
            }
            level01 = Mathf.Clamp01(level01);
            if (glows.Count == 0 || Mathf.Abs(level01 - glow) < 0.001f) return;
            glow = level01;
            if (glowBlock == null) glowBlock = new MaterialPropertyBlock();
            foreach (var g in glows)
            {
                if (g.r == null) continue;
                glowBlock.Clear();
                if (glow < 0.999f) glowBlock.SetColor("_EmissionColor", g.emission * Mathf.Lerp(0.1f, 1f, glow));
                g.r.SetPropertyBlock(glowBlock, g.index);
            }
        }

        // ------------- accessories -------------
        public void SetAccessories(IReadOnlyDictionary<AccessorySlot, string> ids, ContentCatalog catalog)
        {
            foreach (AccessorySlot slot in System.Enum.GetValues(typeof(AccessorySlot)))
            {
                ids.TryGetValue(slot, out var want);
                wornIds.TryGetValue(slot, out var have);
                if (want == have) continue;
                if (worn.TryGetValue(slot, out var go) && go != null) Destroy(go);
                worn.Remove(slot); wornIds.Remove(slot);
                if (string.IsNullOrEmpty(want)) continue;
                var def = catalog.Accessory(want);
                var ap = def != null ? Node(def.attachPointName) : null;
                if (def == null || def.prefab == null || ap == null) { Debug.LogWarning("[ToyActor] cannot equip " + want); continue; }
                var inst = Instantiate(def.prefab, ap);
                inst.transform.localPosition = Vector3.zero; inst.transform.localRotation = Quaternion.identity; inst.transform.localScale = Vector3.one;
                inst.name = "WORN_" + want;
                worn[slot] = inst; wornIds[slot] = want;
            }
        }

        public GameObject WornObject(AccessorySlot s) => worn.TryGetValue(s, out var g) ? g : null;

        // ------------- procedural additive pose -------------
        void LateUpdate()
        {
            var pb = pullBone != null ? pullBone : earL;
            if (pb != null && earPull > 0.001f)
            {
                var tipPos = pullBone != null ? (pullTip != null ? pullTip.position : pb.position + pb.up * 0.05f) : Point("GP_THORN", earL.position + earL.up * 0.05f);
                var up = tipPos - pb.position; up = up.sqrMagnitude > 1e-8f ? up.normalized : pb.up;
                var want = Vector3.Slerp(up, earPullDir.sqrMagnitude > 0 ? earPullDir.normalized : up, Mathf.Clamp01(earPull) * pullBend);
                pb.rotation = Quaternion.FromToRotation(up, want) * pb.rotation;
            }
            limp = Mathf.MoveTowards(limp, LimpTarget, Time.deltaTime * 1.5f);
            if (limpBone != null && limp > 0.001f)
            {
                // the hurt arm does not follow the animation: it hangs ~35° out from straight down and dangles slowly
                var cur = limpBone.TransformDirection(limpAxis);
                float side = Vector3.Dot(limpBone.position - transform.position, transform.right) >= 0f ? 1f : -1f;
                var hang = Vector3.down * 0.82f + transform.right * side * 0.57f;
                hang = Quaternion.AngleAxis(Mathf.Sin(Time.time * 1.4f) * 9f, transform.right) * hang;
                limpBone.rotation = Quaternion.FromToRotation(cur, Vector3.Slerp(cur, hang.normalized, limp)) * limpBone.rotation;
            }
            if (spine != null && Mathf.Abs(bodyPulse) > 0.001f)
                spine.localScale = Vector3.one * (1f + bodyPulse);
            else if (spine != null && spine.localScale != Vector3.one) spine.localScale = Vector3.one;
        }
    }
}
