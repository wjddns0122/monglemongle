using System.Collections.Generic;
using Mongle.Data;
using UnityEngine;

namespace Mongle.UI
{
    /// <summary>Renders small item icons from the real prefabs (once, cached) so shop cards show what you actually get.</summary>
    public static class IconBaker
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static int bakes; // each bake gets its own spot: Destroy() is deferred, so the previous item may still exist

        public static Sprite Get(FurnitureDefinition def)
        {
            if (def == null || def.prefab == null) return null;
            if (cache.TryGetValue(def.id, out var s) && s != null) return s;
            s = Bake(def.prefab, def); cache[def.id] = s; return s;
        }

        /// <summary>An icon of any prefab (a friend's house in the storage box), cached under <paramref name="key"/>.</summary>
        public static Sprite Get(string key, GameObject prefab)
        {
            if (prefab == null || string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            s = Bake(prefab, null); cache[key] = s; return s;
        }

        public static void Clear() => cache.Clear();

        // the editor's screen-prefab baker draws the cards outside play mode too (D-035): nothing may stay behind in the open scene
        static void Kill(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }

        static Sprite Bake(GameObject prefab, FurnitureDefinition def)
        {
            const int size = 256;
            var inst = Object.Instantiate(prefab, new Vector3(5000f + 40f * (bakes++ % 50), 0f, 5000f), Quaternion.Euler(0, 28f, 0));
            // plants: show the fully grown stage
            for (int i = 0; ; i++)
            {
                var st = inst.transform.Find("Stage" + i); if (st == null) break;
                st.gameObject.SetActive(false);
            }
            if (def != null && def.IsPlant) { int last = Mathf.Max(0, def.MaxStage); var st = inst.transform.Find("Stage" + last); if (st != null) st.gameObject.SetActive(true); }
            var drop = inst.transform.Find("Drop"); if (drop != null) drop.gameObject.SetActive(false);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) c.enabled = false;

            Bounds b = new Bounds(inst.transform.position + Vector3.up * 0.1f, Vector3.zero); bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var camGo = new GameObject("IconCam"); var cam = camGo.AddComponent<Camera>();
            cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 24f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 20f;
            float radius = Mathf.Max(0.05f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            var dir = new Vector3(0.45f, 0.42f, 0.8f).normalized;
            camGo.transform.position = b.center + dir * dist; camGo.transform.LookAt(b.center);
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); Kill(rt);
            Kill(camGo); Kill(inst);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
