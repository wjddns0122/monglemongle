using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>
    /// A sewn stitch between two points on the body that follows the animated body (D-037): a round thread that comes out of
    /// the fabric at <see cref="a"/>, arches over the gap and goes back in at <see cref="b"/>. <see cref="Slack"/> 1 = just
    /// pulled through (loose, sagging), 0 = pulled tight.
    /// </summary>
    public class StitchThread : MonoBehaviour
    {
        public Transform a, b;
        public Vector3 offsetA, offsetB; // world-space nudge
        public float Radius = 0.0009f;
        public float Slack;
        /// <summary>How deep the ends dive under the stitch points (SEAM_ anchors float a little above the skin).</summary>
        public float Dip = 0.0025f;
        ThreadTube tube;
        readonly List<Vector3> pts = new List<Vector3>();

        public static StitchThread Create(Transform parent, Transform a, Transform b, Material mat, float width = 0.0038f)
        {
            var go = new GameObject("Thread");
            go.transform.SetParent(parent, false);
            var th = go.AddComponent<StitchThread>();
            th.a = a; th.b = b; th.Radius = width * 0.24f;
            th.tube = ThreadTube.Create(go, mat, th.Radius);
            th.Apply();
            return th;
        }

        void LateUpdate() { Apply(); }

        void Apply()
        {
            if (a == null || b == null || tube == null) return;
            var pa = a.position + offsetA; var pb = b.position + offsetB;
            var n = (SurfaceNormal(a, pa) + SurfaceNormal(b, pb)).normalized;
            ThreadTube.Stitch(pts, pa, pb, n, Radius, Slack, Dip);
            tube.SetPoints(pts);
        }

        /// <summary>Outward skin normal at a stitch point: seam anchors carry it as their forward axis; otherwise away from the bone they ride on.</summary>
        public static Vector3 SurfaceNormal(Transform t, Vector3 p)
        {
            if (t.name.StartsWith("SEAM_") || t.name.StartsWith("XIT_") || t.name.StartsWith("ENT_")) return t.forward;
            var from = t.parent != null ? t.parent.position : p - Vector3.forward;
            var d = p - from; return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.up;
        }

        /// <summary>Creates the finished zig-zag seam across all stitch points.</summary>
        public static List<StitchThread> BuildSeam(ToyActor toy, Material mat)
        {
            var list = new List<StitchThread>();
            var seam = toy.Seam;
            if (seam == null) return list;
            for (int i = 0; i + 1 < seam.Anchors.Count; i++)
                list.Add(Create(seam.Anchors[i], seam.Anchors[i], seam.Anchors[i + 1], mat));
            return list;
        }
    }

    /// <summary>A round thread mesh rebuilt from a list of world points every frame (stitches and the thread on the needle).</summary>
    public class ThreadTube : MonoBehaviour
    {
        const int Sides = 7;
        public float Radius = 0.0009f;
        UnityEngine.Mesh mesh;
        readonly List<Vector3> verts = new List<Vector3>(), norms = new List<Vector3>();
        readonly List<int> tris = new List<int>();

        public static ThreadTube Create(GameObject host, Material mat, float radius)
        {
            var t = host.AddComponent<ThreadTube>(); t.Radius = radius;
            t.mesh = new UnityEngine.Mesh { name = "Thread" }; t.mesh.MarkDynamic();
            host.AddComponent<MeshFilter>().sharedMesh = t.mesh;
            var r = host.AddComponent<MeshRenderer>(); if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        public static ThreadTube Create(Transform parent, string name, Material mat, float radius)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            return Create(go, mat, radius);
        }

        /// <summary>
        /// One stitch from pa to pb over a skin with normal n: the ends dive <paramref name="dip"/> under the points, the middle
        /// arches over the gap; a loose thread (slack 1) arches higher and sags.
        /// </summary>
        public static void Stitch(List<Vector3> pts, Vector3 pa, Vector3 pb, Vector3 n, float r, float slack, float dip)
        {
            pts.Clear(); const int N = 14; float len = (pb - pa).magnitude;
            float lift = r + dip + len * (0.05f + 0.22f * slack);
            for (int i = 0; i <= N; i++)
            {
                float u = i / (float)N, s = Mathf.Max(0f, Mathf.Sin(Mathf.PI * u));   // sin(pi) is a hair below 0: Pow would give NaN
                pts.Add(Vector3.Lerp(pa, pb, u) + n * (lift * Mathf.Pow(s, 0.6f) - dip - 0.0012f * Mathf.Pow(1f - s, 4f))
                        + Vector3.down * (len * 0.25f * slack * s));
            }
        }

        public void Hide() { if (mesh != null) mesh.Clear(); }

        public void SetPoints(IList<Vector3> world)
        {
            if (mesh == null) return;
            int n = world.Count;
            if (n < 2) { mesh.Clear(); return; }
            verts.Clear(); norms.Clear(); tris.Clear();
            Vector3 prev = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var t = world[Mathf.Min(i + 1, n - 1)] - world[Mathf.Max(i - 1, 0)];
                t = t.sqrMagnitude > 1e-12f ? t.normalized : Vector3.up;
                var a = i == 0 ? Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized : Vector3.ProjectOnPlane(prev, t).normalized;
                if (a.sqrMagnitude < 0.5f) a = Vector3.Cross(t, Vector3.right).normalized;
                prev = a; var b = Vector3.Cross(t, a);
                for (int k = 0; k < Sides; k++)
                {
                    float ang = k * Mathf.PI * 2f / Sides; var d = a * Mathf.Cos(ang) + b * Mathf.Sin(ang);
                    verts.Add(transform.InverseTransformPoint(world[i] + d * Radius)); norms.Add(transform.InverseTransformDirection(d));
                }
            }
            for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < Sides; k++)
                {
                    int a0 = i * Sides + k, a1 = i * Sides + (k + 1) % Sides, b0 = a0 + Sides, b1 = a1 + Sides;
                    tris.Add(a0); tris.Add(a1); tris.Add(b1); tris.Add(a0); tris.Add(b1); tris.Add(b0);   // outward-facing
                }
            for (int end = 0; end < 2; end++)   // rounded-ish caps
            {
                int ring = end == 0 ? 0 : (n - 1) * Sides; var dir = end == 0 ? (world[0] - world[1]).normalized : (world[n - 1] - world[n - 2]).normalized;
                int c = verts.Count; verts.Add(transform.InverseTransformPoint(world[end == 0 ? 0 : n - 1] + dir * Radius * 0.7f)); norms.Add(transform.InverseTransformDirection(dir));
                for (int k = 0; k < Sides; k++)
                {
                    int a0 = ring + k, a1 = ring + (k + 1) % Sides;
                    if (end == 0) { tris.Add(c); tris.Add(a1); tris.Add(a0); } else { tris.Add(c); tris.Add(a0); tris.Add(a1); }
                }
            }
            mesh.Clear(); mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        }

        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
