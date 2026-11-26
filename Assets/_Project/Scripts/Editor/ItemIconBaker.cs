using System.IO;
using Mongle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-038: pictures of the game's own items (furniture, plants, accessories of the ContentCatalog) for the storage box
    /// and shops, photographed from their 3D models in a private preview scene (same soft three-light setup as the tool
    /// pictures, the shop icons' camera angle), cropped to fill the square, transparent background.
    /// Saved as Resources/UI2/Items/&lt;item id&gt;.png: plain files, so a hand-made picture with the same name can replace one.
    /// </summary>
    public static class ItemIconBaker
    {
        public const string Dir = "Assets/_Project/Resources/UI2/Items";
        const int Raw = 1024, Size = 512;

        public static string PathOf(string id) => Dir + "/" + id + ".png";

        public static string DisplayName(string id)
        {
            foreach (var g in AssetDatabase.FindAssets("t:FurnitureDefinition"))
            {
                var d = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(AssetDatabase.GUIDToAssetPath(g));
                if (d != null && d.id == id) return d.displayName;
            }
            return id;
        }

        [MenuItem("Mongle/UI v2/아이템 그림 다시 찍기")]
        static void Menu()
        {
            if (Directory.Exists(Dir) && Directory.GetFiles(Dir, "*.png").Length > 0 && !EditorUtility.DisplayDialog("아이템 그림 다시 찍기",
                "Resources/UI2/Items 의 아이템 그림을 3D 모델로 새로 찍어요.\n직접 넣은 그림이 있으면 덮어써요. 계속할까요?", "새로 찍기", "취소")) return;
            BakeAll(true);
        }

        /// <summary>Photographs every item (only the missing pictures unless <paramref name="overwrite"/>).</summary>
        public static int BakeAll(bool overwrite)
        {
            Directory.CreateDirectory(Dir);
            var scene = EditorSceneManager.NewPreviewScene();
            int n = 0;
            try
            {
                AddLight(scene, new Vector3(40f, -35f, 0f), 0.9f, new Color(1f, 0.97f, 0.92f));
                AddLight(scene, new Vector3(15f, 150f, 0f), 0.35f, new Color(0.9f, 0.94f, 1f));
                AddLight(scene, new Vector3(-25f, 200f, 0f), 0.25f, Color.white);
                foreach (var g in AssetDatabase.FindAssets("t:FurnitureDefinition"))
                {
                    var d = AssetDatabase.LoadAssetAtPath<FurnitureDefinition>(AssetDatabase.GUIDToAssetPath(g));
                    if (d == null || d.prefab == null || string.IsNullOrEmpty(d.id)) continue;
                    var path = PathOf(d.id);
                    if (!overwrite && File.Exists(path)) continue;
                    var raw = Shoot(d, scene); var pic = Fit(raw);
                    File.WriteAllBytes(path, pic.EncodeToPNG()); n++;
                    Object.DestroyImmediate(raw); if (pic != raw) Object.DestroyImmediate(pic);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            if (n > 0) AssetDatabase.Refresh();
            Debug.Log("[ItemIcons] " + n + " picture(s) in " + Dir);
            return n;
        }

        static void AddLight(Scene scene, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject("Light"); SceneManager.MoveGameObjectToScene(go, scene);
            var l = go.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        /// <summary>The model turned 28° like the shop icons (plants fully grown, the watering drop hidden).</summary>
        static Texture2D Shoot(FurnitureDefinition def, Scene scene)
        {
            var inst = Object.Instantiate(def.prefab); SceneManager.MoveGameObjectToScene(inst, scene);
            inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 28f, 0f));
            for (int i = 0; ; i++) { var st = inst.transform.Find("Stage" + i); if (st == null) break; st.gameObject.SetActive(false); }
            if (def.IsPlant) { var st = inst.transform.Find("Stage" + Mathf.Max(0, def.MaxStage)); if (st != null) st.gameObject.SetActive(true); }
            var drop = inst.transform.Find("Drop"); if (drop != null) drop.gameObject.SetActive(false);
            foreach (var c in inst.GetComponentsInChildren<Collider>()) c.enabled = false;
            Bounds b = default; bool any = false;
            foreach (var r in inst.GetComponentsInChildren<Renderer>())
            {
                if (!r.gameObject.activeInHierarchy || r is ParticleSystemRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            var camGo = new GameObject("ItemIconCam"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false; cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.fieldOfView = 24f; cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
            float radius = Mathf.Max(0.02f, b.extents.magnitude);
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            camGo.transform.position = b.center + new Vector3(0.45f, 0.42f, 0.8f).normalized * dist; camGo.transform.LookAt(b.center);
            var rt = new RenderTexture(Raw, Raw, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Raw, Raw, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Raw, Raw), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(inst);
            return tex;
        }

        /// <summary>Crops to the item and fits it to 92 % of the square.</summary>
        static Texture2D Fit(Texture2D src)
        {
            var px = src.GetPixels32(); int x0 = Raw, y0 = Raw, x1 = -1, y1 = -1;
            for (int y = 0; y < Raw; y++)
                for (int x = 0; x < Raw; x++)
                    if (px[y * Raw + x].a > 10) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
            if (x1 < 0) return src;
            float side = Mathf.Max(x1 - x0, y1 - y0) / 0.92f, cx = (x0 + x1) / 2f, cy = (y0 + y1) / 2f;
            var outTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var fit = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float u = (cx - side / 2f + (x + 0.5f) / Size * side) / Raw, v = (cy - side / 2f + (y + 0.5f) / Size * side) / Raw;
                    fit[y * Size + x] = u < 0 || v < 0 || u > 1 || v > 1 ? Color.clear : src.GetPixelBilinear(u, v);
                }
            outTex.SetPixels(fit); outTex.Apply();
            return outTex;
        }
    }
}
