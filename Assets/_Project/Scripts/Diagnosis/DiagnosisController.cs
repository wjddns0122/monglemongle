using System;
using System.Linq;
using Mongle.Core;
using Mongle.Data;
using Mongle.Repair;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Diagnosis
{
    /// <summary>
    /// Free inspection: rotate/zoom, click a suspicious spot. A damage is discovered only when its point is on the
    /// visible side and the click lands within a generous radius (03 §5, D-001-4). Stuck players get staged hints.
    /// </summary>
    public class DiagnosisController : MonoBehaviour
    {
        public ToyActor Toy; [System.NonSerialized] public ToyRuntime State; public OrbitCamera Orbit; public ContentCatalog Catalog; public Camera Cam;
        /// <summary>Name used in hints ("몽이의 왼쪽"); the hospital friend's name when a friend is the patient (D-005).</summary>
        public string ToyName = "몽이";
        public Action<string> OnMessage;       // banner/toast text
        public Action OnChanged;               // notebook refresh
        public float Hint1 = 6f, Hint2 = 9f, Hint3 = 15f;
        public float VisibleDot = 0.05f;
        public float ClickSlopPixels = 8f;
        public int HintLevel { get; private set; }
        float idle;
        Vector2 pressPos; bool pressing; float pressDist;
        GameObject hintRing;
        public bool Active { get; private set; }
        SurfaceProbe probe;
        public float OcclusionTolerance = 0.05f;

        public void Begin()
        {
            Active = true; idle = 0; HintLevel = 0;
            if (probe == null) probe = SurfaceProbe.Create(Toy.Body);
            GameRoot.Instance.Pointer.Mode = PointerMode.Orbit;
            Orbit.allowRotate = true; Orbit.allowZoom = true;
            OnMessage?.Invoke(Josa.EulReul(ToyName) + " 돌려보며 불편한 곳을 찾아봐요");
        }

        public void End()
        {
            Active = false; ClearHint();
            if (probe != null) { Destroy(probe.gameObject); probe = null; }
        }

        void Update()
        {
            if (!Active || State == null) return;
            var ptr = GameRoot.Instance.Pointer;
            var f = ptr.World;
            if (f.pressed) { pressPos = f.position; pressing = true; pressDist = 0; }
            if (pressing && f.held) pressDist = Mathf.Max(pressDist, (f.position - pressPos).magnitude);
            if (pressing && f.released)
            {
                pressing = false;
                if (pressDist <= ClickSlopPixels && !Orbit.Focusing) HandleClick(f.position);
            }
            if (State.AllDiscovered) return;
            idle += Time.unscaledDeltaTime;
            UpdateHints();
        }

        public bool HandleClick(Vector2 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            DamageDefinition best = null; float bestDist = float.MaxValue; bool anyNear = false;
            var center = Toy.BodyBounds.center;
            // the patient's own damages (a friend has its own hurts, D-009)
            foreach (var rtd in State.Damages)
            {
                var def = Catalog.Damage(rtd.id); if (def == null) continue;
                var dp = Toy.Node(def.damagePointName); if (dp == null) continue;
                float d = DistToRay(ray, dp.position);
                if (d > def.interactionRadius) continue;
                anyNear = true;
                // visibility rule: the spot must be seen from here (the toy's own body must not hide it)
                if (IsOccluded(dp.position)) continue;
                if (d < bestDist) { bestDist = d; best = def; }
            }
            if (best == null)
            {
                // Not a problem spot: the toy relaxes a little (comfortable reaction).
                if (!anyNear) { GameRoot.Instance.Audio.Play("click", 0.35f, 1.4f); Toy.bodyPulse = 0.01f; Invoke(nameof(Relax), 0.12f); }
                return false;
            }
            var rt = State.Get(best.id);
            if (rt.status != DamageStatus.Hidden)
            {
                OnMessage?.Invoke(Catalog.DamageNote(State.KindId, best));
                return false;
            }
            Discover(best);
            return true;
        }

        bool IsOccluded(Vector3 point)
        {
            if (probe == null) probe = SurfaceProbe.Create(Toy.Body);
            probe.Rebake();
            var from = Cam.transform.position; var to = point - from;
            return probe.Raycast(new Ray(from, to.normalized), out var hit, out _, to.magnitude) && hit.sqrMagnitude >= 0 && (hit - from).magnitude < to.magnitude - OcclusionTolerance;
        }

        void Relax() { if (Toy != null) Toy.bodyPulse = 0f; }

        static float DistToRay(Ray r, Vector3 p)
        {
            var v = p - r.origin; float t = Mathf.Max(0, Vector3.Dot(v, r.direction));
            return (p - (r.origin + r.direction * t)).magnitude;
        }

        void Discover(DamageDefinition def)
        {
            if (!State.Discover(def.id)) return;
            idle = 0; HintLevel = 0; ClearHint();
            var dp = Toy.Point(def.damagePointName);
            var sfx = GameRoot.Instance.Audio;
            sfx.Play("hint", 0.9f);
            Fx.Burst(Catalog, dp, new Color(1f, 0.85f, 0.4f), 16, 0.011f, 0.22f, 0.6f, -0.05f);
            Toy.bodyPulse = 0.02f; Invoke(nameof(Relax), 0.2f);
            Toy.PlayThen("Nervous", Toy.CurrentState == "Nervous" ? "Nervous" : "Nervous");
            OnMessage?.Invoke(Catalog.DamageNote(State.KindId, def));
            // short focus shot toward the discovered spot
            var ca = Toy.Node(def.cameraAnchorName);
            if (ca != null) StartCoroutine(FocusShot(ca.position, dp));
            OnChanged?.Invoke();
        }

        System.Collections.IEnumerator FocusShot(Vector3 camPos, Vector3 look)
        {
            Orbit.FocusOn(camPos, look);
            float t = 0; while (t < 1.2f) { t += Time.unscaledDeltaTime; yield return null; }
            Orbit.ReleaseFocus();
        }

        void UpdateHints()
        {
            var target = State.Damages.Where(d => d.status == DamageStatus.Hidden)
                .Select(d => Catalog.Damage(d.id)).Where(d => d != null).OrderBy(d => d.hintPriority).FirstOrDefault();
            if (target == null) return;
            int want = idle >= Hint3 ? 3 : idle >= Hint2 ? 2 : idle >= Hint1 ? 1 : 0;
            if (want <= HintLevel) return;
            HintLevel = want;
            var dp = Toy.Point(target.damagePointName);
            var sfx = GameRoot.Instance.Audio;
            if (want == 1)
            {
                // level 1: the toy fidgets around the problem spot
                sfx.Play("hint", 0.4f, 0.8f);
                Toy.Play("Nervous", 0.05f); Toy.bodyPulse = 0.015f; Invoke(nameof(Relax), 0.4f);
            }
            else if (want == 2)
            {
                // level 2: soft sparkle
                sfx.Play("sparkle", 0.6f);
                ClearHint(); hintRing = Fx.Ring(Catalog, dp, 0.05f, new Color(1f, 0.9f, 0.5f, 0.85f));
                hintRing.transform.SetParent(Toy.Node(target.damagePointName)?.parent, true);
                Fx.Burst(Catalog, dp, new Color(1f, 0.95f, 0.6f), 12, 0.01f, 0.15f, 0.9f, -0.03f);
            }
            else
            {
                // level 3: tell which side to look at
                var local = Toy.transform.InverseTransformPoint(dp);
                string side = Mathf.Abs(local.z) > Mathf.Abs(local.x) ? (local.z < 0 ? "뒤쪽" : "앞쪽") : (local.x > 0 ? ToyName + "의 왼쪽" : ToyName + "의 오른쪽");
                OnMessage?.Invoke(side + "도 살펴볼까요?");
                sfx.Play("hint", 0.6f);
            }
        }

        void ClearHint() { if (hintRing != null) Destroy(hintRing); hintRing = null; }

        /// <summary>Test hook for automated checks.</summary>
        public void ForceIdle(float seconds) { idle = seconds; }
    }
}
