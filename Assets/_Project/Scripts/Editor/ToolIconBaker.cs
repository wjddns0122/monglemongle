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
    /// (soft key + fill light, transparent background) and saved as Resources/UI/Tools/&lt;tool id&gt;.png — plain files, so a hand-made
    /// picture with the same name can replace any of them. Remaking asks first (it would overwrite such pictures).
    /// </summary>
    public static class ToolIconBaker
    {
        public const string Dir = "Assets/_Project/Resources/UI/Tools";
        const int Size = 512;

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
                AddLight(scene, "Key", new Vector3(38f, -32f, 0f), 1.15f, new Color(1f, 0.97f, 0.92f));
                AddLight(scene, "Fill", new Vector3(18f, 150f, 0f), 0.55f, new Color(0.9f, 0.94f, 1f));
                AddLight(scene, "Rim", new Vector3(-20f, 200f, 0f), 0.35f, Color.white);
                foreach (var t in catalog.tools)
                {
                    if (t == null || t.prefab == null) continue;
                    var path = Dir + "/" + t.id + ".png";
                    if (!overwrite && File.Exists(path)) continue;
                    File.WriteAllBytes(path, Shoot(t.prefab, scene).EncodeToPNG()); n++;
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
            Debug.Log("[ToolIcons] " + n + " picture(s) in " + Dir);
            return n;
        }

        static void AddLight(Scene scene, string name, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene);
            var l = go.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = intensity; l.color = color; l.shadows = LightShadows.None;
            go.transform.rotation = Quaternion.Euler(euler);
        }

        /// <summary>The model turned a little towards the camera, framed to fill the square with a small margin.</summary>
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
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            var dir = new Vector3(0.42f, 0.38f, -0.82f).normalized;
            camGo.transform.position = b.center + dir * dist; camGo.transform.LookAt(b.center);
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); tex.Apply();
            RenderTexture.active = prev; rt.Release(); Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(inst);
            return tex;
        }
    }
}
