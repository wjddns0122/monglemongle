using System.IO;
using System.Linq;
using Mongle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Pictures for the keepers' requests that name no item (the round icon of a request row, 11-26 상점 / 퀘스트): a request about
    /// a thing (튤립 화분, 포근 의자, 하트 관람차 …) shows that thing's own icon at run time (IconBaker); one about watering shows
    /// the watering can, photographed from its 3D model like the tool pictures (ToolIconBaker), and one about accessories shows
    /// the first accessory's picture. Saved as Resources/UI/QuestIcons/&lt;quest id&gt;.png: plain files, so a hand-made picture
    /// with the same name replaces one (and wins over the item icon too).
    /// </summary>
    public static class QuestIconBaker
    {
        public const string Dir = "Assets/_Project/Resources/UI/QuestIcons";

        [MenuItem("Mongle/UI/부탁 아이콘 만들기 (없는 것만)")]
        static void Menu() => BakeAll(false);

        /// <summary>Makes the pictures of the requests without an item (only the missing ones unless <paramref name="overwrite"/>).</summary>
        public static int BakeAll(bool overwrite)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null) return 0;
            Directory.CreateDirectory(Dir);
            int n = 0;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                ToolIconBaker.AddLight(scene, new Vector3(40f, -35f, 0f), 0.9f, new Color(1f, 0.97f, 0.92f));
                ToolIconBaker.AddLight(scene, new Vector3(15f, 150f, 0f), 0.35f, new Color(0.9f, 0.94f, 1f));
                ToolIconBaker.AddLight(scene, new Vector3(-25f, 200f, 0f), 0.25f, Color.white);
                foreach (var q in catalog.quests.Where(q => q != null && string.IsNullOrEmpty(q.eventParam)))
                {
                    var path = Dir + "/" + q.id + ".png";
                    if (!overwrite && File.Exists(path)) continue;
                    if (q.eventId == "PlantWatered" && catalog.wateringCan != null)
                    {
                        var raw = ToolIconBaker.Shoot(catalog.wateringCan, scene); var pic = ToolIconBaker.Style(raw); var padded = Pad(pic);
                        File.WriteAllBytes(path, padded.EncodeToPNG()); n++;
                        Object.DestroyImmediate(raw); Object.DestroyImmediate(pic); Object.DestroyImmediate(padded);
                    }
                    else if (q.eventId == "AccessoryWorn")
                    {
                        var first = catalog.AllAccessories.FirstOrDefault(a => a != null && File.Exists(AccessoryIconBaker.Dir + "/" + a.id + ".png"));
                        if (first == null) continue;
                        var src = new Texture2D(2, 2); src.LoadImage(File.ReadAllBytes(AccessoryIconBaker.Dir + "/" + first.id + ".png"));
                        var padded = Pad(src); File.WriteAllBytes(path, padded.EncodeToPNG()); n++;
                        Object.DestroyImmediate(src); Object.DestroyImmediate(padded);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
            Debug.Log("[QuestIcons] " + n + " picture(s) in " + Dir);
            return n;
        }

        /// <summary>The tightly framed tool / accessory photo in a bigger clear square, so it sits on the round icon as small as
        /// the item icons do (those are shot with room around them).</summary>
        static Texture2D Pad(Texture2D src, float room = 1.5f)
        {
            int size = Mathf.CeilToInt(Mathf.Max(src.width, src.height) * room);
            var o = new Texture2D(size, size, TextureFormat.RGBA32, false);
            o.SetPixels32(new Color32[size * size]);
            o.SetPixels((size - src.width) / 2, (size - src.height) / 2, src.width, src.height, src.GetPixels());
            o.Apply();
            return o;
        }
    }
}
