using System.Collections.Generic;
using Mongle.Data;
using Mongle.Repair;
using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>Dirt (or missing paint) blobs stuck around one damage. The pure DirtModel holds progress; this component owns the visuals.</summary>
    public class DirtField : MonoBehaviour
    {
        /// <summary>A blob keeps where it sits on the full and on the deflated body (bone-local) and blends between them.</summary>
        public class Blob { public Transform t; public Renderer r; public float baseRadius; public Vector3 pFull, pFlat; public Quaternion qFull, qFlat; }
        public DirtModel Model { get; private set; } = new DirtModel();
        public readonly List<Blob> Blobs = new List<Blob>();
        public Color dirtColor = new Color(0.36f, 0.25f, 0.15f);
        public Color cleanColor = new Color(0.95f, 0.88f, 0.72f);
        /// <summary>The damage this field belongs to (a toy can have several dirt/paint damages, D-009).</summary>
        public string DamageId;
        MaterialPropertyBlock mpb;
        bool built;
        ToyActor toy;

        /// <summary>Mongi's belly (C-001): scatter blobs on the belly surface as seen from the front (toy forward = +Z).</summary>
        public static DirtField Build(ToyActor toy, Material mat, int count = 26, int seed = 11)
        {
            var parent = toy.Node("Spine") != null ? toy.Node("Spine") : toy.transform;
            var dp = toy.Point("DP_BELLY", toy.transform.position + new Vector3(0, 0.19f, 0.1f)) + Vector3.down * 0.03f;
            var front = toy.transform.forward;
            return Scatter("DirtField", toy, parent, mat, dp, front, toy.transform.right, Vector3.up, 0.06f, count, 1f, seed, 0f);
        }

        /// <summary>
        /// Blobs around a damage site (D-009): centre DP, outward DN - DP (toy forward without a DN); rays come in from outside along
        /// -outward. Colours come from the definition (dirt → clean, or bare wood → paint).
        /// </summary>
        public static DirtField Build(ToyActor toy, DamageDefinition def, Material mat, int seed = 11)
        {
            var parent = toy.NodeOr(def.boneName, toy.NodeOr("Spine", toy.transform));
            var dpNode = toy.Node(def.damagePointName);
            var dp = dpNode != null ? dpNode.position : toy.BodyBounds.center;
            var dn = toy.Node(def.normalPointName);
            var outward = dn != null && (dn.position - dp).sqrMagnitude > 1e-8f ? (dn.position - dp).normalized : toy.transform.forward;
            var a = Vector3.Cross(outward, Vector3.up); if (a.sqrMagnitude < 1e-4f) a = toy.transform.right; a.Normalize();
            var b = Vector3.Cross(a, outward).normalized;
            float spread = def.spread > 0f ? def.spread : 0.06f;
            var f = Scatter("DirtField_" + def.id, toy, parent, mat, dp, outward, a, b, spread, def.blobCount > 0 ? def.blobCount : 26, Mathf.Clamp(spread / 0.06f, 0.6f, 1.2f), seed + def.id.Length, 0.05f);
            f.dirtColor = def.stainColor; f.cleanColor = def.cleanColor; f.DamageId = def.id;
            f.Refresh();
            return f;
        }

        /// <summary>Rays toward the body around <paramref name="centre"/>; <paramref name="retry"/> &gt; 0 retries from just outside the surface
        /// when something else is hit first (e.g. an arm in front of the spot).</summary>
        static DirtField Scatter(string name, ToyActor toy, Transform parent, Material mat, Vector3 centre, Vector3 outward, Vector3 axisA, Vector3 axisB, float spread, int count, float size, int seed, float retry)
        {
            var field = new GameObject(name).AddComponent<DirtField>();
            field.toy = toy;
            field.transform.SetParent(parent, false);
            // the surface differs when the toy is short of cotton: probe both shapes
            float deflate0 = toy.GetBodyShape("Deflated");
            toy.SetBodyShape("Deflated", 0f); var probe = SurfaceProbe.Create(toy.Body);
            toy.SetBodyShape("Deflated", 1f); var flat = SurfaceProbe.Create(toy.Body);
            toy.SetBodyShape("Deflated", deflate0);
            var rng = new System.Random(seed);
            field.mpb = new MaterialPropertyBlock();
            for (int i = 0; i < count; i++)
            {
                float ang = (float)(rng.NextDouble() * Mathf.PI * 2);
                float rad = Mathf.Sqrt((float)rng.NextDouble()) * spread;
                var target = centre + axisA * Mathf.Cos(ang) * rad + axisB * Mathf.Sin(ang) * rad * 0.85f;
                if (!Hit(probe, target, outward, spread, retry, out var pt, out var n, out var bone)) continue;
                bool onFlat = Hit(flat, target, outward, spread, retry, out var ptF, out var nF, out var boneF);
                float r = Mathf.Lerp(0.011f, 0.021f, (float)rng.NextDouble()) * size;
                // QA 2026-11-27: where the deflated body has no surface under the spot (the belly's lower rim pulls in) a blob kept its
                // full-body place and hung in the air below the belly; such a spot gets no blob. (The deflated belly lies up to ~4 cm
                // behind the full one, so only a miss or a hit on some other part far behind counts.)
                if (!onFlat || (ptF - pt).magnitude > 0.06f) continue;
                // the whole splotch has to lie on the body, full and deflated: a flat disc on the belly's lower rim stuck out into
                // the air below it. Try it smaller once, else leave the spot.
                if (!Covers(probe, pt, n, r) || !Covers(flat, ptF, nF, r))
                {
                    r *= 0.6f;
                    if (!Covers(probe, pt, n, r) || !Covers(flat, ptF, nF, r)) continue;
                }
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Dirt_" + i;
                Destroy(go.GetComponent<Collider>());
                // ride on the bone that moves that bit of surface (a toy car's bumper is on Hips, its cabin on Spine) — in the shape the
                // toy has now: Mongi's dirt is cleaned while it is still deflated, where the lower belly follows another bone
                var under = deflate0 >= 0.5f && boneF != null ? boneF : bone;
                var host = under != null ? under : field.transform;
                go.transform.SetParent(host, true);
                var blob = new Blob
                {
                    pFull = host.InverseTransformPoint(pt + n * 0.002f), qFull = Quaternion.Inverse(host.rotation) * Quaternion.LookRotation(-n),
                    pFlat = host.InverseTransformPoint(ptF + nF * 0.002f), qFlat = Quaternion.Inverse(host.rotation) * Quaternion.LookRotation(-nF),
                };
                go.transform.localPosition = Vector3.Lerp(blob.pFull, blob.pFlat, deflate0);
                go.transform.localRotation = Quaternion.Slerp(blob.qFull, blob.qFlat, deflate0);
                go.transform.localScale = new Vector3(r * 2f, r * 2f, Thick);
                var rend = go.GetComponent<Renderer>();
                if (mat != null) rend.sharedMaterial = mat;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                blob.t = go.transform; blob.r = rend; blob.baseRadius = r;
                field.Blobs.Add(blob);
                field.Model.Add(go.transform.position, r);
            }
            Destroy(probe.gameObject); Destroy(flat.gameObject);
            field.built = true;
            field.Refresh();
            return field;
        }

        /// <summary>True when four points on the rim of a disc of radius <paramref name="r"/> at <paramref name="c"/> (facing
        /// <paramref name="n"/>) all have the surface right under them.</summary>
        static bool Covers(SurfaceProbe p, Vector3 c, Vector3 n, float r)
        {
            var a = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized; var b = Vector3.Cross(n, a);
            for (int k = 0; k < 4; k++)
            {
                float ang = (k + 0.5f) * Mathf.PI * 0.5f;
                var q = c + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * r * 0.85f;
                if (!p.Raycast(new Ray(q + n * 0.03f, -n), out var hit, out _, 0.06f)) return false;
                if (Mathf.Abs(Vector3.Dot(hit - q, n)) > r * 0.5f) return false;
            }
            return true;
        }

        static bool Hit(SurfaceProbe p, Vector3 target, Vector3 outward, float spread, float retry, out Vector3 pt, out Vector3 n, out Transform bone)
        {
            bool hit = p.Raycast(new Ray(target + outward * 0.5f, -outward), out pt, out n, out bone, 1f);
            if (retry <= 0f || (hit && (pt - target).magnitude < spread + retry)) return hit;
            return p.Raycast(new Ray(target + outward * 0.04f, -outward), out pt, out n, out bone, 0.2f);
        }

        /// <summary>D-036: blob thickness along the surface normal. 12 mm made every stain a cluster of beads (the bee's
        /// honey looked like an orange bead pile); 4 mm reads as a flat splotch.</summary>
        const float Thick = 0.004f;

        void Update() { SyncPositions(); }

        /// <summary>Blobs ride on the bones under them and follow the belly as it deflates/fills; keep the model in world space.</summary>
        public void SyncPositions()
        {
            float w = toy != null ? toy.GetBodyShape("Deflated") : 0f;
            for (int i = 0; i < Blobs.Count; i++)
            {
                var b = Blobs[i];
                b.t.localPosition = Vector3.Lerp(b.pFull, b.pFlat, w);
                b.t.localRotation = Quaternion.Slerp(b.qFull, b.qFlat, w);
                Model.Positions[i] = b.t.position;
            }
        }

        public void Refresh()
        {
            if (!built) return;
            for (int i = 0; i < Blobs.Count; i++)
            {
                float h = Model.Health[i];
                var b = Blobs[i];
                bool on = h > 0.001f && visible;
                if (b.t.gameObject.activeSelf != on) b.t.gameObject.SetActive(on);
                float s = Mathf.Lerp(0.15f, 1f, h) * b.baseRadius * 2f;
                b.t.localScale = new Vector3(s / b.t.parent.lossyScale.x, s / b.t.parent.lossyScale.y, Thick / b.t.parent.lossyScale.z);
                var c = Color.Lerp(cleanColor, dirtColor, Mathf.Clamp01(h * 1.2f));
                mpb.SetColor("_BaseColor", c);
                b.r.SetPropertyBlock(mpb);
            }
        }

        bool visible = true;
        public void SetVisible(bool v) { visible = v; Refresh(); }
        public bool Visible => visible;
    }
}
