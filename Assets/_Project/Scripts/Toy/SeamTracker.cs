using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>
    /// Stitch anchors that follow the real (animated + deflated) back surface. The Blender SP_* empties cannot follow blend
    /// shapes, so anchors are re-projected onto the tear mesh's current extent every frame (C-001 §9).
    /// </summary>
    public class SeamTracker : MonoBehaviour
    {
        public const int Count = 6;
        public readonly List<Transform> Anchors = new List<Transform>();
        ToyActor toy; SkinnedMeshRenderer tear; SurfaceProbe probe; UnityEngine.Mesh baked; System.Collections.Generic.List<Vector3> verts = new System.Collections.Generic.List<Vector3>();
        public float Lateral = 0.0135f, Lift = 0.0015f; // stitches cross the gap and reach onto the fabric on both sides (D-037 tear: half-width 10 mm)

        public static SeamTracker Create(ToyActor toy, SkinnedMeshRenderer tear)
        {
            var t = new GameObject("SeamTracker").AddComponent<SeamTracker>();
            t.toy = toy; t.tear = tear;
            var parent = toy.Node("Spine") != null ? toy.Node("Spine") : toy.transform;
            t.transform.SetParent(parent, false);
            for (int i = 0; i < Count; i++)
            {
                var a = new GameObject("SEAM_" + (i + 1)).transform; a.SetParent(t.transform, false); t.Anchors.Add(a);
            }
            t.probe = SurfaceProbe.Create(toy.Body);
            t.probe.transform.SetParent(t.transform, true);
            t.probe.RebakeInterval = 0.1f;
            t.Refresh();
            return t;
        }

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (tear == null || probe == null) return;
            if (baked == null) baked = new UnityEngine.Mesh();
            tear.BakeMesh(baked, true);
            baked.GetVertices(verts);
            float minY = float.MaxValue, maxY = float.MinValue, minX = float.MaxValue, maxX = float.MinValue;
            foreach (var v in verts) { var w = tear.transform.TransformPoint(v); minY = Mathf.Min(minY, w.y); maxY = Mathf.Max(maxY, w.y); minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); }
            if (verts.Count == 0) return;
            var b = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0), new Vector3(maxX - minX, maxY - minY, 0));
            float yTop = b.max.y - 0.012f, yBot = b.min.y + 0.012f;
            float z0 = toy.transform.position.z - 0.8f;
            for (int i = 0; i < Count; i++)
            {
                float y = Mathf.Lerp(yTop, yBot, i / (float)(Count - 1));
                float x = b.center.x + ((i & 1) == 0 ? -Lateral : Lateral);
                if (probe.Raycast(new Ray(new Vector3(x, y, z0), Vector3.forward), out var p, out var n, 2f))
                { Anchors[i].position = p + n * Lift; Anchors[i].rotation = Quaternion.LookRotation(n, Vector3.up); } // forward = skin normal (threads, needle)
            }
        }
    }
}
