using UnityEngine;

namespace Mongle.Toy
{
    /// <summary>
    /// Flaps the bee's FLY_Wing_L/R meshes (D-009). Each wing turns about an axis through its root (its origin) parallel to the toy's
    /// forward, mirrored per side. Fast while flying (village), a slow lazy flutter while resting (repair, photo).
    /// </summary>
    public class WingFlap : MonoBehaviour
    {
        public Transform Left, Right;
        public bool Flying;
        public float FlyHz = 14f, FlyDeg = 30f, RestHz = 2f, RestDeg = 10f;
        Quaternion restL, restR; float phase, hz, deg;

        /// <summary>Adds the flapper when the model has wings; null otherwise.</summary>
        public static WingFlap Attach(ToyActor toy)
        {
            var l = toy.Node("FLY_Wing_L"); var r = toy.Node("FLY_Wing_R");
            if (l == null && r == null) return null;
            var w = toy.GetComponent<WingFlap>(); if (w == null) w = toy.gameObject.AddComponent<WingFlap>();
            w.Left = l; w.Right = r;
            if (l != null) w.restL = l.localRotation;
            if (r != null) w.restR = r.localRotation;
            w.hz = w.RestHz; w.deg = w.RestDeg;
            return w;
        }

        void LateUpdate()
        {
            float k = Mathf.Clamp01(Time.deltaTime * 4f);
            hz = Mathf.Lerp(hz, Flying ? FlyHz : RestHz, k); deg = Mathf.Lerp(deg, Flying ? FlyDeg : RestDeg, k);
            phase = Mathf.Repeat(phase + Time.deltaTime * hz, 1f);
            float a = Mathf.Sin(phase * Mathf.PI * 2f) * deg;
            Turn(Left, restL, a); Turn(Right, restR, -a);
        }

        void Turn(Transform w, Quaternion rest, float a)
        {
            if (w == null) return;
            var axis = w.parent != null ? w.parent.InverseTransformDirection(transform.forward) : transform.forward;
            w.localRotation = Quaternion.AngleAxis(a, axis) * rest;
        }
    }
}
