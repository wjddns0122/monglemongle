using System;
using System.Collections.Generic;
using Mongle.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Mongle.UI2
{
    /// <summary>
    /// The v2 UI preview scene (D-038, Scenes/UiPreview.unity): every new screen over a still of the game, to compare with
    /// Figma and to try any window size. Tab / Shift+Tab: next / previous screen · R: Figma screenshot on top (half
    /// transparent) · B: backdrop on/off · L: Korean / English. The settings screen in it is fully working.
    /// </summary>
    public class UiPreview : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string name;
            public GameObject screen;
            public Texture backdrop, reference;
        }
        public List<Entry> entries = new List<Entry>();
        public RawImage backdrop, reference;
        public Text label;
        int index; bool showRef, showBack = true;

        void Start()
        {
            GameSettings.Apply();
            Show(0);
        }

        void Update()
        {
            var kb = Keyboard.current; if (kb == null || entries.Count == 0) return;
            if (kb.tabKey.wasPressedThisFrame) Show((index + (kb.shiftKey.isPressed ? entries.Count - 1 : 1)) % entries.Count);
            if (kb.rKey.wasPressedThisFrame) { showRef = !showRef; Refresh(); }
            if (kb.bKey.wasPressedThisFrame) { showBack = !showBack; Refresh(); }
            if (kb.lKey.wasPressedThisFrame) GameSettings.Set(d => d.lang = d.lang == 0 ? 1 : 0);
        }

        public void Show(int i)
        {
            index = Mathf.Clamp(i, 0, entries.Count - 1);
            for (int k = 0; k < entries.Count; k++) if (entries[k].screen != null) entries[k].screen.SetActive(k == index);
            Refresh();
        }

        void Refresh()
        {
            var e = entries[index];
            if (backdrop != null) { backdrop.texture = e.backdrop; backdrop.enabled = showBack && e.backdrop != null; }
            if (reference != null) { reference.texture = e.reference; reference.enabled = showRef && e.reference != null; }
            if (label != null) label.text = (index + 1) + "/" + entries.Count + "  " + e.name + "   ·   " + Loc.Get("preview.hint");
        }
    }
}
