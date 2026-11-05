using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>Dev tool: renders imported models from several angles in an empty scene and writes their real world-space sizes.</summary>
    public static class ModelProbe
    {
        public static string Run(string[] assetPaths, string outDir, bool colorize = false, float yaw = 0f)
        {
            Directory.CreateDirectory(outDir);
            string done = Path.Combine(outDir, "_probe_done.txt"); if (File.Exists(done)) File.Delete(done);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) { l.intensity = 1.1f; l.shadows = LightShadows.Soft; }
            var cam = Camera.main; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.82f, 0.9f, 0.95f);
            var report = new StringBuilder();
            var items = assetPaths.Select(p => (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(p))).ToList();
            for (int i = 0; i < items.Count; i++) items[i].transform.position = new Vector3(i * 5f, 0, 0);
            if (yaw != 0f) foreach (var it in items) it.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (colorize)
            {
                var pal = new[] { Color.red, Color.green, Color.blue, Color.yellow, Color.cyan, Color.magenta, new Color(1, .5f, 0), new Color(.5f, 0, 1), Color.white, Color.black, new Color(.5f, .25f, 0), new Color(1, .6f, .8f) };
                var names = new[] { "red", "green", "blue", "yellow", "cyan", "magenta", "orange", "purple", "white", "black", "brown", "pink" };
                foreach (var it in items) { int k = 0; foreach (var r in it.GetComponentsInChildren<Renderer>()) { var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", pal[k % 12]); r.sharedMaterial = m; report.AppendLine("COLOR " + names[k % 12] + " = " + r.name); k++; } }
            }
            int step = 0, wait = 6;
            EditorApplication.CallbackFunction fn = null;
            fn = () =>
            {
                if (wait-- > 0) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                if (step >= items.Count * 3) { File.WriteAllText(done, report.ToString()); EditorApplication.update -= fn; return; }
                int idx = step / 3, ang = step % 3; var go = items[idx];
                var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (ang == 0) report.AppendLine($"{go.name} worldSize={b.size:F3} center={b.center:F3} renderers={rs.Length}");
                if (ang == 0) foreach (var r in rs.OrderByDescending(x => x.bounds.size.magnitude).Take(40)) report.AppendLine($"    {r.name} size={r.bounds.size:F3} center={r.bounds.center:F3}");
                float dist = b.extents.magnitude / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f;
                var dirs = new[] { new Vector3(0.5f, 0.45f, 1f), new Vector3(-1f, 0.4f, -0.6f), new Vector3(0f, 1.6f, 0.4f) };
                cam.transform.position = b.center + dirs[ang].normalized * dist; cam.transform.LookAt(b.center);
                cam.nearClipPlane = 0.01f;
                var rt = new RenderTexture(900, 700, 24); cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                var prev = RenderTexture.active; RenderTexture.active = rt; var tex = new Texture2D(900, 700, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 900, 700), 0, 0); RenderTexture.active = prev;
                File.WriteAllBytes(Path.Combine(outDir, go.name.Replace("(Clone)", "") + "_" + ang + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                step++;
            };
            EditorApplication.update += fn;
            return "started";
        }
    }
}
