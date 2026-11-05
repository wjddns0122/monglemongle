using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>
    /// Baked collider of the skinned body so tools can touch the exact (animated, deflated) surface.
    /// Rebake periodically while a repair is active.
    /// </summary>
    public class SurfaceProbe : MonoBehaviour
    {
        MeshCollider col;
        UnityEngine.Mesh baked;
        SkinnedMeshRenderer smr;
        float lastBake = -1f;
        int[] tris; BoneWeight[] bw; // source mesh topology, read once for bone lookups
        public float RebakeInterval = 0.12f;

        public static SurfaceProbe Create(SkinnedMeshRenderer source)
        {
            var go = new GameObject("SurfaceProbe");
            var p = go.AddComponent<SurfaceProbe>();
            p.smr = source;
            p.col = go.AddComponent<MeshCollider>();
            p.baked = new UnityEngine.Mesh { name = "ProbeBaked" };
            p.Rebake();
            return p;
        }

        public void Rebake()
        {
            if (smr == null) return;
            smr.BakeMesh(baked, true);
            transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
            transform.localScale = Vector3.one;
            col.sharedMesh = null; col.sharedMesh = baked;
            lastBake = Time.unscaledTime;
        }

        void Update() { if (Time.unscaledTime - lastBake > RebakeInterval) Rebake(); }

        public bool Raycast(Ray ray, out Vector3 point, out Vector3 normal, float maxDist = 10f)
        {
            if (col.Raycast(ray, out var hit, maxDist)) { point = hit.point; normal = hit.normal; return true; }
            point = Vector3.zero; normal = Vector3.up; return false;
        }

        /// <summary>Raycast that also reports the bone carrying the hit triangle (most skin weight), so things stuck there can ride on it.</summary>
        public bool Raycast(Ray ray, out Vector3 point, out Vector3 normal, out Transform bone, float maxDist = 10f)
        {
            bone = null;
            if (!col.Raycast(ray, out var hit, maxDist)) { point = Vector3.zero; normal = Vector3.up; return false; }
            point = hit.point; normal = hit.normal;
            var mesh = smr.sharedMesh;
            if (hit.triangleIndex >= 0 && mesh != null && mesh.isReadable)
            {
                if (tris == null) { tris = mesh.triangles; bw = mesh.boneWeights; }   // the baked collider keeps the source triangle order
                var bones = smr.bones;
                if (bw.Length == mesh.vertexCount && hit.triangleIndex * 3 + 2 < tris.Length)
                {
                    var sum = new System.Collections.Generic.Dictionary<int, float>();
                    for (int k = 0; k < 3; k++)
                    {
                        var w = bw[tris[hit.triangleIndex * 3 + k]];
                        void Add(int i, float v) { if (v > 0f) sum[i] = (sum.TryGetValue(i, out var s) ? s : 0f) + v; }
                        Add(w.boneIndex0, w.weight0); Add(w.boneIndex1, w.weight1); Add(w.boneIndex2, w.weight2); Add(w.boneIndex3, w.weight3);
                    }
                    int best = -1; float bestW = 0f;
                    foreach (var kv in sum) if (kv.Value > bestW) { bestW = kv.Value; best = kv.Key; }
                    if (best >= 0 && best < bones.Length) bone = bones[best];
                }
            }
            return true;
        }

        /// <summary>Closest point on the body surface to a world position (falls back to the position).</summary>
        public Vector3 Closest(Vector3 world) => col.ClosestPoint(world);

        void OnDestroy() { if (baked != null) Destroy(baked); }
    }
}
