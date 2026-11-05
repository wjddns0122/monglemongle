using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Village
{
    /// <summary>
    /// The capsule claw machine (D-014, claw_machine.py): the pile is 40 separate capsules. The claw is steered over it, lowered,
    /// its three prongs close and it takes the capsule it reaches (ClawGrab), lifts it, carries it to the drop hole in the
    /// front corner and lets go; the capsule drops through the cabinet to the chute and rolls out. A copy of the capsule is
    /// the prize; the pile gets its capsule back when the next try starts.
    /// </summary>
    public class ClawMachine : MonoBehaviour
    {
        public Transform claw, chamberA, chamberB, prizeFloor, chute, chuteOut, chuteDrop, playerSpot, top, joystick;
        public Renderer capsules;        // unused since the pile is split (kept so older prefabs load)
        public Material markerMat;       // unused (there is no marker: it is a real machine now)
        public float closeDegrees = 15.1f, carryBelowClaw = 0.31f, capsuleRadius = 0.06f;

        /// <summary>Which capsules are gone; set by the village (null = none ever leave).</summary>
        [NonSerialized] public ClawStock Stock;
        public event Action<bool> Finished;
        public ClawRound Round { get; private set; }
        public bool Busy { get; private set; }
        /// <summary>The capsule the claw is carrying / the prize rolling out (for the UI and effects).</summary>
        public Transform PrizeCapsule => prize != null ? prize.transform : null;

        class Cap { public Transform t; public Vector3 home; public Quaternion homeRot; public int id; }
        readonly List<Cap> pile = new List<Cap>();
        Transform[] prongs, tips; Quaternion[] prongRest; float closeSign = -1f;
        Vector2 xRange, zRange; float clawY, tipDrop, floorTop; Vector2 restUv;
        Transform pileRoot; Quaternion stickRest; bool ready; Vector2 shown = new Vector2(0.5f, 0.5f);
        GameObject prize; Cap taken; bool dropping;

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = Deep(c, name); if (r != null) return r; }
            return null;
        }

        void Init()
        {
            if (ready || claw == null || chamberA == null || chamberB == null) return;
            ready = true;
            var a = transform.InverseTransformPoint(chamberA.position); var b = transform.InverseTransformPoint(chamberB.position);
            xRange = new Vector2(Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x)); zRange = new Vector2(Mathf.Min(a.z, b.z), Mathf.Max(a.z, b.z));
            clawY = transform.InverseTransformPoint(claw.position).y;
            if (joystick != null) stickRest = joystick.localRotation;
            // the pile
            pileRoot = Deep(transform, "CM_CapsulePile");
            if (pileRoot != null) foreach (Transform c in pileRoot) pile.Add(new Cap { t = c, home = c.localPosition, homeRot = c.localRotation, id = ParseId(c.name, pile.Count) });
            floorTop = pile.Count > 0 ? pile.Min2(c => c.home.y) - capsuleRadius : clawY - 0.55f;
            // prongs
            prongs = new Transform[3]; tips = new Transform[3]; prongRest = new Quaternion[3];
            for (int i = 0; i < 3; i++)
            {
                prongs[i] = Deep(claw, "CM_Prong_" + (i + 1)); tips[i] = prongs[i] != null ? Deep(prongs[i], "CM_ProngTip_" + (i + 1)) : null;
                if (prongs[i] != null) prongRest[i] = prongs[i].localRotation;
            }
            tipDrop = 0.357f;
            if (tips[0] != null) tipDrop = clawY - transform.InverseTransformPoint(tips[0].position).y;
            // which way closes the prongs? (the sign of a rotation about the local X axis depends on how the FBX was imported)
            if (prongs[0] != null && tips[0] != null)
            {
                SetClose(1f, -1f); float a1 = Radius(tips[0]);
                SetClose(1f, 1f); float a2 = Radius(tips[0]);
                closeSign = a1 < a2 ? -1f : 1f;
                SetClose(0f, closeSign);
            }
            // where the claw waits: over the drop hole in the front corner
            Transform hole = chuteDrop != null ? chuteDrop : chute;
            if (hole != null)
            {
                var h = transform.InverseTransformPoint(hole.position);
                restUv = new Vector2(Mathf.Clamp01(Mathf.InverseLerp(xRange.y, xRange.x, h.x)), Mathf.Clamp01(Mathf.InverseLerp(zRange.x, zRange.y, h.z)));
            }
            else restUv = new Vector2(0.5f, 0.5f);
            shown = restUv; PlaceClaw(shown.x, shown.y);
        }

        static int ParseId(string n, int fallback) { int i = n.LastIndexOf('_'); return i >= 0 && int.TryParse(n.Substring(i + 1), out int v) ? v : fallback; }

        bool Gone(Cap c) => Stock != null && Stock.IsGone(c.id);
        /// <summary>Capsules left in the pile.</summary>
        public int Available { get { Init(); int n = 0; foreach (var c in pile) if (!Gone(c)) n++; return n; } }

        float Radius(Transform tip)
        {
            var d = tip.position - claw.position; return new Vector2(d.x, d.z).magnitude;   // world: the claw's own axes come from Blender's
        }

        void SetClose(float k, float sign)
        {
            for (int i = 0; i < 3; i++) if (prongs[i] != null) prongs[i].localRotation = prongRest[i] * Quaternion.AngleAxis(sign * closeDegrees * k, Vector3.right);
        }
        void SetClose(float k) => SetClose(k, closeSign);

        void Awake() { Init(); }

        /// <summary>Local position over the pile: x 0..1 left-right as seen from the front, y 0..1 back-front (toward the player).</summary>
        Vector3 Spot(float x, float y, float height) => new Vector3(Mathf.Lerp(xRange.y, xRange.x, x), height, Mathf.Lerp(zRange.x, zRange.y, y));
        void PlaceClaw(float x, float y, float lower = 0f)
        {
            claw.position = transform.TransformPoint(Spot(x, y, clawY - lower));
            if (joystick != null)
            {
                float dx = (x - 0.5f) * 2f, dy = (y - 0.5f) * 2f;
                joystick.localRotation = stickRest * Quaternion.Euler(dy * 14f, 0f, -dx * 14f);
            }
        }

        public Vector2 RestUv { get { Init(); return restUv; } }

        public void Begin(ClawRound round)
        {
            Init(); Stock?.Refresh(); Round = round; Busy = true; dropping = false; ClearPrize(); RestorePile(); SetClose(0f);
            shown = new Vector2(round.X, round.Y);
        }

        public void Abort() { StopAllCoroutines(); Busy = false; dropping = false; Round = null; ClearPrize(); if (ready) { RestorePile(); SetClose(0f); shown = restUv; PlaceClaw(shown.x, shown.y); } }

        void RestorePile()
        {
            foreach (var c in pile) { if (c.t == null) continue; c.t.gameObject.SetActive(!Gone(c)); c.t.SetParent(pileRoot, false); c.t.localPosition = c.home; c.t.localRotation = c.homeRot; }
            taken = null;
        }

        void ClearPrize() { if (prize != null) { Destroy(prize); prize = null; } }

        void Update()
        {
            if (!ready || Round == null) return;
            if (Round.Phase == ClawPhase.Move)
            {
                shown = Vector2.Lerp(shown, new Vector2(Round.X, Round.Y), 1f - Mathf.Exp(-14f * Time.deltaTime));
                PlaceClaw(shown.x, shown.y);
            }
            else if (Round.Phase == ClawPhase.Drop && Busy && !dropping) StartCoroutine(DropRoutine());
        }

        // ---------------- the grab ----------------
        IEnumerator DropRoutine()
        {
            dropping = true;
            var r = Round; float x = r.X, y = r.Y;
            var axis = transform.TransformPoint(Spot(x, y, 0f));
            // the capsule reached first, in machine-local numbers
            var local = new List<Vector3>(); foreach (var c in pile) local.Add(Gone(c) ? new Vector3(0f, -99f, 0f) : transform.InverseTransformPoint(c.t.position));
            int pick = ClawGrab.PickTarget(local, transform.InverseTransformPoint(axis), out float dist);
            var outcome = pick < 0 ? ClawGrab.Outcome.Miss : ClawGrab.Decide(dist, UnityEngine.Random.value);
            float slipAt = outcome == ClawGrab.Outcome.Slip ? ClawGrab.SlipPoint(UnityEngine.Random.value) : 2f;
            // how far down: to the capsule's middle when there is one under it, else just above the floor
            float tipTarget = pick >= 0 ? local[pick].y - 0.01f : floorTop + 0.03f;
            float lower = Mathf.Max(0f, clawY - tipDrop - tipTarget);
            yield return Tween(Mathf.Lerp(0.5f, 1.0f, lower / 0.5f), k => PlaceClaw(x, y, lower * Mathf.SmoothStep(0f, 1f, k)));
            yield return Tween(0.45f, k => SetClose(Mathf.SmoothStep(0f, 1f, k)));           // the prongs close
            bool holds = pick >= 0;   // it always closes on the capsule under it; whether it keeps it is another matter
            if (holds) { taken = pile[pick]; taken.t.SetParent(claw, true); }
            // lift and carry toward the hole in one go; a weak grip lets go somewhere on the way
            bool slipped = false; Cap lost = null;
            yield return Tween(1.9f, k =>
            {
                float liftK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.42f)), carryK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((k - 0.42f) / 0.58f));
                PlaceClaw(Mathf.Lerp(x, restUv.x, carryK), Mathf.Lerp(y, restUv.y, carryK), lower * (1f - liftK));
                if (!holds || taken == null || taken.t.parent != claw) return;
                HoldCapsule(k);
                if (outcome == ClawGrab.Outcome.Slip && k > slipAt - 0.09f)   // it starts to slide...
                    taken.t.position += new Vector3(Mathf.Sin(k * 90f) * 0.006f, 0f, Mathf.Cos(k * 70f) * 0.006f);
                if (outcome == ClawGrab.Outcome.Slip && !slipped && k >= slipAt)   // ...and drops
                {
                    slipped = true; lost = taken; lost.t.SetParent(pileRoot, true); taken = null;
                    StartCoroutine(FallBack(lost));
                }
            });
            if (holds && !slipped)
            {
                taken.t.SetParent(transform, true);
                yield return Tween(0.35f, k => SetClose(1f - Mathf.SmoothStep(0f, 1f, k)));
                var capGo = taken.t.gameObject; Stock?.Take(taken.id);   // gone for good (until the machine is refilled, one per 30 minutes)
                prize = Instantiate(capGo, taken.t.position, taken.t.rotation, transform); prize.name = "ClawPrize";
                capGo.SetActive(false);
                var p0 = prize.transform.position;
                var dropAt = chuteDrop != null ? chuteDrop.position : p0 + Vector3.down * 0.5f;
                yield return Tween(0.45f, k => prize.transform.position = Vector3.Lerp(p0, new Vector3(p0.x, Mathf.Min(p0.y, dropAt.y + 0.15f), p0.z), k * k));
                var q0 = prize.transform.position; var cp = chute != null ? chute.position : dropAt;
                yield return Tween(0.5f, k => prize.transform.position = Vector3.Lerp(q0, new Vector3(dropAt.x, dropAt.y, dropAt.z), k) );
                yield return Tween(0.4f, k => prize.transform.position = Vector3.Lerp(dropAt, cp, k * k));
                var outP = chuteOut != null ? chuteOut.position + Vector3.up * capsuleRadius : cp + transform.forward * 0.25f;
                var o0 = prize.transform.position;
                yield return Tween(0.55f, k => prize.transform.position = Vector3.Lerp(o0, outP, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.1f));
                yield return new WaitForSeconds(0.4f);
            }
            else
            {
                // empty prongs (nothing in reach, or it lost the capsule on the way): open them above the hole
                yield return Tween(0.4f, k => SetClose(1f - Mathf.SmoothStep(0f, 1f, k)));
            }
            shown = restUv;
            yield return new WaitForSeconds(0.4f);
            bool won = holds && !slipped;
            Busy = false; dropping = false;
            r.Finish(won);
            Finished?.Invoke(won);
        }

        /// <summary>
        /// A capsule that slipped out of the prongs falls straight down from where it was (gravity), lands on whatever is under it
        /// (the pile or the floor), bounces once and stays there: that spot is its new place in the pile.
        /// </summary>
        IEnumerator FallBack(Cap c)
        {
            var from = c.t.position; float r = capsuleRadius * Mathf.Max(0.01f, transform.lossyScale.y);
            // what is under it: the highest other capsule within touching distance, else the floor
            float landY = transform.TransformPoint(new Vector3(0f, floorTop, 0f)).y + r;
            foreach (var o in pile)
            {
                if (o == c || o.t == null || !o.t.gameObject.activeSelf || o.t.parent != pileRoot) continue;
                var d = new Vector2(o.t.position.x - from.x, o.t.position.z - from.z).magnitude;
                if (d >= 2f * r || o.t.position.y > from.y) continue;
                landY = Mathf.Max(landY, o.t.position.y + Mathf.Sqrt(4f * r * r - d * d));
            }
            const float g = 9.8f;
            float h = Mathf.Max(0f, from.y - landY), tFall = Mathf.Sqrt(2f * h / g);
            var land = new Vector3(from.x, landY, from.z);
            for (float t = 0f; t < tFall; t += Time.deltaTime) { c.t.position = from + Vector3.down * (0.5f * g * t * t); yield return null; }
            // one small bounce (about a fifth of the fall), then it rests
            float vUp = Mathf.Sqrt(2f * g * h) * 0.4f, tUp = 2f * vUp / g;
            for (float t = 0f; t < tUp; t += Time.deltaTime) { c.t.position = land + Vector3.up * (vUp * t - 0.5f * g * t * t); yield return null; }
            c.t.position = land;
            c.home = pileRoot.InverseTransformPoint(land);      // it stays where it fell
        }

        /// <summary>The capsule hangs in the closed prongs, centred under the hub.</summary>
        void HoldCapsule(float _)
        {
            if (taken == null || taken.t == null) return;
            var target = claw.position + Vector3.down * carryBelowClaw * transform.lossyScale.y;
            taken.t.position = Vector3.Lerp(taken.t.position, target, 0.35f);
        }

        static IEnumerator Tween(float seconds, Action<float> step)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) { step(Mathf.Clamp01(t / seconds)); yield return null; }
            step(1f);
        }

        /// <summary>Where the camera looks from and at when the player plays (in front of the glass).</summary>
        public void CameraPose(out Vector3 cam, out Vector3 look)
        {
            var c = transform.position; var f = transform.forward;
            cam = c + f * 2.35f + Vector3.up * 1.3f; look = c + Vector3.up * 1.0f;
        }
    }

    static class ClawListExt
    {
        public static float Min2<T>(this List<T> l, Func<T, float> f) { float m = float.MaxValue; foreach (var x in l) m = Mathf.Min(m, f(x)); return m; }
        public static List<R> Select2<T, R>(this List<T> l, Func<T, R> f) { var o = new List<R>(l.Count); foreach (var x in l) o.Add(f(x)); return o; }
    }
}
