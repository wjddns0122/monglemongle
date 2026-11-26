using System.Collections.Generic;
using System.IO;
using System.Text;
using Mongle.UI2;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Mongle.EditorTools
{
    /// <summary>
    /// D-039: the words of the v2 screens are edited right in Unity. The LocText inspector shows the label's Korean and English and
    /// saves changes into Resources/Loc/strings.txt; typing into the label's own TextMeshPro box saves its Korean there too, so the
    /// game (which reads strings.txt) shows what was typed.
    /// </summary>
    [CustomEditor(typeof(LocText))]
    public class LocTextEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var lt = (LocText)target;
            EditorGUILayout.Space();
            if (string.IsNullOrEmpty(lt.key))
            {
                EditorGUILayout.HelpBox("Key가 비어 있어요: 이 글자는 TextMeshPro 칸에 쓴 그대로 나와요.", MessageType.Info);
                return;
            }
            LocFile.Current(lt.key, out var ko, out var en);
            EditorGUILayout.LabelField("글자 (고치면 바로 저장돼요)", EditorStyles.boldLabel);
            bool hasArgs = lt.args != null && lt.args.Length > 0;
            if (hasArgs) EditorGUILayout.HelpBox("{0} 같은 자리에는 게임이 이름·숫자를 넣어요. 지우지 마세요.", MessageType.None);
            var wrap = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField("한국어");
            var nko = EditorGUILayout.TextArea(ko, wrap, GUILayout.MinHeight(38));
            EditorGUILayout.LabelField("English");
            var nen = EditorGUILayout.TextArea(en, wrap, GUILayout.MinHeight(38));
            if (!EditorGUI.EndChangeCheck()) return;
            if (nko != ko) LocFile.Queue(lt.key, Lang.Ko, nko);
            if (nen != en) LocFile.Queue(lt.key, Lang.En, nen);
            // the screens are made in Korean: show the new words on the label at once
            if (nko != ko && !hasArgs && lt.TryGetComponent<TMP_Text>(out var t))
            {
                Undo.RecordObject(t, "글자 고치기");
                t.text = nko;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                EditorUtility.SetDirty(t);
            }
        }
    }

    /// <summary>Writes Resources/Loc/strings.txt (key, Korean, English; tab separated, "\n" for a line break) from the editor.</summary>
    [InitializeOnLoad]
    public static class LocFile
    {
        public const string FilePath = "Assets/_Project/Resources/Loc/strings.txt";
        static readonly Dictionary<string, string[]> pending = new Dictionary<string, string[]>();
        static double due;

        static LocFile()
        {
            Undo.postprocessModifications += Typed;
            EditorApplication.update += Flush;
            EditorApplication.quitting += () => { due = 0; Flush(); };
        }

        /// <summary>Typing into the TextMeshPro box of a label that has a LocText key saves its Korean too.</summary>
        static UndoPropertyModification[] Typed(UndoPropertyModification[] mods)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return mods;
            foreach (var m in mods)
            {
                var pm = m.currentValue;
                if (pm == null || pm.propertyPath != "m_text" || !(pm.target is TMP_Text t)) continue;
                if (!t.TryGetComponent<LocText>(out var lt) || string.IsNullOrEmpty(lt.key)) continue;
                if (lt.args != null && lt.args.Length > 0)
                {
                    Debug.LogWarning("[글자] " + t.name + ": 이름·숫자가 들어가는 글자라 Loc Text 칸의 한국어/English에서 고쳐 주세요.", t);
                    continue;
                }
                Queue(lt.key, Lang.Ko, pm.value);
            }
            return mods;
        }

        /// <summary>The key's words as they will be saved (waiting edits included).</summary>
        public static void Current(string key, out string ko, out string en)
        {
            if (pending.TryGetValue(key, out var p)) { ko = p[0]; en = p[1]; return; }
            Loc.TryGetRaw(key, out ko, out en);
        }

        public static void Queue(string key, Lang lang, string text)
        {
            if (!pending.TryGetValue(key, out var p))
            {
                Loc.TryGetRaw(key, out var ko, out var en);
                pending[key] = p = new[] { ko, en };
            }
            p[(int)lang] = text ?? "";
            due = EditorApplication.timeSinceStartup + 0.8;   // saved once typing pauses
        }

        static void Flush()
        {
            if (pending.Count == 0 || EditorApplication.timeSinceStartup < due) return;
            if (!File.Exists(FilePath)) { pending.Clear(); return; }
            var bytes = File.ReadAllBytes(FilePath);
            bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            var lines = new List<string>(Encoding.UTF8.GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0)).Split('\n'));
            foreach (var kv in pending)
            {
                int at = lines.FindIndex(l => l.StartsWith(kv.Key + "\t"));
                if (at < 0)
                {
                    int end = lines.Count; while (end > 0 && lines[end - 1].Trim().Length == 0) end--;
                    lines.Insert(end, kv.Key + "\t" + Esc(kv.Value[0]) + "\t" + Esc(kv.Value[1]));
                    continue;
                }
                string cr = lines[at].EndsWith("\r") ? "\r" : "";
                var cols = new List<string>(lines[at].TrimEnd('\r').Split('\t'));
                while (cols.Count < 3) cols.Add("");
                cols[1] = Esc(kv.Value[0]); cols[2] = Esc(kv.Value[1]);
                lines[at] = string.Join("\t", cols) + cr;
            }
            Debug.Log("[글자] strings.txt 저장: " + string.Join(", ", pending.Keys));
            pending.Clear();
            File.WriteAllText(FilePath, string.Join("\n", lines), new UTF8Encoding(bom));
            AssetDatabase.ImportAsset(FilePath);
            Loc.Reload();
        }

        static string Esc(string s) => (s ?? "").Replace("\r", "").Replace("\n", "\\n").Replace("\t", " ");
    }
}
