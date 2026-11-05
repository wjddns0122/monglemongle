using System.Collections;
using Mongle.Data;
using UnityEngine;

namespace Mongle.Repair
{
    /// <summary>Small procedural feedback helpers (particles, rings). Uses catalog materials so shaders ship in builds.</summary>
    public static class Fx
    {
        public static Material ParticleMat(ContentCatalog c) => c != null ? c.Mat("M_Particle") : null;

        public static void Burst(ContentCatalog c, Vector3 pos, Color color, int count = 18, float size = 0.012f, float speed = 0.25f, float life = 0.7f, float gravity = 0.15f)
        {
            var go = new GameObject("FxBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.2f; main.loop = false; main.playOnAwake = false;
            main.startLifetime = life; main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = color; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0; em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.005f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            var m = ParticleMat(c); if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            Object.Destroy(go, life + 0.5f);
        }

        public static ParticleSystem Stream(ContentCatalog c, Transform parent, Color color, float rate = 24, float size = 0.012f, float speed = 0.12f)
        {
            var go = new GameObject("FxStream");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.playOnAwake = false;
            main.startLifetime = 0.6f; main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size); main.startColor = color; main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 25f; sh.radius = 0.004f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            var m = ParticleMat(c); if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>Pulsing ring marker that billboards to the camera.</summary>
        public static GameObject Ring(ContentCatalog c, Vector3 pos, float radius, Color color)
        {
            var go = new GameObject("FxRing");
            go.transform.position = pos;
            var lr = go.AddComponent<LineRenderer>();
            lr.loop = true; lr.useWorldSpace = false; lr.positionCount = 28; lr.widthMultiplier = 0.004f;
            for (int i = 0; i < 28; i++) { float a = i / 28f * Mathf.PI * 2; lr.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius); }
            var m = c != null ? c.Mat("M_Marker") : null; if (m != null) lr.sharedMaterial = m;
            lr.startColor = lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<Billboard>().pulse = true;
            return go;
        }
    }

    public class Billboard : MonoBehaviour
    {
        public bool pulse;
        Vector3 baseScale; void Awake() { baseScale = transform.localScale; }
        void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
            if (pulse) transform.localScale = baseScale * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f));
        }
    }

    /// <summary>Handles a tool prefab instance following the pointer.</summary>
    public class ToolView : MonoBehaviour
    {
        public Transform Tip; // the TIP empty if present, otherwise the root
        Vector3 targetPos; Quaternion targetRot; Vector3 vel;
        Renderer[] renders;
        SkinnedMeshRenderer pinch;
        public Vector3 handleDirWorldBias = Vector3.zero;

        public static ToolView Spawn(ToolDefinition def)
        {
            GameObject go;
            if (def != null && def.prefab != null) go = Instantiate(def.prefab);
            else { go = GameObject.CreatePrimitive(PrimitiveType.Capsule); go.transform.localScale = new Vector3(0.015f, 0.05f, 0.015f); Destroy(go.GetComponent<Collider>()); }
            go.name = "TOOL_" + (def != null ? def.id : "fallback");
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var tv = go.AddComponent<ToolView>();
            tv.renders = go.GetComponentsInChildren<Renderer>();
            tv.pinch = go.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (var t in go.GetComponentsInChildren<Transform>()) if (t.name == "TIP") tv.Tip = t;
            if (tv.Tip == null) tv.Tip = go.transform;
            return tv;
        }

        public void SetVisible(bool v) { foreach (var r in renders) r.enabled = v; }

        public void SetPinch(float v01)
        {
            if (pinch != null && pinch.sharedMesh != null && pinch.sharedMesh.blendShapeCount > 0) pinch.SetBlendShapeWeight(0, Mathf.Clamp01(v01) * 100f);
        }

        /// <summary>Place the tool so its TIP touches <paramref name="point"/>; the handle leans toward the camera.</summary>
        public void Follow(Vector3 point, Camera cam, bool snap = false)
        {
            var handle = (cam.transform.up * 0.55f + cam.transform.right * 0.45f - cam.transform.forward * 0.75f + handleDirWorldBias).normalized;
            targetRot = Quaternion.FromToRotation(Vector3.up, handle);
            // tip offset in root space
            var tipLocal = transform.InverseTransformPoint(Tip.position);
            targetPos = point - targetRot * Vector3.Scale(tipLocal, transform.lossyScale);
            if (snap) { transform.SetPositionAndRotation(targetPos, targetRot); vel = Vector3.zero; }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref vel, 0.04f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.3f);
            }
        }
    }
}
