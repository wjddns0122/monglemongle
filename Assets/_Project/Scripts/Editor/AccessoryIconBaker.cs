using System.IO;
using System.Linq;
using Mongle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// Pictures of the accessories for the friend card's 꾸며주기 cards (11-26 "캐릭터 선택하면 뜨는창" 88:1912), photographed from
    /// their 3D models the same way as the tool pictures (ToolIconBaker: soft lights, transparent background, white sticker edge).
    /// Saved as Resources/UI/Accessories/&lt;accessory id&gt;.png: plain files, so a hand-made picture with the same name replaces one.
    /// </summary>
    public static class AccessoryIconBaker
    {
        public const string Dir = "Assets/_Project/Resources/UI/Accessories";

        [MenuItem("Mongle/UI/악세서리 아이콘 만들기 (없는 것만)")]
        static void Menu() => BakeAll(false);

        /// <summary>Photographs every accessory of the catalog (only the missing pictures unless <paramref name="overwrite"/>).</summary>
        public static int BakeAll(bool overwrite)
        {
            var catalog = Resources.Load<ContentCatalog>("ContentCatalog");
            if (catalog == null) return 0;
            Directory.CreateDirectory(Dir);
            var scene = EditorSceneManager.NewPreviewScene();
            int n = 0;
            try
            {
                ToolIconBaker.AddLight(scene, new Vector3(40f, -35f, 0f), 0.9f, new Color(1f, 0.97f, 0.92f));
                ToolIconBaker.AddLight(scene, new Vector3(15f, 150f, 0f), 0.35f, new Color(0.9f, 0.94f, 1f));
                ToolIconBaker.AddLight(scene, new Vector3(-25f, 200f, 0f), 0.25f, Color.white);
                foreach (var a in catalog.AllAccessories.Where(a => a != null && a.prefab != null))
                {
                    var path = Dir + "/" + a.id + ".png";
                    if (!overwrite && File.Exists(path)) continue;
                    var raw = ToolIconBaker.Shoot(a.prefab, scene); var pic = ToolIconBaker.Style(raw);
                    File.WriteAllBytes(path, pic.EncodeToPNG()); n++;
                    Object.DestroyImmediate(raw); Object.DestroyImmediate(pic);
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            AssetDatabase.Refresh();
            Debug.Log("[AccessoryIcons] " + n + " picture(s) in " + Dir);
            return n;
        }
    }
}
