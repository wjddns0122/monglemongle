using System.Collections;
using System.Collections.Generic;
using Mongle.Data;
using Mongle.Repair;
using UnityEngine;
using UnityEngine.AI;

namespace Mongle.Village
{
    /// <summary>
    /// A candy tree in the park that can be shaken (D-011): a click makes it wobble, sheds leaves and sparkles, and drops one to
    /// three candy fruits on the ground to pick up. It regrows for a few seconds before dropping again. The scene builder adds
    /// it (with a capsule to click); scenes built earlier get it at runtime (<see cref="Ensure"/>). Trees stay NavMesh holes.
    /// </summary>
    [DisallowMultipleComponent]
    public class FruitTree : MonoBehaviour
    {
        public int kind;                       // 0 cotton-candy tree, 1 soft-serve tree, 2 lollipop (VillageDecor.CandyTree)
        public Color fruitColor = new Color(1f, 0.74f, 0.84f);
        public float canopyHeight = 1f, canopyRadius = 0.5f;
        public float regrowSeconds = 12f;
        float readyAt, wobble; Quaternion rest; bool hasRest; Vector2 sway = Vector2.right;
        public bool Ready => Time.time >= readyAt;
        static readonly Color[] Candy = { new Color(1f, 0.62f, 0.75f), new Color(1f, 0.8f, 0.45f), new Color(0.75f, 0.66f, 1f), new Color(0.55f, 0.88f, 0.75f), new Color(1f, 0.7f, 0.55f) };

        /// <summary>Makes a scenery tree shakeable: a capsule around trunk and canopy, and where its fruit hangs.</summary>
        public static FruitTree Attach(GameObject g, int kind, Color fruit)
        {
            var t = g.GetComponent<FruitTree>(); if (t == null) t = g.AddComponent<FruitTree>();
            t.kind = kind; t.fruitColor = fruit;
            var rs = g.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return t;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var col = g.GetComponent<CapsuleCollider>(); if (col == null) col = g.AddComponent<CapsuleCollider>();
            float side = Mathf.Min(b.extents.x, b.extents.z);
            col.direction = 1; col.center = g.transform.InverseTransformPoint(b.center);
            col.radius = Mathf.Max(0.2f, side * 0.9f); col.height = Mathf.Max(col.radius * 2f, b.size.y);
            float ground = g.transform.position.y;
            t.canopyRadius = Mathf.Max(0.2f, side);
            t.canopyHeight = Mathf.Max(0.4f, b.max.y - ground - side * 0.8f);
            return t;
        }

        /// <summary>Runtime fallback for a park built before shaking trees existed: guesses the kind and colour from its parts.</summary>
        public static FruitTree Ensure(GameObject g)
        {
            var t = g.GetComponent<FruitTree>(); if (t != null) return t;
            int kind = g.transform.Find("Stick") != null ? 2 : g.transform.Find("Cherry") != null ? 1 : 0;
            var color = Candy[Spot(g.transform.position) % Candy.Length];
            var top = g.transform.Find(kind == 2 ? "Candy" : kind == 1 ? "Swirl" : "Puff");
            var r = top != null ? top.GetComponent<Renderer>() : null;
            if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_BaseColor")) color = r.sharedMaterial.GetColor("_BaseColor");
            return Attach(g, kind, color);
        }

        Color FruitColor(int i)
        {
            if (kind == 1) return new Color(1f, 0.42f, 0.55f);                       // cherries
            if (i == 0) return Color.Lerp(fruitColor, Color.white, 0.1f);
            return Candy[(i + Spot(transform.position)) % Candy.Length];
        }

        /// <summary>A small stable number from where the tree stands (so each tree keeps its own fruit colours).</summary>
        static int Spot(Vector3 p) => Mathf.Abs(Mathf.RoundToInt(p.x * 13f + p.z * 7f));

        public Vector3 CanopyWorld => transform.position + Vector3.up * canopyHeight;

        /// <summary>Wobbles the tree and drops up to <paramref name="maxFruit"/> fruits (none while it regrows). Returns how many fell.</summary>
        public int Shake(ContentCatalog cat, int maxFruit = 3)
        {
            if (!hasRest) { rest = transform.localRotation; hasRest = true; }
            wobble = 1f; sway = Random.insideUnitCircle.normalized; if (sway == Vector2.zero) sway = Vector2.right;
            var top = CanopyWorld;
            Fx.Burst(cat, top, Color.Lerp(fruitColor, Color.white, 0.25f), 18, 0.035f, 0.5f, 1.3f, 0.35f); // leaves fluttering down
            Fx.Burst(cat, top + Vector3.up * 0.15f, new Color(1f, 0.95f, 0.72f), 12, 0.016f, 0.35f, 0.8f, -0.05f); // sparkles
            if (!Ready || maxFruit <= 0) return 0;
            readyAt = Time.time + regrowSeconds;
            int n = Random.Range(1, Mathf.Min(3, maxFruit) + 1);
            // fruit rolls out on the camera's side, where the canopy does not hide it
            var cam = Camera.main; float toCam = Random.value * Mathf.PI * 2f;
            if (cam != null) { var c = cam.transform.position - transform.position; toCam = Mathf.Atan2(c.z, c.x); }
            for (int i = 0; i < n; i++)
            {
                var f = QuestProps.Fruit(kind, FruitColor(i));
                float a = toCam + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * 2.2f) + Random.Range(-0.3f, 0.3f), d = canopyRadius * 0.7f + Random.Range(0.3f, 0.65f);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var land = transform.position + dir * d;
                if (NavMesh.SamplePosition(land, out var hit, 0.7f, NavMesh.AllAreas)) land = hit.position; // on the lawn where it can be seen
                land.y = transform.position.y;
                var from = top + dir * canopyRadius * 0.4f;
                f.transform.SetPositionAndRotation(from, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                f.Drop(from, land, i * 0.14f);
            }
            return n;
        }

        /// <summary>A little sparkle so the player sees which trees to shake.</summary>
        public void Hint(ContentCatalog cat) => Fx.Burst(cat, CanopyWorld + Vector3.up * 0.2f, new Color(1f, 0.92f, 0.6f), 16, 0.02f, 0.35f, 1.1f, -0.05f);

        void Update()
        {
            if (wobble <= 0f) return;
            wobble = Mathf.Max(0f, wobble - Time.deltaTime * 1.4f);
            float s = Mathf.Sin((1f - wobble) * 26f) * 7f * wobble;
            transform.localRotation = rest * Quaternion.Euler(sway.y * s, 0f, -sway.x * s);
            if (wobble <= 0f) transform.localRotation = rest;
        }
    }

    /// <summary>A candy fruit under a tree or a glowing star of the star hunt (D-011): clicked to collect, fruit fades after a while.</summary>
    public class QuestPickup : MonoBehaviour
    {
        public enum Type { Fruit, Star }
        public Type Kind;
        public Color Tint = Color.white;
        public Transform Body;
        public float Life = 30f;
        public bool Collected { get; private set; }
        public static readonly List<QuestPickup> Live = new List<QuestPickup>();
        float age, phase; bool fading, dropping;

        void Awake() { phase = Random.value * 6f; }
        void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }
        void OnDisable() { Live.Remove(this); }

        void Update()
        {
            age += Time.deltaTime;
            if (Body != null && !Collected && !fading)
            {
                float t = Time.time + phase;
                if (Kind == Type.Star) { Body.localRotation = Quaternion.Euler(0f, t * 100f, Mathf.Sin(t * 1.7f) * 10f); Body.localPosition = Vector3.up * Mathf.Sin(t * 2.1f) * 0.07f; }
                else if (!dropping) Body.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 3.2f) * 0.05f, 1f); // a soft breathing wiggle
            }
            if (Kind == Type.Fruit && !Collected && age > Life) FadeAway();
        }

        public void Drop(Vector3 from, Vector3 to, float delay) => StartCoroutine(DropRoutine(from, to, delay));

        IEnumerator DropRoutine(Vector3 from, Vector3 to, float delay)
        {
            dropping = true;
            transform.position = from; if (Body != null) Body.localScale = Vector3.zero;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            float t = 0f, dur = 0.6f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                transform.position = Vector3.Lerp(from, to, k * k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.18f;
                if (Body != null) Body.localScale = Vector3.one * Mathf.Min(1f, k * 3f);
                yield return null;
            }
            // one little bounce
            t = 0f;
            while (t < 0.25f) { t += Time.deltaTime; transform.position = to + Vector3.up * Mathf.Sin(Mathf.Clamp01(t / 0.25f) * Mathf.PI) * 0.06f; yield return null; }
            transform.position = to; if (Body != null) Body.localScale = Vector3.one;
            dropping = false;
        }

        /// <summary>The player picked it up: a hop and a pop. False when it is already going.</summary>
        public bool Collect()
        {
            if (Collected || fading) return false;
            Collected = true; StopAllCoroutines();
            var c = GetComponent<Collider>(); if (c != null) c.enabled = false;
            StartCoroutine(Vanish(0.35f, 0.4f, 1.35f));
            return true;
        }

        /// <summary>Nobody picked it up (fruit after a while, stars when the hunt ends).</summary>
        public void FadeAway()
        {
            if (fading || Collected) return;
            fading = true; StopAllCoroutines();
            var c = GetComponent<Collider>(); if (c != null) c.enabled = false;
            StartCoroutine(Vanish(0.9f, 0f, 1f));
        }

        IEnumerator Vanish(float dur, float rise, float grow)
        {
            var p0 = transform.position; var s0 = Body != null ? Body.localScale : Vector3.one; float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01(t / dur);
                transform.position = p0 + Vector3.up * rise * Mathf.Sin(k * Mathf.PI * 0.5f);
                if (Body != null) Body.localScale = s0 * (k < 0.3f ? Mathf.Lerp(1f, grow, k / 0.3f) : Mathf.Lerp(grow, 0f, (k - 0.3f) / 0.7f));
                yield return null;
            }
            Destroy(gameObject);
        }
    }

    /// <summary>Runtime props for the friends' requests (D-011), made of primitives with the Village scene's own materials.</summary>
    public static class QuestProps
    {
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Material lit; static Mesh star;

        /// <summary>Remembers this scene's materials by name (candy, balloons and lamp stars ship with the Village scene).</summary>
        public static void Begin()
        {
            mats.Clear();
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
                foreach (var m in r.sharedMaterials) if (m != null && !mats.ContainsKey(m.name)) mats[m.name] = m;
        }

        static Material Find(params string[] names) { foreach (var n in names) if (mats.TryGetValue(n, out var m) && m != null) return m; return null; }
        static Material Fallback()
        {
            if (lit == null) { var sh = Shader.Find("Universal Render Pipeline/Lit"); if (sh != null) lit = new Material(sh) { name = "M_QuestFallback" }; }
            return lit;
        }
        public static Material Glossy => Find("M_Gumdrop0", "M_Gumdrop1", "M_Balloon0", "M_Cherry") ?? Fallback();
        public static Material Glow => Find("M_StarGlow", "M_DreamGlow") ?? Glossy;

        public static void Tint(Renderer r, Color c, Color? emission = null)
        {
            var b = new MaterialPropertyBlock(); r.GetPropertyBlock(b);
            b.SetColor("_BaseColor", c); b.SetColor("_Color", c);
            if (emission.HasValue) b.SetColor("_EmissionColor", emission.Value);
            r.SetPropertyBlock(b);
        }

        static GameObject Part(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Color c, float roll = 0f)
        {
            var g = GameObject.CreatePrimitive(t); g.name = name;
            var col = g.GetComponent<Collider>(); if (col != null) Object.Destroy(col);
            g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale; g.transform.localEulerAngles = new Vector3(0f, 0f, roll);
            var r = g.GetComponent<Renderer>(); if (m != null) r.sharedMaterial = m; Tint(r, c);
            return g;
        }

        static readonly Color Leaf = new Color(0.45f, 0.78f, 0.45f), Cream = new Color(1f, 0.96f, 0.9f);

        /// <summary>A candy fruit for a tree of this kind: a round candy fruit, a cherry, or a little lollipop.</summary>
        public static QuestPickup Fruit(int kind, Color c)
        {
            var root = new GameObject("QuestFruit");
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            var m = Glossy;
            if (kind == 2)
            {
                Part(PrimitiveType.Cylinder, "Stick", body, new Vector3(0f, 0.05f, 0f), new Vector3(0.016f, 0.05f, 0.016f), m, Cream);
                Part(PrimitiveType.Sphere, "Candy", body, new Vector3(0f, 0.15f, 0f), new Vector3(0.18f, 0.18f, 0.07f), m, c);
                Part(PrimitiveType.Sphere, "Swirl", body, new Vector3(0f, 0.15f, 0.03f), new Vector3(0.1f, 0.1f, 0.03f), m, Color.white);
            }
            else if (kind == 1)
            {
                Part(PrimitiveType.Sphere, "Cherry", body, new Vector3(0f, 0.08f, 0f), Vector3.one * 0.16f, m, c);
                Part(PrimitiveType.Sphere, "Shine", body, new Vector3(-0.035f, 0.115f, 0.05f), Vector3.one * 0.04f, m, Color.white);
                Part(PrimitiveType.Cylinder, "Stem", body, new Vector3(0.01f, 0.18f, 0f), new Vector3(0.012f, 0.04f, 0.012f), m, new Color(0.55f, 0.75f, 0.4f), -15f);
                Part(PrimitiveType.Sphere, "Leaf", body, new Vector3(0.045f, 0.21f, 0f), new Vector3(0.08f, 0.022f, 0.045f), m, Leaf, -25f);
            }
            else
            {
                Part(PrimitiveType.Sphere, "Fruit", body, new Vector3(0f, 0.085f, 0f), Vector3.one * 0.17f, m, c);
                Part(PrimitiveType.Sphere, "Shine", body, new Vector3(-0.04f, 0.125f, 0.05f), Vector3.one * 0.045f, m, Color.white);
                Part(PrimitiveType.Sphere, "Leaf", body, new Vector3(0.035f, 0.18f, 0f), new Vector3(0.08f, 0.022f, 0.045f), m, Leaf, -20f);
            }
            var sc = root.AddComponent<SphereCollider>(); sc.center = new Vector3(0f, 0.1f, 0f); sc.radius = 0.24f; // generous: easy to click from afar
            var p = root.AddComponent<QuestPickup>(); p.Kind = QuestPickup.Type.Fruit; p.Tint = c; p.Body = body; p.Life = 30f;
            return p;
        }

        public static readonly Color Gold = new Color(1f, 0.86f, 0.4f);

        /// <summary>A glowing star for the star hunt, with a soft halo that always faces the camera.</summary>
        public static QuestPickup Star(ContentCatalog cat)
        {
            var root = new GameObject("QuestStar");
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            var g = new GameObject("Star"); g.transform.SetParent(body, false); g.transform.localScale = Vector3.one * 0.22f;
            g.AddComponent<MeshFilter>().sharedMesh = StarMesh();
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = Glow; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Tint(r, Gold, new Color(1.6f, 1.3f, 0.5f));
            var halo = cat != null ? cat.Mat("M_Particle") : null;
            if (halo != null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "Halo";
                var qc = q.GetComponent<Collider>(); if (qc != null) Object.Destroy(qc);
                q.transform.SetParent(body, false); q.transform.localScale = Vector3.one * 0.8f;
                var qr = q.GetComponent<Renderer>(); qr.sharedMaterial = halo; qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Tint(qr, new Color(1f, 0.9f, 0.55f, 0.6f));
                q.AddComponent<Billboard>();
            }
            var sc = root.AddComponent<SphereCollider>(); sc.radius = 0.4f;
            var p = root.AddComponent<QuestPickup>(); p.Kind = QuestPickup.Type.Star; p.Tint = Gold; p.Body = body; p.Life = float.MaxValue;
            return p;
        }

        /// <summary>Five-point star, outer radius 1, facing +Z (same shape as the lamp stars).</summary>
        public static Mesh StarMesh()
        {
            if (star != null) return star;
            var v = new List<Vector3>(); var t = new List<int>(); var rim = new Vector3[10];
            for (int i = 0; i < 10; i++) { float rr = i % 2 == 0 ? 1f : 0.45f, a = Mathf.PI / 2 + i * Mathf.PI / 5; rim[i] = new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0); }
            const float d = 0.16f;
            foreach (int side in new[] { 1, -1 })
            {
                int c = v.Count; v.Add(new Vector3(0, 0, d * side * 1.6f));
                for (int i = 0; i < 10; i++) v.Add(rim[i] + new Vector3(0, 0, d * side));
                for (int i = 0; i < 10; i++) { int a = c + 1 + i, b = c + 1 + (i + 1) % 10; if (side > 0) { t.Add(c); t.Add(a); t.Add(b); } else { t.Add(c); t.Add(b); t.Add(a); } }
            }
            for (int i = 0; i < 10; i++)
            {
                int c = v.Count; var p0 = rim[i]; var p1 = rim[(i + 1) % 10];
                v.Add(p0 + Vector3.forward * d); v.Add(p1 + Vector3.forward * d); v.Add(p1 - Vector3.forward * d); v.Add(p0 - Vector3.forward * d);
                t.Add(c); t.Add(c + 2); t.Add(c + 1); t.Add(c); t.Add(c + 3); t.Add(c + 2);
            }
            star = new Mesh { name = "QuestStar" }; star.SetVertices(v); star.SetTriangles(t, 0); star.RecalculateNormals(); star.RecalculateBounds();
            return star;
        }
    }
}
