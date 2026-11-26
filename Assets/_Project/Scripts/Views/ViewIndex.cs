using System;
using UnityEngine;

namespace Mongle.Views
{
    /// <summary>
    /// The 11-26 screens the game opens on demand (dialog, windows …), by name: Resources/Views/ViewIndex. The prefabs themselves
    /// stay in Assets/_UI수정 (edited there); this list only lets the game find them. Filled by the editor builders.
    /// </summary>
    [CreateAssetMenu(menuName = "Mongle/View Index")]
    public class ViewIndex : ScriptableObject
    {
        [Serializable] public struct Entry { public string name; public GameObject prefab; }
        public Entry[] screens = new Entry[0];

        public const string ResourcePath = "Views/ViewIndex";
        static ViewIndex loaded;

        /// <summary>The prefab registered as <paramref name="name"/>, or null.</summary>
        public static GameObject Prefab(string name)
        {
            if (loaded == null) loaded = Resources.Load<ViewIndex>(ResourcePath);
            if (loaded == null) { Debug.LogWarning("[ViewIndex] Resources/" + ResourcePath + " missing"); return null; }
            foreach (var e in loaded.screens) if (e.name == name && e.prefab != null) return e.prefab;
            Debug.LogWarning("[ViewIndex] no screen '" + name + "'");
            return null;
        }
    }
}
