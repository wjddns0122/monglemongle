using System;
using System.IO;
using UnityEngine;

namespace Mongle.Core
{
    public enum SaveStatus { Ok, None, Corrupt, UnsupportedVersion }

    public struct SaveLoadResult
    {
        public SaveStatus status;
        public SaveData data;
        public string message;
        public bool usedBackup;
    }

    /// <summary>
    /// Single-slot local JSON save. Writes go to a temp file first and replace the live file only when complete,
    /// keeping the previous good file as .bak. A corrupt file is never silently overwritten (D-001-9).
    /// </summary>
    public class SaveService
    {
        public string Path { get; }
        string Tmp => Path + ".tmp";
        string Bak => Path + ".bak";
        public string PhotoFolder { get; }

        public SaveService(string path, string photoFolder = null)
        {
            Path = path;
            PhotoFolder = photoFolder ?? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? ".", "photos");
        }

        public static SaveService CreateDefault()
        {
            return new SaveService(System.IO.Path.Combine(Application.persistentDataPath, "mongle_save.json"));
        }

        public bool Exists => File.Exists(Path);

        public void Save(SaveData data)
        {
            data.saveVersion = SaveData.CurrentVersion;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path) ?? ".");
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(Tmp, json);
            // Verify the temp file parses before it can replace a good save.
            var check = JsonUtility.FromJson<SaveData>(File.ReadAllText(Tmp));
            if (check == null) throw new IOException("temp save did not round-trip");
            if (File.Exists(Path)) File.Replace(Tmp, Path, Bak);
            else File.Move(Tmp, Path);
        }

        public SaveLoadResult Load()
        {
            if (!File.Exists(Path))
            {
                // A crash between temp write and replace may leave only .bak.
                if (File.Exists(Bak))
                {
                    var b = TryRead(Bak);
                    if (b.status == SaveStatus.Ok) { b.usedBackup = true; b.message = "백업 저장에서 복원했어요."; return b; }
                }
                return new SaveLoadResult { status = SaveStatus.None, message = "저장 없음" };
            }
            var r = TryRead(Path);
            if (r.status == SaveStatus.Ok) return r;
            if (r.status == SaveStatus.Corrupt && File.Exists(Bak))
            {
                var b = TryRead(Bak);
                if (b.status == SaveStatus.Ok) { b.usedBackup = true; b.message = "저장 파일이 손상되어 백업에서 복원했어요."; return b; }
            }
            return r;
        }

        SaveLoadResult TryRead(string file)
        {
            try
            {
                string json = File.ReadAllText(file);
                var d = JsonUtility.FromJson<SaveData>(json);
                if (d == null || string.IsNullOrEmpty(d.toyKindId))
                    return new SaveLoadResult { status = SaveStatus.Corrupt, message = "저장 파일을 읽을 수 없어요." };
                if (d.saveVersion > SaveData.CurrentVersion || d.saveVersion < 1)
                    return new SaveLoadResult { status = SaveStatus.UnsupportedVersion, message = "지원하지 않는 저장 버전(" + d.saveVersion + ")이에요." };
                return new SaveLoadResult { status = SaveStatus.Ok, data = d };
            }
            catch (Exception e)
            {
                return new SaveLoadResult { status = SaveStatus.Corrupt, message = "저장 파일을 읽을 수 없어요: " + e.Message };
            }
        }

        /// <summary>Moves an unreadable save aside (kept for the player) so a new game can start without overwriting it.</summary>
        public string QuarantineCurrent()
        {
            if (!File.Exists(Path)) return null;
            string dest = Path + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            File.Move(Path, dest);
            // the automatic backup must not bring the old game back the next time the game starts
            if (File.Exists(Bak)) File.Move(Bak, dest + ".bak");
            if (File.Exists(Tmp)) File.Delete(Tmp);
            return dest;
        }

        public void DeleteAllForTests()
        {
            foreach (var f in new[] { Path, Tmp, Bak }) if (File.Exists(f)) File.Delete(f);
            if (Directory.Exists(PhotoFolder)) Directory.Delete(PhotoFolder, true);
        }
    }
}
