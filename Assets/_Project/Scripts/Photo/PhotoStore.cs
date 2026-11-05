using System;
using System.IO;
using Mongle.Core;
using Mongle.Toy;
using UnityEngine;

namespace Mongle.Photo
{
    /// <summary>Local image storage for the first photo. Writes via temp file; a missing file yields null (caller shows a placeholder).</summary>
    public static class PhotoStore
    {
        public const string FirstPhotoFile = "first_photo.png";
        /// <summary>A hospital friend's first photo (one file per resident).</summary>
        public static string FileFor(string residentId) => "photo_" + residentId + ".png";

        public static string PathFor(SaveService saves, string imageFile) => System.IO.Path.Combine(saves.PhotoFolder, imageFile);

        public static void Write(SaveService saves, string imageFile, byte[] png)
        {
            Directory.CreateDirectory(saves.PhotoFolder);
            string final = PathFor(saves, imageFile), tmp = final + ".tmp";
            File.WriteAllBytes(tmp, png);
            if (File.Exists(final)) File.Replace(tmp, final, null); else File.Move(tmp, final);
        }

        /// <summary>Loads the saved photo texture, or null when the file is missing/unreadable (never throws).</summary>
        public static Texture2D Load(SaveService saves, PhotoRecord rec)
        {
            try
            {
                if (rec == null || string.IsNullOrEmpty(rec.imageFile)) return null;
                var p = PathFor(saves, rec.imageFile);
                if (!File.Exists(p)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(p))) { UnityEngine.Object.Destroy(tex); return null; }
                return tex;
            }
            catch (Exception e) { Debug.LogWarning("[Photo] load failed: " + e.Message); return null; }
        }
    }
}
