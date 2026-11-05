using System.Collections.Generic;
using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>A thread segment between two nodes that follows the animated body (breathing etc).</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class StitchThread : MonoBehaviour
    {
        public Transform a, b;
        public Vector3 offsetA, offsetB; // along surface normal, world-space nudge
        LineRenderer lr;
        void Awake() { lr = GetComponent<LineRenderer>(); }

        public static StitchThread Create(Transform parent, Transform a, Transform b, Material mat, float width = 0.0038f)
        {
            var go = new GameObject("Thread");
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true; lr.positionCount = 2; lr.widthMultiplier = width;
            lr.numCapVertices = 4; lr.numCornerVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (mat != null) lr.sharedMaterial = mat;
            var th = go.AddComponent<StitchThread>();
            th.a = a; th.b = b; th.Apply();
            return th;
        }

        void LateUpdate() { Apply(); }
        void Apply()
        {
            if (lr == null) lr = GetComponent<LineRenderer>();
            if (a == null || b == null) return;
            lr.SetPosition(0, a.position + offsetA);
            lr.SetPosition(1, b.position + offsetB);
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
}
