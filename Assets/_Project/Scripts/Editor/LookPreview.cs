using System.IO;
using Mongle.Data;
using Mongle.Toy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mongle.EditorTools
{
    /// <summary>Renders still images of the generated scenes (with the toy placed) so the look can be checked without Play mode.</summary>
    public static class LookPreview
    {
        // keep in sync with VillageDirector's default orbit
        static readonly Vector3 VillageTarget = Mongle.Village.VillageDirector.DefaultOrbitTarget;
        const float VillageYaw = Mongle.Village.VillageDirector.DefaultOrbitYaw, VillagePitch = Mongle.Village.VillageDirector.DefaultOrbitPitch, VillageDist = Mongle.Village.VillageDirector.DefaultOrbitDistance;
        static Vector3 Orbit(float yaw, float pitch, float d, Vector3 target) => target + Quaternion.Euler(-pitch, yaw, 0) * new Vector3(0, 0, d);
        static bool s_allowDirty = true; // preview scenes are generated; only our own spawned toy/camera edits make them dirty
        struct Shot { public bool gallery, noToy, thornProxy; public string scene, file; public Vector3 cam, look; public float fov; public Vector3 toyPos; public float toyYaw; }

        static readonly Shot[] Shots =
        {
            new Shot { scene = "RepairShop", file = "shop_wide", cam = new Vector3(0, 0.42f, 1.4f), look = new Vector3(0, 0.22f, 0), fov = 34, toyPos = Vector3.zero, toyYaw = 0 },
            new Shot { scene = "RepairShop", file = "shop_close", cam = new Vector3(0.12f, 0.33f, 0.78f), look = new Vector3(0, 0.25f, 0), fov = 30, toyPos = Vector3.zero, toyYaw = 0 },
            new Shot { scene = "PhotoStudio", file = "studio", cam = new Vector3(0, 0.27f, 1.35f), look = new Vector3(0, 0.235f, 0), fov = 24, toyPos = Vector3.zero, toyYaw = 0 },
            new Shot { scene = "Village", file = "village", cam = Vector3.zero, look = new Vector3(0.4f, 0, 0), fov = 32, toyPos = new Vector3(0.4f, 0, 0), toyYaw = 160 },
            new Shot { scene = "RepairShop", file = "thorn_proxy", cam = new Vector3(0.32f, 0.5f, 0.62f), look = new Vector3(0.07f, 0.43f, 0f), fov = 28, toyPos = Vector3.zero, toyYaw = 0, thornProxy = true },
            new Shot { scene = "RepairShop", file = "thorn_plain", cam = new Vector3(0.32f, 0.5f, 0.62f), look = new Vector3(0.07f, 0.43f, 0f), fov = 28, toyPos = Vector3.zero, toyYaw = 0 },
            new Shot { scene = "Village", file = "shops_furniture", cam = new Vector3(-1.0f, 0.95f, -0.3f), look = new Vector3(-0.2f, 0.4f, -3.3f), fov = 36, noToy = true },
            new Shot { scene = "Village", file = "shops_plant", cam = new Vector3(3.0f, 0.95f, -0.3f), look = new Vector3(1.6f, 0.4f, -3.3f), fov = 36, noToy = true },
            new Shot { scene = "Village", file = "gallery", cam = new Vector3(0f, 0.55f, 3.3f), look = new Vector3(0f, 0.2f, 1.5f), fov = 62, noToy = true, gallery = true },
            // in-game village orbit (VillageDirector default) and tilted down to look at the background (pitch 20, d 9)
            new Shot { scene = "Village", file = "village_game", cam = Orbit(VillageYaw, VillagePitch, VillageDist, VillageTarget), look = VillageTarget, fov = 32, noToy = true },
            new Shot { scene = "Village", file = "village_low", cam = new Vector3(12f, 5f, 22f), look = new Vector3(0, 0.6f, 2f), fov = 44, noToy = true },
            new Shot { scene = "Village", file = "village_sky", cam = new Vector3(2.5f, 1.0f, 3.5f), look = new Vector3(-1.5f, 3.6f, -8f), fov = 55, noToy = true },
            // park rides (D-006)
            new Shot { scene = "Village", file = "village_carousel", cam = new Vector3(-3.0f, 2.4f, 15.5f), look = new Vector3(-8f, 1.3f, 9.5f), fov = 40, noToy = true },
            new Shot { scene = "Village", file = "village_wheel", cam = new Vector3(11f, 1.8f, 5.0f), look = new Vector3(12f, 2.5f, -2.5f), fov = 42, noToy = true },
            new Shot { scene = "Village", file = "village_balloon", cam = new Vector3(-8.6f, 1.6f, 3.8f), look = new Vector3(-12f, 2.4f, -3f), fov = 44, noToy = true },
            new Shot { scene = "Village", file = "village_top", cam = new Vector3(0.3f, 46f, 14f), look = new Vector3(0.3f, 0f, 2.9f), fov = 50, noToy = true },
            new Shot { scene = "Village", file = "village_close", cam = new Vector3(0.9f, 0.7f, 1.6f), look = new Vector3(0.4f, 0.2f, 0), fov = 32, toyPos = new Vector3(0.4f, 0, 0), toyYaw = 160 },
        };

        [MenuItem("Mongle/Look Preview")]
        public static void Run() => RunTo(Path.Combine(Directory.GetCurrentDirectory(), "docs/40_reports/shots/look"));

        /// <summary>Renders all shots one at a time across editor frames (new renderers must be registered by the GPU resident drawer first).</summary>
        public static string RunTo(string outDir)
        {
            Directory.CreateDirectory(outDir);
            string done = Path.Combine(outDir, "_done.txt"); if (File.Exists(done)) File.Delete(done);
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            int index = 0, wait = 0; string lastScene = null, report = "";
            ToyActor toy = null; var extras = new System.Collections.Generic.List<GameObject>(); bool prepared = false;
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                try
                {
                    if (index >= Shots.Length) { File.WriteAllText(done, "rendered: " + report); EditorApplication.update -= step; return; }
                    var s = Shots[index];
                    if (!prepared)
                    {
                        if (s.scene != lastScene) { EditorSceneManager.OpenScene("Assets/_Project/Scenes/" + s.scene + ".unity", OpenSceneMode.Single); lastScene = s.scene; }
                        var cam = Camera.main;
                        if (!s.noToy)
                        {
                            toy = ToyActor.Spawn(catalog, s.toyPos, Quaternion.Euler(0, s.toyYaw, 0), false);
                            if (s.thornProxy) { toy.Node("Ear_L"); var px = toy.BeginThornProxy(); report += "proxy=" + (px != null ? px.name + " rend=" + px.GetComponent<MeshRenderer>().enabled + " bounds=" + px.GetComponent<MeshRenderer>().bounds + " mats=" + px.GetComponent<MeshRenderer>().sharedMaterial.name + "/" + px.GetComponent<MeshRenderer>().sharedMaterial.shader.name : "null") + " "; }
                            var an = toy.GetComponentInChildren<Animator>();
                            if (an != null) { an.Rebind(); an.Play("Idle", 0, 0.35f); an.Update(0.05f); }
                        }
                        if (s.gallery)
                        {
                            int gi = 0;
                            foreach (var def in System.Array.FindAll(catalog.furniture, f => f != null && !f.starter))
                            {
                                var inst = (GameObject)Object.Instantiate(def.prefab, new Vector3(-2.2f + gi * 0.5f, 0, 1.5f), Quaternion.Euler(0, 15, 0));
                                for (int i = 0; ; i++) { var st = inst.transform.Find("Stage" + i); if (st == null) break; st.gameObject.SetActive(i == def.MaxStage); }
                                var dr = inst.transform.Find("Drop"); if (dr != null) dr.gameObject.SetActive(false);
                                extras.Add(inst); gi++;
                            }
                        }
                        Vector3 pos = s.cam;
                        if (s.scene == "Village" && s.cam == Vector3.zero) pos = Quaternion.Euler(-28, 42, 0) * new Vector3(0, 0, 7.8f);
                        cam.transform.position = pos; cam.transform.LookAt(s.look); cam.fieldOfView = s.fov;
                        var oc = cam.GetComponent<Mongle.Core.OrbitCamera>(); if (oc != null) oc.enabled = false;
                        prepared = true; wait = 6; return;
                    }
                    if (wait-- > 0) { EditorApplication.QueuePlayerLoopUpdate(); return; }
                    var camera = Camera.main;
                    var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); rt.antiAliasing = 1;
                    camera.targetTexture = rt; camera.Render(); camera.targetTexture = null;
                    var prev = RenderTexture.active; RenderTexture.active = rt;
                    var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                    RenderTexture.active = prev;
                    File.WriteAllBytes(Path.Combine(outDir, s.file + ".png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                    if (toy != null) { Object.DestroyImmediate(toy.gameObject); toy = null; }
                    foreach (var e in extras) Object.DestroyImmediate(e);
                    extras.Clear(); report += s.file + " "; index++; prepared = false;
                }
                catch (System.Exception e) { File.WriteAllText(done, "FAILED at shot " + index + ": " + e); EditorApplication.update -= step; }
            };
            EditorApplication.update += step;
            return "started (watch " + done + ")";
        }
    }
}
