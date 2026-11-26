using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    public enum Lang { Ko = 0, En = 1 }
    /// <summary>
    /// Korean / English strings (D-038). The table is Resources/Loc/strings.txt: key, Korean, English (tab separated,
    /// "\n" for a line break). A missing English text falls back to Korean, a missing key shows the key.
    /// </summary>
    public static class Loc
    {
        static Dictionary<string, string[]> table;
        static Lang lang = Lang.Ko;
        public static event Action Changed;

        public static Lang Current
        {
            get => lang;
            set { if (lang == value) return; lang = value; Changed?.Invoke(); }
        }

        static void Load()
        {
            table = new Dictionary<string, string[]>();
            var ta = Resources.Load<TextAsset>("Loc/strings");
            if (ta == null) return;
            foreach (var raw in ta.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var cols = line.Split('\t');
                if (cols.Length < 2) continue;
                table[cols[0]] = new[] { Unescape(cols[1]), cols.Length > 2 ? Unescape(cols[2]) : "" };
            }
        }

        static string Unescape(string s) => s.Replace("\\n", "\n");

        public static bool Has(string key) { if (table == null) Load(); return table.ContainsKey(key); }

        /// <summary>Both texts of a key as the table has them (line breaks as real breaks). Editor: the LocText inspector.</summary>
        public static bool TryGetRaw(string key, out string ko, out string en)
        {
            ko = en = "";
            if (string.IsNullOrEmpty(key)) return false;
            if (table == null) Load();
            if (!table.TryGetValue(key, out var v)) return false;
            ko = v[0]; en = v[1]; return true;
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (table == null) Load();
            if (!table.TryGetValue(key, out var v)) return key;
            var s = v[(int)lang];
            return string.IsNullOrEmpty(s) ? v[0] : s;
        }

        /// <summary>
        /// Get with {0}, {1} ... filled in. An argument "@key" is itself translated (names: "@name.mongi").
        /// Korean particles follow the word: {0~이가} {0~을를} {0~은는} {0~와과}; any other {0~x} just appends x ({0~의}).
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            var s = Get(key);
            if (args == null || args.Length == 0) return s;
            var a = new object[args.Length];
            for (int i = 0; i < args.Length; i++) a[i] = args[i] is string str && str.StartsWith("@") ? Get(str.Substring(1)) : args[i];
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\{(\d)~([^}]*)\}", m =>
            {
                int i = m.Groups[1].Value[0] - '0'; var w = i < a.Length ? a[i]?.ToString() ?? "" : "";
                switch (m.Groups[2].Value)
                {
                    case "이가": return Mongle.Josa.IGa(w);
                    case "을를": return Mongle.Josa.EulReul(w);
                    case "은는": return Mongle.Josa.EunNeun(w);
                    case "와과": return Mongle.Josa.WaGwa(w);
                    default: return w + m.Groups[2].Value;
                }
            });
            try { return string.Format(s, a); } catch (FormatException) { return s; }
        }

        /// <summary>Editor / tests: forget the loaded table so an edited strings.tsv is read again.</summary>
        public static void Reload() { table = null; Changed?.Invoke(); }
    }
}
