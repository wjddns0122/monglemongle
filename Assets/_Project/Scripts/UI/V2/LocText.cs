using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// Keeps a label in the current language: set <see cref="key"/> (and optional args) instead of the text itself.
    /// Works on a TextMeshPro label (D-038 screens) or a legacy Text. <see cref="koTracking"/> is the Figma letter spacing in
    /// px, applied to Korean only (Hangul titles are tracked wide in the design; Latin words would look spaced out).
    /// </summary>
    public class LocText : MonoBehaviour
    {
        public string key;
        /// <summary>Format arguments, kept in the prefab; "@key" is translated too (names).</summary>
        public string[] args;
        public float koTracking;
        TMP_Text tmp; Text legacy;

        public static LocText On(Component label, string key, params object[] args)
        {
            var l = label.GetComponent<LocText>() ?? label.gameObject.AddComponent<LocText>();
            l.key = key; l.args = null;
            if (args != null && args.Length > 0) { l.args = new string[args.Length]; for (int i = 0; i < args.Length; i++) l.args[i] = args[i]?.ToString() ?? ""; }
            l.Refresh(); return l;
        }

        void OnEnable() { Loc.Changed += Refresh; Refresh(); }
        void OnDisable() { Loc.Changed -= Refresh; }

        public void Refresh()
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            if (legacy == null) legacy = GetComponent<Text>();
            if (string.IsNullOrEmpty(key)) return;
            string s = args != null && args.Length > 0 ? Loc.Format(key, (object[])args) : Loc.Get(key);
            if (tmp != null)
            {
                tmp.text = s;
                if (!Mathf.Approximately(koTracking, 0f)) tmp.characterSpacing = Loc.Current == Lang.Ko ? koTracking / Mathf.Max(1f, tmp.fontSize) * 100f : 0f;
            }
            else if (legacy != null)
            {
                legacy.text = s;
                var fit = GetComponent<ShrinkToFit>(); if (fit != null) fit.Fit();
            }
        }
    }
}
