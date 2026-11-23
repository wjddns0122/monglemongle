using System.IO;
using Mongle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-033: pictures of the repair tools for the tool shelf, photographed from their 3D models in a private preview scene
    /// (soft key + fill + rim light, transparent background), then cropped to fill the square — long thin tools lie diagonally —
    /// with a white sticker edge and a soft brown shadow. Saved as Resources/UI/Tools/&lt;tool id&gt;.png: plain files, so a hand-made
    /// picture with the same name can replace any of them. Remaking asks first (it would overwrite such pictures).
    /// </summary>
    public static class ToolIconBaker
    {
        public const string Dir = "Assets/_Project/Resources/UI/Tools";
        const int Raw = 1024, Size = 512, Edge = 13;

        [MenuItem("Mongle/UI/도구 아이콘 다시 만들기")]
        static void Menu()
        {
            if (Directory.Exists(Dir) && Directory.GetFiles(Dir, "*.png").Length > 0 && !EditorUtility.DisplayDialog("도구 아이콘 다시 만들기",
                "Resources/UI/Tools 의 도구 그림을 3D 모델로 새로 찍어요.\n직접 넣은 그림이 있으면 덮어써요. 계속할까요?", "새로 만들기", "취소")) return;
            BakeAll(true);
        }

        /// <summary>Photographs every tool of the catalog (only the missing pictures unless <paramref name="overwrite"/>).</summary>
        public static int BakeAll(bool overwrite)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null || catalog.tools == null) return 0;
            Directory.CreateDirectory(Dir);
            var scene = EditorSceneManager.NewPreviewScene();
            int n = 0;
            try
            {
                AddLight(scene, new Vector3(40f, -35f, 0f), 0.9f, new Color(1f, 0.97f, 0.92f));
                AddLight(scene, new Vector3(15f, 150f, 0f), 0.35f, new Color(0.9f, 0.94f, 1f));
                AddLight(scene, new Vector3(-25f, 200f, 0f), 0.25f, Color.white);
                foreach (var t in catalog.tools)
                {
                    if (t == null || t.prefab == null) continue;
                    var path = Dir + "/" + t.id + ".png";
                    if (!overwrite && File.Exists(path)) continue;
                    var raw = Shoot(t.prefab, scene); var pic = Style(raw);
                    File.WriteAllBytes(path, pic.EncodeToPNG()); n++;
                    Object.DestroyImmediate(raw); Object.DestroyImmediate(pic);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
            Debug.Log("[ToolIcons] " + n + " picture(s) in " + Dir);
            return n;
        }

        static void AddLight(Scene scene, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Light"); SceneManager.MoveGameObjectToScene(go, scene);
            var l = go.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        /// <summary>The model turned a little towards the camera; long thin ones are rolled 38° so they lie across the picture.</summary>
        static Texture2D Shoot(GameObject prefab, Scene scene)
        {
            var inst = Object.Instantiate(prefab); SceneManager.MoveGameObjectToScene(inst, scene);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 28f, 0f));
            foreach (var c in inst.GetComponentsInChildren<Collider>()) c.enabled = false;
            Bounds b = default; bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var camGo = new GameObject("ToolIconCam"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false; cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 22f; cam.nearClipPlane = 0.01f; cam.farClipPlane = 50f;
            float radius = Mathf.Max(0.02f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            camGo.transform.position = b.center + new Vector3(0.42f, 0.38f, -0.82f).normalized * dist; camGo.transform.LookAt(b.center);
            if (b.size.y > 1.6f * Mathf.Max(b.size.x, b.size.z)) camGo.transform.Rotate(0f, 0f, 38f, Space.Self);
            var rt = new RenderTexture(Raw, Raw, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Raw, Raw, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Raw, Raw), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(inst);
            return tex;
        }

        /// <summary>Crops to the tool, fits it to 80 % of the square, and adds the white sticker edge and a soft shadow under it.</summary>
        static Texture2D Style(Texture2D src)
        {
            var px = src.GetPixels32(); int x0 = Raw, y0 = Raw, x1 = -1, y1 = -1;
            for (int y = 0; y < Raw; y++)
                for (int x = 0; x < Raw; x++)
                    if (px[y * Raw + x].a > 10) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) return src;
            float side = Mathf.Max(x1 - x0, y1 - y0) / 0.80f, cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
            var fit = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (cx - side / 2f + (x + 0.5f) / Size * side) / Raw, v = (cy - side / 2f + (y + 0.5f) / Size * side) / Raw;
                    fit[y * Size + x] = u < 0 || v < 0 || u > 1 || v > 1 ? Color.clear : src.GetPixelBilinear(u, v);
                }
            // the sticker edge: the alpha grown by Edge px (alternating 4- and 8-neighbour steps make it round enough)
            var o = new float[Size * Size]; var tmp = new float[Size * Size];
            for (int i = 0; i < o.Length; i++) o[i] = fit[i].a;
            for (int it = 0; it < Edge; it++)
            {
                bool diag = it % 2 == 1;
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        float m = o[y * Size + x];
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (!diag && dx != 0 && dy != 0) continue;
                                int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= Size || yy >= Size) continue;
                                if (o[yy * Size + xx] > m) m = o[yy * Size + xx];
                            }
                        tmp[y * Size + x] = m;
                    }
                var t = o; o = tmp; tmp = t;
            }
            // the shadow: that shape, softened and dropped a little
            const int Blur = 6, Drop = 9;
            var outPx = new Color[Size * Size]; var shade = new Color(0.48f, 0.35f, 0.30f, 1f);
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float s = 0; int n = 0;
                    for (int dy = -Blur; dy <= Blur; dy += 2)
                        for (int dx = -Blur; dx <= Blur; dx += 2)
                        {
                            int xx = x + dx, yy = y + dy + Drop; n++;
                            if (xx >= 0 && yy >= 0 && xx < Size && yy < Size) s += o[yy * Size + xx];
                        }
                    int i = y * Size + x;
                    var c = new Color(shade.r, shade.g, shade.b, s / n * 0.28f);
                    c = Over(new Color(1f, 1f, 1f, o[i]), c);
                    outPx[i] = Over(fit[i], c);
                }
            var res = new Texture2D(Size, Size, TextureFormat.RGBA32, false); res.SetPixels(outPx); res.Apply();
            return res;
        }

        static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a); if (a <= 0f) return Color.clear;
            var c = (top * top.a + bottom * bottom.a * (1f - top.a)) / a; c.a = a; return c;
        }
    }
}
